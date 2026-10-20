using System.Collections.Generic;
using SmartFactory.Conveyor;
using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Logistics;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 스캔 완료 물류를 pickAnchor X 순으로 팔에 배정하고, 벨트 속도 기반 ETA로 집기 코루틴을 시작합니다.
    /// </summary>
    public class ConveyorPickDispatcher : MonoBehaviour
    {
        const float MinSpeed = 1e-4f;
        const float ItemXEpsilon = 0.05f;

        [SerializeField] ConveyorBelt conveyor;
        [SerializeField] RobotArmPickSlot[] pickSlots;
        [SerializeField] ItemSpawner itemSpawner;
        [SerializeField] float approachLeadMargin = 0.05f;
        [SerializeField] float alignBudgetSeconds = 1.5f;
        [SerializeField] int leadPoseBlendCount = 4;

        readonly List<PickJob> m_activeJobs = new List<PickJob>();
        readonly List<PickJob> m_waitQueue = new List<PickJob>();
        readonly Queue<float> m_completionTimes = new Queue<float>();
        RobotArmPickSlot[] m_sortedSlots;

        /// <summary>대기 큐 길이.</summary>
        public int WaitQueueCount => m_waitQueue.Count;

        /// <summary>최근 60초 처리량(개/분, 시뮬).</summary>
        public float RecentThroughputPerMinute
        {
            get
            {
                TrimCompletionWindow();
                return m_completionTimes.Count;
            }
        }

        sealed class PickJob
        {
            public FactoryItem item;
            public float scanStartX;
            public float scanElapsed;
            public RobotArmPickSlot assignedSlot;
            public bool pickTaskStarted;
            public bool missedLogged;
            public ItemSortOutcome sortOutcome;
        }

        /// <summary>
        /// 컨베이어·집기 슬롯 참조를 주입합니다.
        /// </summary>
        public void Configure(ConveyorBelt belt, RobotArmPickSlot[] slots, ItemSpawner spawner = null)
        {
            conveyor = belt;
            pickSlots = slots;
            if (spawner != null)
            {
                itemSpawner = spawner;
            }

            RebuildSortedSlots();
        }

        void Awake()
        {
            RebuildSortedSlots();
        }

        void RebuildSortedSlots()
        {
            if (pickSlots == null || pickSlots.Length == 0)
            {
                m_sortedSlots = System.Array.Empty<RobotArmPickSlot>();
                return;
            }

            m_sortedSlots = new RobotArmPickSlot[pickSlots.Length];
            System.Array.Copy(pickSlots, m_sortedSlots, pickSlots.Length);
            System.Array.Sort(m_sortedSlots, (a, b) => a.PickAnchorX.CompareTo(b.PickAnchorX));
        }

        /// <summary>
        /// 인식이 끝난 물류에 대해 집기 작업을 등록합니다.
        /// </summary>
        public void SchedulePick(
            FactoryItem item,
            float scanStartX,
            float scanElapsed,
            ItemSortOutcome sortOutcome = ItemSortOutcome.Error)
        {
            if (item == null || !item.gameObject.activeInHierarchy || item.AssignedArmIndex >= 0)
            {
                return;
            }

            if (item.IsErrorLogistics)
            {
                sortOutcome = ItemSortOutcome.Error;
            }

            var job = new PickJob
            {
                item = item,
                scanStartX = scanStartX,
                scanElapsed = scanElapsed,
                sortOutcome = sortOutcome
            };

            if (TryAssignSlot(job))
            {
                m_activeJobs.Add(job);
                Debug.Log(
                    $"[PickDispatcher] Item {item.ItemId} → {job.assignedSlot.Arm.name} pickX={job.assignedSlot.PickAnchorX:F2}");
            }
            else
            {
                m_waitQueue.Add(job);
                Debug.Log($"[PickDispatcher] Item {item.ItemId} queued.");
            }
        }

        void FixedUpdate()
        {
            CleanupFinishedJobs();
            ProcessWaitQueue();
            TickActiveJobs();
            ExpireMissedJobs();
        }

        /// <summary>
        /// 배치·실패로 파이프라인이 끝난 job을 제거해 동일 팔에 다음 물류를 배정할 수 있게 합니다.
        /// </summary>
        void CleanupFinishedJobs()
        {
            for (var i = m_activeJobs.Count - 1; i >= 0; i--)
            {
                var job = m_activeJobs[i];
                if (job.item == null || !job.item.gameObject.activeInHierarchy)
                {
                    m_activeJobs.RemoveAt(i);
                    continue;
                }

                if (job.pickTaskStarted && !job.item.IsRobotPipelineActive)
                {
                    NotifyJobCompleted();
                    m_activeJobs.RemoveAt(i);
                }
            }
        }

        void ProcessWaitQueue()
        {
            SortWaitQueueByPriority();
            for (var i = m_waitQueue.Count - 1; i >= 0; i--)
            {
                var job = m_waitQueue[i];
                if (job.item == null || !job.item.gameObject.activeInHierarchy)
                {
                    m_waitQueue.RemoveAt(i);
                    continue;
                }

                if (HasPassedAllPickLines(job.item))
                {
                    LogPickMissed(job);
                    m_waitQueue.RemoveAt(i);
                    continue;
                }

                if (TryAssignSlot(job))
                {
                    m_waitQueue.RemoveAt(i);
                    m_activeJobs.Add(job);
                }
            }
        }

        void TickActiveJobs()
        {
            var speed = conveyor != null && conveyor.IsRunning ? conveyor.Speed : 0f;

            for (var i = 0; i < m_activeJobs.Count; i++)
            {
                var job = m_activeJobs[i];
                if (job.pickTaskStarted || job.item == null || job.assignedSlot == null)
                {
                    continue;
                }

                var arm = job.assignedSlot.Arm;
                if (arm == null || arm.IsBusy)
                {
                    continue;
                }

                if (job.item.IsRobotPipelineActive && job.pickTaskStarted)
                {
                    continue;
                }

                if (job.assignedSlot.HasItemPassedPickLine(job.item))
                {
                    LogPickMissed(job);
                    job.missedLogged = true;
                    m_activeJobs[i] = job;
                    continue;
                }

                var itemX = job.item.transform.position.x;
                var pickX = job.assignedSlot.PickAnchorX;
                var eta = speed > MinSpeed ? (pickX - itemX) / speed : float.PositiveInfinity;
                var errorItem = job.item.IsErrorLogistics;
                var blendCount = errorItem ? 2 : leadPoseBlendCount;
                var lead = arm.PoseBlendDuration * blendCount + alignBudgetSeconds +
                           (errorItem ? 0f : approachLeadMargin);

                if (eta <= lead || job.assignedSlot.IsItemInPickWindow(job.item))
                {
                    job.pickTaskStarted = true;
                    m_activeJobs[i] = job;
                    var runId = PickLiftTrace.BeginRun(arm.ConfiguredIndex, job.item.ItemId);
                    var itemXLog = job.item.transform.position.x;
                    PickLiftTrace.LogPhase(
                        runId,
                        arm.ConfiguredIndex,
                        job.item.ItemId,
                        "PickTaskStart",
                        "{\"eta\":" + eta + ",\"lead\":" + lead + ",\"itemX\":" + itemXLog + ",\"pickX\":" +
                        pickX + ",\"beltSpeed\":" + speed + "}");
                    arm.RunPickLiftAndPlaceTask(
                        job.item,
                        job.assignedSlot,
                        runId,
                        job.sortOutcome,
                        itemSpawner);
                }
            }
        }

        void ExpireMissedJobs()
        {
            for (var i = m_activeJobs.Count - 1; i >= 0; i--)
            {
                var job = m_activeJobs[i];
                if (job.pickTaskStarted || job.missedLogged)
                {
                    if (job.pickTaskStarted && (job.item == null || !job.item.gameObject.activeInHierarchy))
                    {
                        m_activeJobs.RemoveAt(i);
                    }

                    continue;
                }

                if (job.item == null || HasPassedAllPickLines(job.item))
                {
                    LogPickMissed(job);
                    m_activeJobs.RemoveAt(i);
                }
            }
        }

        bool TryAssignSlot(PickJob job)
        {
            if (m_sortedSlots == null || job.item == null)
            {
                return false;
            }

            var itemX = job.item.transform.position.x;

            foreach (var slot in m_sortedSlots)
            {
                if (slot == null || slot.Arm == null || slot.Arm.IsBusy || ArmHasQueuedJob(slot.Arm))
                {
                    continue;
                }

                if (slot.PickAnchorX <= itemX + ItemXEpsilon)
                {
                    continue;
                }

                job.assignedSlot = slot;
                job.item.AssignedArmIndex = slot.Arm.ConfiguredIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 아직 집기·운반 중이거나 시작 대기 중인 job이 해당 팔에 붙어 있는지 확인합니다.
        /// </summary>
        bool ArmHasQueuedJob(RobotArmController arm)
        {
            if (arm == null)
            {
                return false;
            }

            for (var i = 0; i < m_activeJobs.Count; i++)
            {
                var job = m_activeJobs[i];
                if (job.assignedSlot == null || job.assignedSlot.Arm != arm || job.item == null ||
                    !job.item.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!job.pickTaskStarted || job.item.IsRobotPipelineActive)
                {
                    return true;
                }
            }

            return false;
        }

        bool HasPassedAllPickLines(FactoryItem item)
        {
            if (m_sortedSlots == null || m_sortedSlots.Length == 0 || item == null)
            {
                return true;
            }

            var last = m_sortedSlots[m_sortedSlots.Length - 1];
            return last != null && last.HasItemPassedPickLine(item);
        }

        /// <summary>
        /// 해당 로봇팔에 묶인 집기 job을 제거하고 물류 파이프라인 플래그를 해제합니다(긴급 초기화용).
        /// </summary>
        public void ClearJobsForArm(int armIndex)
        {
            ClearJobsFromList(m_activeJobs, armIndex);
            ClearJobsFromList(m_waitQueue, armIndex);
        }

        static void ClearJobsFromList(List<PickJob> jobs, int armIndex)
        {
            for (var i = jobs.Count - 1; i >= 0; i--)
            {
                var job = jobs[i];
                if (job.assignedSlot == null || job.assignedSlot.Arm == null ||
                    job.assignedSlot.Arm.ConfiguredIndex != armIndex)
                {
                    continue;
                }

                if (job.item != null)
                {
                    job.item.ClearRobotPipeline();
                    job.item.AssignedArmIndex = -1;
                    job.item.SetPickBeltStabilized(false);
                }

                jobs.RemoveAt(i);
            }
        }

        void LogPickMissed(PickJob job)
        {
            if (job.missedLogged || job.item == null)
            {
                return;
            }

            job.missedLogged = true;
            if (job.item != null)
            {
                job.item.ClearRobotPipeline();
                job.item.AssignedArmIndex = -1;
            }

            var armIndex = job.item != null ? job.item.AssignedArmIndex : -1;
            PickLiftTrace.LogPhase(
                0,
                armIndex,
                job.item.ItemId,
                "DispatcherPickMissed",
                "{\"pickTaskStarted\":" + (job.pickTaskStarted ? "true" : "false") + "}");
            Debug.LogWarning($"[PickDispatcher] PickMissed item={job.item.ItemId}");
        }

        /// <summary>대기 중인 물류 목록(우선순위 정렬 전 스냅샷).</summary>
        public List<FactoryItem> GetWaitingPackages()
        {
            var list = new List<FactoryItem>();
            for (var i = 0; i < m_waitQueue.Count; i++)
            {
                if (m_waitQueue[i].item != null)
                {
                    list.Add(m_waitQueue[i].item);
                }
            }

            return list;
        }

        /// <summary>집기·배치 완료 시 처리량 집계용.</summary>
        public void NotifyJobCompleted()
        {
            m_completionTimes.Enqueue(Time.time);
            TrimCompletionWindow();
        }

        void TrimCompletionWindow()
        {
            var cutoff = Time.time - 60f;
            while (m_completionTimes.Count > 0 && m_completionTimes.Peek() < cutoff)
            {
                m_completionTimes.Dequeue();
            }
        }

        void SortWaitQueueByPriority()
        {
            if (m_waitQueue.Count < 2)
            {
                return;
            }

            m_waitQueue.Sort(ComparePickJobs);
        }

        static int ComparePickJobs(PickJob a, PickJob b)
        {
            var itemA = a.item;
            var itemB = b.item;
            if (itemA == null && itemB == null)
            {
                return 0;
            }

            if (itemA == null)
            {
                return 1;
            }

            if (itemB == null)
            {
                return -1;
            }

            var pri = itemB.Priority.CompareTo(itemA.Priority);
            if (pri != 0)
            {
                return pri;
            }

            var ddl = itemA.DeadlineSimTime.CompareTo(itemB.DeadlineSimTime);
            if (ddl != 0)
            {
                return ddl;
            }

            return itemA.AcceptSimTime.CompareTo(itemB.AcceptSimTime);
        }
    }
}
