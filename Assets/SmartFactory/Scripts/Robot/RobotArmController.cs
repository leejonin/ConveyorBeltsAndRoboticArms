using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SmartFactory.Core;
using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 관절 각도 포즈 보간으로 집기·이동·내려놓기를 수행합니다(로봇팔_움직임.md, 회전만).
    /// </summary>
    public class RobotArmController : MonoBehaviour
    {
        [SerializeField] RobotArmRigBinding rigBinding;
        [SerializeField] RobotArmPoseSet poseSet;

        /// <summary>프리팹에 설정된 리그 바인딩.</summary>
        public RobotArmRigBinding RigBinding => rigBinding;
        [SerializeField] Transform modelRoot;
        [SerializeField] Transform endEffector;
        [SerializeField] float poseBlendDuration = 1.2f;
        [SerializeField] float gripAlignTolerance = 0.12f;
        [SerializeField] float minGanchoContactSecondsForGrab = 0.02f;
        [SerializeField] float ganchoCloseStepDegrees = 1.5f;
        [SerializeField] float ganchoCloseTimeoutSeconds = 4f;
        [SerializeField] float carryClearanceY = 0.35f;
        [SerializeField] float zoneSurfaceYOffset = 0.05f;
        [SerializeField] float placeReachTolerance = 0.14f;
        [SerializeField] float placeReachTimeoutSeconds = 10f;

        readonly BatterySimulator m_battery = new BatterySimulator();
        RobotArmRotationDriver m_driver;
        RobotArmPose m_runtimeFromPose;
        FactoryItem m_grabbedItem;
        FixedJoint m_grabJoint;
        Transform m_gripCenter;
        RobotArmGanchoContactProbe m_ganchoProbe1;
        RobotArmGanchoContactProbe m_ganchoProbe2;
        RobotArmSuctionGripper m_suctionGripper;
        RobotArmGrabJointHost m_grabJointHost;
        PickLiftSuccessEvaluator m_pickLiftEvaluator;
        RobotArmZoneTargets m_zoneTargets;
        ItemSpawner m_itemSpawner;
        Vector3 m_gripToPickAlignOffsetWorld;
        bool m_paused;
        string m_error;
        int m_robotIndex;
        int m_pickRunId = -1;
        int m_alignSampleFrame;
        float m_pickAlignWaitStartRealtime;
        float m_gancho1CloseContactSec;
        float m_gancho2CloseContactSec;

        const float AlignDofStepDegrees = 2f;
        /// <summary>물류 흡착·운반 중 grip 정렬 시 DOF 스텝(deg) 상한.</summary>
        const float HeldCarryAlignDofStepDegrees = 1.25f;
        /// <summary>흡착 근접 부착(<see cref="RobotArmSuctionGripper"/>)과 맞춘 집기 전 정렬 허용 거리.</summary>
        const float SuctionPickAlignMaxDistance = 0.14f;
        const float PickSequencePoseBlendSeconds = 0.75f;
        const int SuctionAlignBurstIterations = 10;
        const float SuctionAlignWaitMaxSeconds = 2f;
        const float MinGanchoCloseMotionDegreesSum = 30f;
        const float GripCloseGanchoSampleIntervalSeconds = 0.2f;
        /// <summary>집게 닫힘 직전 CenterOfTheGripper 정렬(1mm Gancho Collider용, gripAlignTolerance보다 엄격).</summary>
        const float GripCloseAlignMaxDistance = 0.06f;
        const float GripCloseAlignTimeoutSeconds = 1.5f;

        public bool IsBusy { get; private set; }
        public float TaskProgress { get; private set; }
        public string CurrentTask { get; private set; } = "Idle";

        /// <summary>회전 드라이버 준비 여부(테스트·UI용).</summary>
        public bool IsMotionReady => m_driver != null && m_driver.IsReady;

        /// <summary>연결된 포즈 세트.</summary>
        public RobotArmPoseSet PoseSet => poseSet;

        /// <summary>엔드이펙터 Transform(집기 앵커).</summary>
        public Transform EndEffector => endEffector;

        /// <summary>Brazo3 집게 중심(CenterOfTheGripper).</summary>
        public Transform GripCenter => m_gripCenter;

        /// <summary>포즈 보간 시간(초).</summary>
        public float PoseBlendDuration
        {
            get => poseBlendDuration;
            set => poseBlendDuration = Mathf.Max(0.05f, value);
        }

        /// <summary>Configure로 설정된 로봇 인덱스.</summary>
        public int ConfiguredIndex => m_robotIndex;

        /// <summary>
        /// LiftHold 성공 판정기를 주입합니다(FactoryOrchestrator).
        /// </summary>
        public void SetPickLiftEvaluator(PickLiftSuccessEvaluator evaluator)
        {
            m_pickLiftEvaluator = evaluator;
        }

        /// <summary>
        /// 팔별 Zone Transform 참조를 주입합니다.
        /// </summary>
        public void SetZoneTargets(RobotArmZoneTargets targets)
        {
            m_zoneTargets = targets;
        }

        /// <summary>
        /// 배치 실패 시 Despawn 등에 사용할 spawner(디스패처와 동일).
        /// </summary>
        public void SetItemSpawner(ItemSpawner spawner)
        {
            m_itemSpawner = spawner;
        }

        /// <summary>
        /// 해당 물류가 흡착(또는 레거시 FixedJoint)으로 잡혀 있는지 확인합니다.
        /// </summary>
        public bool HasActiveGrabJoint(FactoryItem item)
        {
            if (item == null)
            {
                return false;
            }

            ResolveSuctionGripperIfNeeded();
            if (m_suctionGripper != null && m_suctionGripper.IsHolding(item))
            {
                return true;
            }

            if (m_grabbedItem == item && item.IsHeldByRobot)
            {
                return true;
            }

            return m_grabJoint != null && m_grabJoint.connectedBody == item.ItemRigidbody;
        }

        /// <summary>
        /// Gancho1 흡착 컴포넌트 참조가 비어 있으면 리그에서 다시 찾습니다.
        /// </summary>
        void ResolveSuctionGripperIfNeeded()
        {
            if (m_suctionGripper != null || m_driver == null || m_driver.Rig.Gancho1 == null)
            {
                return;
            }

            m_suctionGripper = m_driver.Rig.Gancho1.GetComponent<RobotArmSuctionGripper>();
        }

        /// <summary>
        /// 집기·LiftHold 이후 Zone 분류를 진행할 수 있는지 판정합니다.
        /// </summary>
        bool CanProceedToPlaceAfterPick(FactoryItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (CurrentTask == "GrabFailed" || CurrentTask == "PickMissed")
            {
                return false;
            }

            if (IsItemSecuredForPlace(item))
            {
                return true;
            }

            return CurrentTask == "LiftHold" && item.IsHeldByRobot;
        }

        /// <summary>
        /// 흡착·Joint로 Zone 배치까지 이어갈 수 있는지 확인합니다.
        /// </summary>
        bool IsItemSecuredForPlace(FactoryItem item)
        {
            if (HasActiveGrabJoint(item))
            {
                return true;
            }

            return m_grabbedItem == item && item.IsHeldByRobot;
        }

        /// <summary>
        /// Zone Transform 참조를 Inspector·컴포넌트에서 확보합니다.
        /// </summary>
        void EnsureZoneTargetsReady()
        {
            if (m_zoneTargets != null)
            {
                return;
            }

            m_zoneTargets = GetComponent<RobotArmZoneTargets>();
        }

        /// <summary>
        /// Joint 호스트에서 호출되는 joint 파손 알림.
        /// </summary>
        public void NotifyGrabJointBroken(float breakForce)
        {
            if (m_pickRunId < 0)
            {
                return;
            }

            var itemId = m_grabbedItem != null ? m_grabbedItem.ItemId : -1;
            PickLiftTrace.LogPhase(
                m_pickRunId,
                m_robotIndex,
                itemId,
                "JointBroken",
                "{\"breakForce\":" + breakForce + ",\"jointBroken\":true}");
        }

        void Awake()
        {
            ApplyBindingIfNeeded();
        }

        /// <summary>
        /// 로봇 인덱스를 설정합니다.
        /// </summary>
        public void Configure(int robotIndex)
        {
            m_robotIndex = robotIndex;
            m_driver?.SetRobotIndex(robotIndex);
        }

        /// <summary>
        /// 포즈 세트를 설정합니다.
        /// </summary>
        public void SetPoseSet(RobotArmPoseSet set)
        {
            poseSet = set;
        }

        /// <summary>
        /// 리그 바인딩 에셋을 적용합니다.
        /// </summary>
        /// <param name="binding">본 경로가 정의된 바인딩.</param>
        public void SetupFromBinding(RobotArmRigBinding binding)
        {
            rigBinding = binding;
            ApplyBindingIfNeeded();
        }

        /// <summary>
        /// rigBinding에 따라 modelRoot에서 회전 제어 본을 해석합니다.
        /// </summary>
        public void ApplyBindingIfNeeded()
        {
            if (rigBinding == null)
            {
                return;
            }

            if (modelRoot == null)
            {
                modelRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            m_driver = new RobotArmRotationDriver(rigBinding);
            m_driver.SetRobotIndex(m_robotIndex);
            if (!m_driver.Initialize(modelRoot))
            {
                Debug.LogWarning($"{name}: RobotArmRigBinding motion 해석 실패. Principal/Brazo/Gancho 경로를 확인하세요.");
                return;
            }

            endEffector = m_driver.Rig.EndEffector;
            m_gripCenter = m_driver.Rig.GripCenter != null
                ? m_driver.Rig.GripCenter
                : RobotArmRigBinding.FindTransform(modelRoot, "CenterOfTheGripper");
            if (m_gripCenter == null && m_driver.Rig.Gancho1 != null)
            {
                m_gripCenter = m_driver.Rig.Gancho1;
            }

            if (m_gripCenter == null)
            {
                Debug.LogWarning($"{name}: CenterOfTheGripper/Gancho1을 찾지 못했습니다. 집기 정렬이 동작하지 않습니다.");
            }
            if (poseSet != null)
            {
                poseSet.EnsureDefaultPoses();
                if (poseSet.TryGetPose("Idle", out var idle))
                {
                    m_driver.ApplyPose(idle);
                    m_runtimeFromPose = ClonePose(idle);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"{name}: RobotArmPoseSet 미설정 — FactoryOrchestrator.SetPoseSet 또는 Inspector 할당 필요. 집게 닫힘 포즈 보간이 실패할 수 있습니다.");
            }

            EnsureGanchoContactProbes();
            EnsureSuctionGripper();
            EnsureGrabJointHost();
        }

        /// <summary>
        /// Gancho1에 흡착 그리퍼와 물리 충돌용 kinematic Rigidbody를 보장합니다.
        /// </summary>
        void EnsureSuctionGripper()
        {
            if (m_driver == null || m_driver.Rig.Gancho1 == null)
            {
                return;
            }

            var gancho = m_driver.Rig.Gancho1;
            EnsureGanchoEndCollisionRigidbody(gancho);
            m_suctionGripper = gancho.GetComponent<RobotArmSuctionGripper>();
            if (m_suctionGripper == null)
            {
                m_suctionGripper = gancho.gameObject.AddComponent<RobotArmSuctionGripper>();
            }
        }

        void EnsureGanchoContactProbes()
        {
            if (modelRoot == null)
            {
                return;
            }

            m_ganchoProbe1 = EnsureGanchoProbeOnPath("Gancho1_end", 1, m_ganchoProbe1);
            m_ganchoProbe2 = EnsureGanchoProbeOnPath("Gancho2_end", 2, m_ganchoProbe2);
        }

        RobotArmGanchoContactProbe EnsureGanchoProbeOnPath(
            string pathSuffix,
            int ganchoIndex,
            RobotArmGanchoContactProbe existing)
        {
            var t = RobotArmRigBinding.FindTransform(modelRoot, pathSuffix);
            if (t == null)
            {
                return existing;
            }

            if (existing != null && existing.transform == t)
            {
                EnsureGanchoEndCollisionRigidbody(t);
                return existing;
            }

            var probe = t.GetComponent<RobotArmGanchoContactProbe>();
            if (probe == null)
            {
                probe = t.gameObject.AddComponent<RobotArmGanchoContactProbe>();
            }

            probe.Configure(ganchoIndex);
            EnsureGanchoEndCollisionRigidbody(t);
            return probe;
        }

        /// <summary>
        /// Gancho_end Collider가 OnCollision*를 받도록 kinematic Rigidbody를 보장합니다(크기 변경 없음).
        /// </summary>
        void EnsureGanchoEndCollisionRigidbody(Transform ganchoEnd)
        {
            if (ganchoEnd == null || ganchoEnd.GetComponent<Collider>() == null)
            {
                return;
            }

            var rb = ganchoEnd.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = ganchoEnd.gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void EnsureGrabJointHost()
        {
            if (m_driver == null || m_driver.Rig.Brazo3 == null)
            {
                return;
            }

            var hostGo = m_driver.Rig.Brazo3.gameObject;
            m_grabJointHost = hostGo.GetComponent<RobotArmGrabJointHost>();
            if (m_grabJointHost == null)
            {
                m_grabJointHost = hostGo.AddComponent<RobotArmGrabJointHost>();
            }

            m_grabJointHost.Bind(this);
        }

        void Update()
        {
            if (!IsBusy && !m_paused)
            {
                m_battery.RechargeIdle(Time.deltaTime);
            }

            PublishStatus();
        }

        /// <summary>
        /// 작업 일시 정지.
        /// </summary>
        public void Pause()
        {
            m_paused = true;
            CurrentTask = "Paused";
        }

        /// <summary>
        /// 작업 재개.
        /// </summary>
        public void Resume()
        {
            m_paused = false;
            if (!IsBusy)
            {
                CurrentTask = "Idle";
            }
        }

        /// <summary>
        /// 오류를 주입합니다.
        /// </summary>
        public void InjectFault(string message)
        {
            m_error = message;
            StopAllCoroutines();
            IsBusy = false;
            CurrentTask = "Fault";
        }

        /// <summary>
        /// 대시보드·중앙뇌 긴급 초기화: 코루틴 중단, 흡착 해제, 파이프라인 정리, Idle 포즈.
        /// </summary>
        public void EmergencyReset()
        {
            StopAllCoroutines();
            m_paused = false;
            m_error = null;
            m_pickRunId = -1;
            m_alignSampleFrame = 0;

            var held = m_grabbedItem;
            m_suctionGripper?.SetSuction(false);
            if (m_grabJoint != null)
            {
                Destroy(m_grabJoint);
                m_grabJoint = null;
            }

            if (held != null)
            {
                held.IsHeldByRobot = false;
                held.ClearRobotPipeline();
                held.AssignedArmIndex = -1;
                held.SetPickBeltStabilized(false);
                held.RestoreWorldPhysics();
            }

            m_grabbedItem = null;
            IsBusy = false;
            TaskProgress = 0f;
            CurrentTask = "Idle";

            if (m_driver != null && m_driver.IsReady && poseSet != null && poseSet.TryGetPose("Idle", out var idle))
            {
                m_driver.ApplyPose(idle);
            }
        }

        /// <summary>
        /// 물품 분류 작업을 시작합니다.
        /// </summary>
        public Coroutine RunSortTask(FactoryItem item, Transform dropTarget, bool isMismatch, ItemSpawner spawner)
        {
            return StartCoroutine(SortRoutine(item, dropTarget, isMismatch, spawner));
        }

        /// <summary>
        /// 컨베이어 이동 중 물류를 집어 LiftHold까지 수행합니다(회전만, Zone/Despawn 없음).
        /// </summary>
        public Coroutine RunPickAndLiftTask(FactoryItem item, RobotArmPickSlot pickSlot, int pickRunId = -1)
        {
            return StartCoroutine(PickAndLiftRoutine(item, pickSlot, pickRunId));
        }

        /// <summary>
        /// 집기·LiftHold 후 판정 Zone으로 운반·내려놓기까지 수행합니다.
        /// </summary>
        public Coroutine RunPickLiftAndPlaceTask(
            FactoryItem item,
            RobotArmPickSlot pickSlot,
            int pickRunId,
            ItemSortOutcome sortOutcome,
            ItemSpawner spawner)
        {
            if (spawner != null)
            {
                m_itemSpawner = spawner;
            }

            return StartCoroutine(PickLiftAndPlaceRoutine(item, pickSlot, pickRunId, sortOutcome));
        }

        /// <summary>
        /// 단일 관절(DOF) 각도를 즉시 적용합니다.
        /// </summary>
        public bool SetJointAngle(RobotArmDof dof, float degrees)
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return false;
            }

            m_driver.SetDofAngle(dof, degrees);
            m_runtimeFromPose = CaptureCurrentPose("Manual");
            return true;
        }

        /// <summary>
        /// 단일 관절 현재 각도(도).
        /// </summary>
        public float GetJointAngle(RobotArmDof dof)
        {
            return m_driver != null && m_driver.IsReady ? m_driver.GetDofAngle(dof) : 0f;
        }

        /// <summary>
        /// 단일 관절 허용 각도 범위(도).
        /// </summary>
        public bool TryGetJointLimits(RobotArmDof dof, out float min, out float max)
        {
            min = max = 0f;
            if (m_driver == null || !m_driver.IsReady)
            {
                return false;
            }

            m_driver.GetDofLimits(dof, out min, out max);
            return true;
        }

        /// <summary>
        /// 수동 각도 포즈를 즉시 적용합니다(로컬 회전만).
        /// </summary>
        public void ApplyManualPose(RobotArmPose pose)
        {
            if (pose == null || m_driver == null || !m_driver.IsReady)
            {
                return;
            }

            m_driver.ApplyPose(pose);
            m_runtimeFromPose = ClonePose(pose);
        }

        /// <summary>
        /// 현재 관절 각도로 포즈 스냅샷을 만듭니다.
        /// </summary>
        public RobotArmPose CaptureCurrentPose(string poseName = "Manual")
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return null;
            }

            var a = m_driver.GetJointAnglesForStatus();
            return new RobotArmPose
            {
                poseName = poseName,
                principalY = a[0],
                principalZ = a[1],
                brazo1Z = a[2],
                brazo2Z = a[3],
                brazo3Z = a[4],
                gancho1Z = a[5],
                gancho2Z = a[6]
            };
        }

        /// <summary>
        /// 등록된 포즈 이름으로 보간 이동을 시작합니다.
        /// </summary>
        public Coroutine BlendToNamedPose(string poseName)
        {
            return StartCoroutine(MoveToPose(poseName, TaskProgress));
        }

        /// <summary>
        /// 분류 시퀀스 포즈만 순서대로 재생합니다(집기·물품 없음).
        /// </summary>
        public Coroutine PlayPoseSequenceDemo()
        {
            return StartCoroutine(PoseSequenceDemoRoutine());
        }

        IEnumerator PoseSequenceDemoRoutine()
        {
            if (m_paused || m_driver == null || !m_driver.IsReady || poseSet == null)
            {
                yield break;
            }

            poseSet.EnsureDefaultPoses();
            IsBusy = true;
            CurrentTask = "PoseSequenceDemo";
            var steps = new[] { "ApproachPick", "GripClosed", "ApproachDrop", "GripOpen", "Idle" };
            foreach (var step in steps)
            {
                yield return MoveToPose(step, TaskProgress);
            }

            IsBusy = false;
            CurrentTask = "Idle";
        }

        IEnumerator PickAndLiftRoutine(FactoryItem item, RobotArmPickSlot pickSlot, int pickRunId)
        {
            if (m_paused || item == null || pickSlot == null || m_gripCenter == null || m_driver == null || !m_driver.IsReady)
            {
                yield break;
            }

            if (poseSet == null)
            {
                Debug.LogWarning($"{name}: RobotArmPoseSet이 없습니다.");
                yield break;
            }

            poseSet.EnsureDefaultPoses();
            IsBusy = true;
            TaskProgress = 0f;
            CurrentTask = "PickAndLift";
            m_pickRunId = pickRunId >= 0 ? pickRunId : PickLiftTrace.ActiveRunId;
            m_alignSampleFrame = 0;
            m_ganchoProbe1?.ResetTracking(item.ItemId);
            m_ganchoProbe2?.ResetTracking(item.ItemId);

            LogPickPhase(item, "ApproachPickStart", null);
            yield return MoveToPose("ApproachPick", 0.1f, blendDurationSeconds: PickSequencePoseBlendSeconds);
            LogPickPhase(item, "ReachPickStart", null);
            yield return MoveToPose("ReachPick", 0.15f, blendDurationSeconds: PickSequencePoseBlendSeconds);
            LogPickPhase(item, "AlignWaitStart", null);
            m_pickAlignWaitStartRealtime = Time.realtimeSinceStartup;
            ResolveSuctionGripperIfNeeded();
            m_suctionGripper?.SetPickCollisionIgnore(item, true);

            while (!IsReadyForPickAttempt(item, pickSlot) && !ShouldForceSuctionPickAttempt(item, pickSlot))
            {
                TryEnterPickBeltStabilize(item, pickSlot);
                if (pickSlot.HasItemPassedPickLine(item))
                {
                    Debug.LogWarning($"{name}: PickMissed item={item.ItemId}");
                    LogPickPhase(item, "PickMissed", "{\"reason\":\"passedPickLine\"}");
                    PickLiftTrace.EndRun("PickMissed");
                    m_pickRunId = -1;
                    ReleasePickConveyorInteraction(item);
                    item.ClearRobotPipeline();
                    item.AssignedArmIndex = -1;
                    m_suctionGripper?.SetSuction(false);
                    yield return MoveToPose("Idle", 0f);
                    IsBusy = false;
                    CurrentTask = "PickMissed";
                    yield break;
                }

                StabilizeItemForGrip(item);
                var alignStopDist = m_suctionGripper != null
                    ? SuctionPickAlignMaxDistance
                    : gripAlignTolerance;
                if (m_suctionGripper != null)
                {
                    BurstAlignGripTowardItem(item, alignStopDist, SuctionAlignBurstIterations);
                }
                else
                {
                    AlignGripCenterTowardItem(item);
                }

                MaybeLogAlignSample(item, pickSlot);

                yield return null;
            }

            TryEnterPickBeltStabilize(item, pickSlot);

            if (m_suctionGripper == null)
            {
                var gripCloseAlignUntil = Time.time + GripCloseAlignTimeoutSeconds;
                while (GetGripCenterAlignDistance(item.PickAlignWorldPosition) > GripCloseAlignMaxDistance)
                {
                    if (pickSlot.HasItemPassedPickLine(item))
                    {
                        Debug.LogWarning($"{name}: PickMissed item={item.ItemId}");
                        LogPickPhase(item, "PickMissed", "{\"reason\":\"passedPickLine\"}");
                        PickLiftTrace.EndRun("PickMissed");
                        m_pickRunId = -1;
                        ReleasePickConveyorInteraction(item);
                        item.ClearRobotPipeline();
                        item.AssignedArmIndex = -1;
                        m_suctionGripper?.SetSuction(false);
                        yield return MoveToPose("Idle", 0f);
                        IsBusy = false;
                        CurrentTask = "PickMissed";
                        yield break;
                    }

                    if (Time.time > gripCloseAlignUntil)
                    {
                        break;
                    }

                    StabilizeItemForGrip(item);
                    AlignGripCenterTowardItem(item, GripCloseAlignMaxDistance);
                    SyncGanchoPhysicsTransforms();
                    yield return null;
                }
            }

            StabilizeItemForGrip(item);
            var suctionAttached = false;
            yield return WaitSuctionAttachRoutine(item, pickSlot, r => suctionAttached = r);
            if (!suctionAttached)
            {
                Debug.LogWarning($"{name}: Suction attach timeout item={item.ItemId}");
                LogPickPhase(item, "GrabFailed", "{\"reason\":\"suctionAttachTimeout\"}");
                PickLiftTrace.EndRun("GrabFailed");
                m_pickRunId = -1;
                ReleasePickConveyorInteraction(item);
                item.ClearRobotPipeline();
                item.AssignedArmIndex = -1;
                m_suctionGripper?.SetSuction(false);
                yield return MoveToPose("Idle", 0f);
                IsBusy = false;
                CurrentTask = "GrabFailed";
                yield break;
            }

            m_grabbedItem = item;
            if (m_gripCenter != null)
            {
                m_gripToPickAlignOffsetWorld = m_gripCenter.position - item.PickAlignWorldPosition;
            }

            m_runtimeFromPose = CapturePoseFromDriver("SuctionHoldAtReach");
            LogPickPhase(item, "SuctionGrab", BuildGrabProbeJson(item));
            m_suctionGripper?.SetSuction(true, item);

            ReleasePickConveyorInteraction(item);
            LogPickPhase(item, "GrabSuccess", BuildJointJson(item));
            TaskProgress = 0.6f;
            m_battery.Consume(2f);
            LogPickPhase(item, "LiftHoldStart", null);
            CurrentTask = "LiftToZonePrep";
            yield return LiftHeldItemRoutine(item, 0.85f);
            TaskProgress = 1f;
            CurrentTask = "LiftHold";
            LogPickPhase(item, "LiftHoldEntered", BuildLiftSampleJson(item));

            if (m_pickLiftEvaluator == null)
            {
                m_pickLiftEvaluator = FindFirstObjectByType<PickLiftSuccessEvaluator>();
            }

            if (m_pickLiftEvaluator != null && m_pickRunId >= 0)
            {
                var beltSpeed = FindFirstObjectByType<SmartFactory.Conveyor.ConveyorBelt>()?.Speed ?? 0f;
                var contactAtGrab = m_suctionGripper != null ? m_suctionGripper.ContactSeconds : 0f;
                m_pickLiftEvaluator.BeginEvaluation(
                    m_pickRunId,
                    m_robotIndex,
                    item,
                    this,
                    null,
                    null,
                    contactAtGrab,
                    beltSpeed);
            }

            m_pickRunId = -1;
        }

        IEnumerator PickLiftAndPlaceRoutine(
            FactoryItem item,
            RobotArmPickSlot pickSlot,
            int pickRunId,
            ItemSortOutcome sortOutcome)
        {
            if (item == null)
            {
                yield break;
            }

            if (item.IsErrorLogistics)
            {
                sortOutcome = ItemSortOutcome.Error;
            }

            item.SetSortOutcome(sortOutcome, pipelineActive: true);
            yield return PickAndLiftRoutine(item, pickSlot, pickRunId);

            if (!CanProceedToPlaceAfterPick(item))
            {
                LogPlaceFailure(item, sortOutcome, null, "GripLost", "pick or lift did not complete");
                item.ClearRobotPipeline();
                item.AssignedArmIndex = -1;
                if (IsBusy || CurrentTask != "GrabFailed" && CurrentTask != "PickMissed")
                {
                    yield return MoveToPose("Idle", 0f);
                }

                IsBusy = false;
                if (CurrentTask != "GrabFailed" && CurrentTask != "PickMissed")
                {
                    CurrentTask = "PlaceFailed";
                }

                yield break;
            }

            EnsureZoneTargetsReady();

            if (m_zoneTargets == null || !m_zoneTargets.TryResolve(sortOutcome, out var zone))
            {
                LogPlaceFailure(item, sortOutcome, null, "PlaceFailed", "zone target missing");
                item.ClearRobotPipeline();
                yield return MoveToPose("LiftHold", 0.85f);
                yield return MoveToPose("Idle", 1f);
                IsBusy = false;
                CurrentTask = "PlaceFailed";
                yield break;
            }

            CurrentTask = "TransportToZone";
            Debug.Log(
                $"[RobotArm] TransportToZone item={item.ItemId} outcome={sortOutcome} zone={zone.name} arm={m_robotIndex}");

            var travel = RobotArmPlacementGeometry.ComputeTravelGripPoint(zone, carryClearanceY);
            var travelOk = false;
            yield return ReachGripWorldTarget(travel, placeReachTolerance, placeReachTimeoutSeconds, r => travelOk = r);
            if (!travelOk && poseSet != null && poseSet.TryGetPose("ApproachDrop", out _))
            {
                yield return MoveToPose("ApproachDrop", 0.7f);
                travelOk = IsItemSecuredForPlace(item);
            }

            if (!travelOk || !IsItemSecuredForPlace(item))
            {
                LogPlaceFailure(item, sortOutcome, zone, "PlaceTimeout", "travel reach failed");
                item.ClearRobotPipeline();
                yield return MoveToPose("Idle", 1f);
                IsBusy = false;
                CurrentTask = "PlaceFailed";
                yield break;
            }

            TaskProgress = 0.75f;
            CurrentTask = "DescendToZone";
            var place = RobotArmPlacementGeometry.ComputePlaceGripPoint(
                zone, item, m_gripToPickAlignOffsetWorld, zoneSurfaceYOffset);
            var placeOk = false;
            yield return ReachGripWorldTarget(place, placeReachTolerance, placeReachTimeoutSeconds, r => placeOk = r);
            if (!placeOk || !IsItemSecuredForPlace(item))
            {
                LogPlaceFailure(item, sortOutcome, zone, "PlaceTimeout", "place reach failed");
                item.ClearRobotPipeline();
                yield return MoveToPose("Idle", 1f);
                IsBusy = false;
                CurrentTask = "PlaceFailed";
                yield break;
            }

            if (GetGripCenterAlignDistance(place) > placeReachTolerance)
            {
                LogPlaceFailure(item, sortOutcome, zone, "PlaceFailed", "grip not at place point");
                item.ClearRobotPipeline();
                yield return MoveToPose("Idle", 1f);
                IsBusy = false;
                CurrentTask = "PlaceFailed";
                yield break;
            }

            CurrentTask = "ReleaseAtZone";
            ReleaseHeldItemAtZone(item, sortOutcome, zone);
            TaskProgress = 0.95f;
            m_battery.Consume(1f);

            CurrentTask = "Retreat";
            yield return MoveToPose("Idle", 1f);
            item.ClearRobotPipeline();
            item.AssignedArmIndex = -1;
            IsBusy = false;
            CurrentTask = "Completed";
            TaskProgress = 1f;
            Debug.Log(
                $"[RobotArm] Place complete item={item.ItemId} outcome={sortOutcome} zone={zone.name} arm={m_robotIndex}");
        }

        /// <summary>
        /// 벨트 X/Z 창 안이고 CenterOfTheGripper가 물류 좌표와 허용 오차 내 일치하는지 확인합니다.
        /// Gancho1·2 Collider 물리 충돌은 집게 닫힘 후 <see cref="TryGrab"/>에서 판정합니다.
        /// </summary>
        bool IsReadyToGrab(FactoryItem item, RobotArmPickSlot pickSlot)
        {
            if (item == null || pickSlot == null || m_gripCenter == null)
            {
                return false;
            }

            if (!pickSlot.IsItemInPickWindow(item))
            {
                return false;
            }

            return GetGripCenterAlignDistance(item.PickAlignWorldPosition) <= gripAlignTolerance;
        }

        /// <summary>
        /// 집기 시도 가능 여부. 흡착 장치가 있으면 근접 부착 거리까지 완화합니다.
        /// </summary>
        bool IsReadyForPickAttempt(FactoryItem item, RobotArmPickSlot pickSlot)
        {
            if (item == null || pickSlot == null || m_gripCenter == null)
            {
                return false;
            }

            if (!pickSlot.IsItemInPickWindow(item))
            {
                return false;
            }

            var maxDist = m_suctionGripper != null
                ? Mathf.Max(gripAlignTolerance, SuctionPickAlignMaxDistance)
                : gripAlignTolerance;
            return GetGripCenterAlignDistance(item.PickAlignWorldPosition) <= maxDist;
        }

        /// <summary>
        /// 물류가 집기 X/Z 창에 들어온 뒤에만 벨트 X 이동을 멈춥니다(lead 선행 시 창 밖에서 X 고정 방지).
        /// </summary>
        void TryEnterPickBeltStabilize(FactoryItem item, RobotArmPickSlot pickSlot)
        {
            if (item == null || pickSlot == null || item.IsPickBeltStabilized)
            {
                return;
            }

            if (pickSlot.IsItemInPickWindow(item))
            {
                item.SetPickBeltStabilized(true);
            }
        }

        /// <summary>
        /// 집기 창 안인데 정렬만 지연될 때 흡착 시도로 넘어갈지(근접 부착 거리 이내·최대 대기 초과).
        /// </summary>
        bool ShouldForceSuctionPickAttempt(FactoryItem item, RobotArmPickSlot pickSlot)
        {
            if (m_suctionGripper == null || item == null || pickSlot == null || m_gripCenter == null)
            {
                return false;
            }

            if (!pickSlot.IsItemInPickWindow(item))
            {
                return false;
            }

            var elapsed = Time.realtimeSinceStartup - m_pickAlignWaitStartRealtime;
            if (elapsed < SuctionAlignWaitMaxSeconds)
            {
                return false;
            }

            return GetGripCenterAlignDistance(item.PickAlignWorldPosition) <= SuctionPickAlignMaxDistance + 0.04f;
        }

        /// <summary>
        /// ReachPick 대기 중 DOF를 한 스텝씩 조정해 CenterOfTheGripper·물류 거리를 줄입니다.
        /// </summary>
        /// <param name="item">대상 물류.</param>
        /// <param name="withinDistance">
        /// 이미 이 거리( m ) 이내면 조정 생략. 음수면 <see cref="gripAlignTolerance"/> 사용.
        /// 집게 닫힘 직전에는 더 엄격한 값을 넘깁니다.
        /// </param>
        void AlignGripCenterTowardItem(FactoryItem item, float withinDistance = -1f)
        {
            if (item == null || m_gripCenter == null || m_driver == null || !m_driver.IsReady)
            {
                return;
            }

            // 흡착 후에는 물류가 Gancho1을 따라가므로 팔이 물류를 쫓지 않습니다.
            if (item.IsHeldByRobot || HasActiveGrabJoint(item))
            {
                return;
            }

            var target = item.PickAlignWorldPosition;
            var stopDist = withinDistance >= 0f ? withinDistance : gripAlignTolerance;
            if (GetGripCenterAlignDistance(target) <= stopDist)
            {
                return;
            }

            var dofs = new[]
            {
                RobotArmDof.PrincipalY,
                RobotArmDof.PrincipalZ,
                RobotArmDof.Brazo1Z,
                RobotArmDof.Brazo2Z,
                RobotArmDof.Brazo3Z
            };

            var carrying = item != null && item.IsHeldByRobot;
            var stepDeg = SelectAlignDofStepDegrees(GetGripCenterAlignDistance(target), carrying);

            foreach (var dof in dofs)
            {
                var cur = m_driver.GetDofAngle(dof);
                var bestDist = GetGripCenterAlignDistance(target);
                var bestAngle = cur;

                foreach (var delta in new[] { stepDeg, -stepDeg })
                {
                    m_driver.SetDofAngle(dof, cur + delta);
                    var trialDist = GetGripCenterAlignDistance(target);
                    if (trialDist < bestDist)
                    {
                        bestDist = trialDist;
                        bestAngle = cur + delta;
                    }
                }

                m_driver.SetDofAngle(dof, bestAngle);
            }

            if (carrying)
            {
                SyncGanchoPhysicsTransforms();
            }
        }

        /// <summary>
        /// 그립-목표 거리·운반 여부에 따른 DOF 정렬 스텝(deg). 운반 중에는 큰 스텝을 쓰지 않습니다.
        /// </summary>
        static float SelectAlignDofStepDegrees(float alignDistanceMeters, bool carryingHeldItem)
        {
            if (carryingHeldItem)
            {
                return HeldCarryAlignDofStepDegrees;
            }

            if (alignDistanceMeters > 0.22f)
            {
                return 8f;
            }

            if (alignDistanceMeters > 0.14f)
            {
                return 5f;
            }

            return AlignDofStepDegrees;
        }

        /// <summary>
        /// 한 프레임 안에서 hill-climb 정렬을 반복해 흡착 집기 대기 시간을 줄입니다.
        /// </summary>
        void BurstAlignGripTowardItem(FactoryItem item, float withinDistance, int maxIterations)
        {
            if (item == null || maxIterations <= 0)
            {
                return;
            }

            for (var i = 0; i < maxIterations; i++)
            {
                var before = GetGripCenterAlignDistance(item.PickAlignWorldPosition);
                if (before <= withinDistance)
                {
                    break;
                }

                AlignGripCenterTowardItem(item, withinDistance);
                var after = GetGripCenterAlignDistance(item.PickAlignWorldPosition);
                if (after >= before - 0.0005f)
                {
                    break;
                }
            }

            SyncGanchoPhysicsTransforms();
        }

        /// <summary>
        /// CenterOfTheGripper와 목표 월드 좌표(물류 pivot) 간 거리.
        /// </summary>
        float GetGripCenterAlignDistance(Vector3 itemWorldPos)
        {
            if (m_gripCenter == null)
            {
                return float.MaxValue;
            }

            return Vector3.Distance(m_gripCenter.position, itemWorldPos);
        }

        /// <summary>
        /// 집기 구간 벨트 안정화·Gancho-물류 충돌 무시를 해제합니다.
        /// </summary>
        void ReleasePickConveyorInteraction(FactoryItem item)
        {
            if (item == null)
            {
                return;
            }

            item.SetPickBeltStabilized(false);
            ResolveSuctionGripperIfNeeded();
            m_suctionGripper?.SetPickCollisionIgnore(item, false);
        }

        void StabilizeItemForGrip(FactoryItem item)
        {
            if (item != null && item.IsHeldByRobot)
            {
                return;
            }

            var rb = item.ItemRigidbody;
            if (rb == null)
            {
                return;
            }

            if (!item.IsPickBeltStabilized)
            {
                // 창 진입 전에는 벨트 X 이동을 유지합니다(안정화 지연과 함께 동작).
                return;
            }

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>Transform 기반 Gancho 회전 후 물리 Collider 위치를 동기화합니다.</summary>
        void SyncGanchoPhysicsTransforms()
        {
            Physics.SyncTransforms();
        }

        /// <summary>
        /// 프로브 물리 접촉 누적을 trace용 로컬 타이머에 반영합니다.
        /// </summary>
        void SyncGanchoCloseContactFromProbes(int itemId)
        {
            m_gancho1CloseContactSec = m_ganchoProbe1 != null ? m_ganchoProbe1.PeekContactSeconds(itemId) : 0f;
            m_gancho2CloseContactSec = m_ganchoProbe2 != null ? m_ganchoProbe2.PeekContactSeconds(itemId) : 0f;
        }

        /// <summary>
        /// Gancho1·2 Collider가 모두 물류 본체와 물리 충돌 중이고, 양쪽 접촉 유지 시간을 충족했는지 확인합니다.
        /// </summary>
        bool HasDualGanchoContactForGrab(FactoryItem item)
        {
            if (item == null || m_ganchoProbe1 == null || m_ganchoProbe2 == null)
            {
                return false;
            }

            var itemId = item.ItemId;
            var g1Sec = m_ganchoProbe1.PeekContactSeconds(itemId);
            var g2Sec = m_ganchoProbe2.PeekContactSeconds(itemId);
            if (g1Sec < minGanchoContactSecondsForGrab || g2Sec < minGanchoContactSecondsForGrab)
            {
                return false;
            }

            if (!m_ganchoProbe1.IsBodyContactingItem(itemId) ||
                !m_ganchoProbe2.IsBodyContactingItem(itemId))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 양 집게 물리 접촉 유지 중 짧은 쪽(초).
        /// </summary>
        float GetDualGanchoContactSecondsForGrab(int itemId)
        {
            var g1 = m_ganchoProbe1 != null ? m_ganchoProbe1.PeekContactSeconds(itemId) : 0f;
            var g2 = m_ganchoProbe2 != null ? m_ganchoProbe2.PeekContactSeconds(itemId) : 0f;
            return Mathf.Min(g1, g2);
        }

        /// <summary>
        /// FixedJoint로 물류를 잡고 성공 여부를 반환합니다.
        /// </summary>
        bool TryGrab(FactoryItem item)
        {
            if (item == null || m_gripCenter == null)
            {
                return false;
            }

            var itemRb = item.ItemRigidbody;
            if (itemRb == null)
            {
                return false;
            }

            if (!HasDualGanchoContactForGrab(item))
            {
                return false;
            }

            var dist = GetGripCenterAlignDistance(item.PickAlignWorldPosition);
            if (dist > gripAlignTolerance)
            {
                return false;
            }

            m_grabbedItem = item;
            if (m_grabJoint != null)
            {
                Destroy(m_grabJoint);
            }

            EnsureGrabJointHost();
            var jointHost = m_driver != null && m_driver.Rig.Brazo3 != null
                ? m_driver.Rig.Brazo3.gameObject
                : m_gripCenter.gameObject;
            m_grabJoint = jointHost.AddComponent<FixedJoint>();
            m_grabJoint.connectedBody = itemRb;
            m_grabJoint.breakForce = Mathf.Infinity;

            if (m_grabJoint.connectedBody != itemRb)
            {
                Destroy(m_grabJoint);
                m_grabJoint = null;
                m_grabbedItem = null;
                return false;
            }

            item.IsHeldByRobot = true;
            itemRb.linearVelocity = Vector3.zero;
            itemRb.angularVelocity = Vector3.zero;
            m_gripToPickAlignOffsetWorld = m_gripCenter.position - item.PickAlignWorldPosition;
            return true;
        }

        /// <summary>
        /// 집게 닫힘 포즈 이름(GripClosedAtReach 우선).
        /// </summary>
        string ResolveGripClosedPoseName()
        {
            if (poseSet != null && poseSet.TryGetPose("GripClosedAtReach", out _))
            {
                return "GripClosedAtReach";
            }

            return "GripClosed";
        }

        /// <summary>
        /// 집게 닫힘 목표 Gancho 각도(포즈 또는 바인딩 limit)를 해석합니다.
        /// </summary>
        bool TryResolveClosedGanchoTargets(out float gancho1TargetZ, out float gancho2TargetZ, out bool hasNamedPose)
        {
            gancho1TargetZ = -50f;
            gancho2TargetZ = 50f;
            hasNamedPose = false;
            if (poseSet == null)
            {
                if (m_driver != null && m_driver.IsReady)
                {
                    m_driver.GetDofLimits(RobotArmDof.Gancho1Z, out gancho1TargetZ, out _);
                    m_driver.GetDofLimits(RobotArmDof.Gancho2Z, out _, out gancho2TargetZ);
                }

                return false;
            }

            poseSet.EnsureDefaultPoses();
            var poseName = ResolveGripClosedPoseName();
            if (poseSet.TryGetPose(poseName, out var closed))
            {
                gancho1TargetZ = closed.gancho1Z;
                gancho2TargetZ = closed.gancho2Z;
                hasNamedPose = true;
                return true;
            }

            if (m_driver != null && m_driver.IsReady)
            {
                m_driver.GetDofLimits(RobotArmDof.Gancho1Z, out gancho1TargetZ, out _);
                m_driver.GetDofLimits(RobotArmDof.Gancho2Z, out _, out gancho2TargetZ);
            }

            return false;
        }

        /// <summary>
        /// trace용 Gancho DOF·닫힘 limit·포즈 존재 여부 JSON.
        /// </summary>
        string BuildGripCloseGanchoSampleJson()
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return "{\"driverReady\":false}";
            }

            var g1 = m_driver.GetDofAngle(RobotArmDof.Gancho1Z);
            var g2 = m_driver.GetDofAngle(RobotArmDof.Gancho2Z);
            TryResolveClosedGanchoTargets(out _, out _, out var hasPose);
            return "{\"gancho1Z\":" + F(g1) + ",\"gancho2Z\":" + F(g2) +
                   ",\"atCloseLimit1\":" + (IsGancho1AtCloseLimit() ? "true" : "false") +
                   ",\"atCloseLimit2\":" + (IsGancho2AtCloseLimit() ? "true" : "false") +
                   ",\"poseSetHasGripClosed\":" + (hasPose ? "true" : "false") + ",\"driverReady\":true}";
        }

        void LogGripCloseGanchoSample(FactoryItem item)
        {
            LogPickPhase(item, "GripCloseGanchoSample", BuildGripCloseGanchoSampleJson());
        }

        /// <summary>
        /// ArmLock 유지하며 Gancho1·2만 닫힘 포즈로 보간합니다(로봇팔_움직임.md §5.1, Update).
        /// </summary>
        IEnumerator BlendGanchosClosedForPick(FactoryItem item, RobotArmPose armLock, float progressMarker)
        {
            if (item == null || m_driver == null || !m_driver.IsReady || armLock == null)
            {
                yield break;
            }

            var fromG1 = m_driver.GetDofAngle(RobotArmDof.Gancho1Z);
            var fromG2 = m_driver.GetDofAngle(RobotArmDof.Gancho2Z);
            TryResolveClosedGanchoTargets(out var targetG1, out var targetG2, out var hasNamedPose);
            if (!hasNamedPose)
            {
                Debug.LogWarning(
                    $"{name}: GripClosed 포즈 없음 — 바인딩 limit로 Gancho 닫힘 보간 item={(item != null ? item.ItemId : -1)}");
            }

            var duration = Mathf.Max(0.05f, poseBlendDuration);
            var elapsed = 0f;
            var sampleElapsed = 0f;
            while (elapsed < duration)
            {
                if (m_paused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                sampleElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                ApplyArmLockPose(armLock);
                m_driver.SetDofAngle(RobotArmDof.Gancho1Z, Mathf.Lerp(fromG1, targetG1, t));
                m_driver.SetDofAngle(RobotArmDof.Gancho2Z, Mathf.Lerp(fromG2, targetG2, t));
                SyncGanchoPhysicsTransforms();
                StabilizeItemForGrip(item);
                TaskProgress = progressMarker;

                if (sampleElapsed >= GripCloseGanchoSampleIntervalSeconds)
                {
                    sampleElapsed = 0f;
                    LogGripCloseGanchoSample(item);
                }

                yield return null;
            }

            ApplyArmLockPose(armLock);
            m_driver.SetDofAngle(RobotArmDof.Gancho1Z, targetG1);
            m_driver.SetDofAngle(RobotArmDof.Gancho2Z, targetG2);
            SyncGanchoPhysicsTransforms();
            StabilizeItemForGrip(item);
            m_runtimeFromPose = CapturePoseFromDriver("GripClosedAtReach");

            var toG1 = m_driver.GetDofAngle(RobotArmDof.Gancho1Z);
            var toG2 = m_driver.GetDofAngle(RobotArmDof.Gancho2Z);
            var motionSum = Mathf.Abs(toG1 - fromG1) + Mathf.Abs(toG2 - fromG2);
            LogPickPhase(
                item,
                "GripCloseBlendEnd",
                BuildGripCloseGanchoSampleJson() + ",\"motionSumDeg\":" + F(motionSum));
            if (motionSum < MinGanchoCloseMotionDegreesSum)
            {
                Debug.LogWarning(
                    $"{name}: GanchoCloseNoMotion item={item.ItemId} motionSumDeg={motionSum:0.###} from=({fromG1:0.###},{fromG2:0.###}) to=({toG1:0.###},{toG2:0.###})");
                LogPickPhase(
                    item,
                    "GanchoCloseNoMotion",
                    "{\"motionSumDeg\":" + F(motionSum) + ",\"fromG1\":" + F(fromG1) + ",\"fromG2\":" + F(fromG2) +
                    ",\"toG1\":" + F(toG1) + ",\"toG2\":" + F(toG2) + "}");
            }
        }

        /// <summary>
        /// 목표 닫힘 각도까지 StepGancho가 필요한지 판정합니다.
        /// </summary>
        bool NeedsGancho1StepTowardCloseTarget(float targetG1Z)
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return false;
            }

            return Mathf.Abs(m_driver.GetDofAngle(RobotArmDof.Gancho1Z) - targetG1Z) >= ganchoCloseStepDegrees * 0.5f;
        }

        /// <summary>
        /// 목표 닫힘 각도까지 StepGancho가 필요한지 판정합니다.
        /// </summary>
        bool NeedsGancho2StepTowardCloseTarget(float targetG2Z)
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return false;
            }

            return Mathf.Abs(m_driver.GetDofAngle(RobotArmDof.Gancho2Z) - targetG2Z) >= ganchoCloseStepDegrees * 0.5f;
        }

        /// <summary>
        /// 집게 닫힘 직전 Gancho1·2를 ReachPick(열림) 각도로 맞춥니다.
        /// </summary>
        void EnsureGanchosOpenForPickClose()
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return;
            }

            if (poseSet != null && poseSet.TryGetPose("ReachPick", out var reach))
            {
                m_driver.SetDofAngle(RobotArmDof.Gancho1Z, reach.gancho1Z);
                m_driver.SetDofAngle(RobotArmDof.Gancho2Z, reach.gancho2Z);
                return;
            }

            m_driver.GetDofLimits(RobotArmDof.Gancho1Z, out _, out var g1Open);
            m_driver.GetDofLimits(RobotArmDof.Gancho2Z, out var g2Open, out _);
            m_driver.SetDofAngle(RobotArmDof.Gancho1Z, g1Open);
            m_driver.SetDofAngle(RobotArmDof.Gancho2Z, g2Open);
        }

        /// <summary>
        /// 집게 닫힘 중 팔 DOF만 고정합니다(Gancho 제외, 회전만).
        /// </summary>
        void ApplyArmLockPose(RobotArmPose armLock)
        {
            if (armLock == null || m_driver == null || !m_driver.IsReady)
            {
                return;
            }

            m_driver.SetDofAngle(RobotArmDof.PrincipalY, armLock.principalY);
            m_driver.SetDofAngle(RobotArmDof.PrincipalZ, armLock.principalZ);
            m_driver.SetDofAngle(RobotArmDof.Brazo1Z, armLock.brazo1Z);
            m_driver.SetDofAngle(RobotArmDof.Brazo2Z, armLock.brazo2Z);
            m_driver.SetDofAngle(RobotArmDof.Brazo3Z, armLock.brazo3Z);
        }

        /// <summary>Gancho1이 닫힘 각도 하한에 도달했는지 확인합니다.</summary>
        bool IsGancho1AtCloseLimit()
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return false;
            }

            m_driver.GetDofLimits(RobotArmDof.Gancho1Z, out var min, out _);
            return m_driver.GetDofAngle(RobotArmDof.Gancho1Z) <= min + 0.01f;
        }

        /// <summary>Gancho2가 닫힘 각도 상한에 도달했는지 확인합니다.</summary>
        bool IsGancho2AtCloseLimit()
        {
            if (m_driver == null || !m_driver.IsReady)
            {
                return false;
            }

            m_driver.GetDofLimits(RobotArmDof.Gancho2Z, out _, out var max);
            return m_driver.GetDofAngle(RobotArmDof.Gancho2Z) >= max - 0.01f;
        }

        /// <summary>
        /// 흡착 On 후 Gancho1 접촉·부착이 완료될 때까지 정렬을 유지합니다.
        /// </summary>
        IEnumerator WaitSuctionAttachRoutine(
            FactoryItem item,
            RobotArmPickSlot pickSlot,
            System.Action<bool> onComplete)
        {
            if (item == null || m_suctionGripper == null)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            LogPickPhase(item, "SuctionOnStart", null);
            m_suctionGripper.ResetContactTracking();
            m_suctionGripper.SetSuction(true, item);

            var deadline = Time.time + ganchoCloseTimeoutSeconds;
            while (!m_suctionGripper.IsHolding(item))
            {
                if (pickSlot != null && pickSlot.HasItemPassedPickLine(item))
                {
                    m_suctionGripper.SetSuction(false);
                    onComplete?.Invoke(false);
                    yield break;
                }

                if (Time.time > deadline)
                {
                    break;
                }

                StabilizeItemForGrip(item);
                AlignGripCenterTowardItem(item, GripCloseAlignMaxDistance);
                SyncGanchoPhysicsTransforms();
                yield return null;
            }

            onComplete?.Invoke(m_suctionGripper.IsHolding(item));
        }

        /// <summary>
        /// ArmLock 유지하며 Gancho를 닫힘 한계까지 회전하고, 양쪽 Collider 물리 충돌(AND) 유지 시간을 충족할 때까지 대기합니다.
        /// </summary>
        IEnumerator WaitDualGanchoContactForGrab(
            FactoryItem item,
            RobotArmPose armLock,
            System.Action<bool> onComplete)
        {
            if (item == null || m_driver == null || !m_driver.IsReady)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            m_gancho1CloseContactSec = 0f;
            m_gancho2CloseContactSec = 0f;
            m_ganchoProbe1?.ResetTracking(item.ItemId);
            m_ganchoProbe2?.ResetTracking(item.ItemId);

            TryResolveClosedGanchoTargets(out var targetG1Z, out var targetG2Z, out _);
            var elapsed = 0f;
            var sampleElapsed = 0f;
            while (elapsed < ganchoCloseTimeoutSeconds)
            {
                if (m_paused)
                {
                    yield return null;
                    continue;
                }

                StabilizeItemForGrip(item);

                if (GetGripCenterAlignDistance(item.PickAlignWorldPosition) > gripAlignTolerance)
                {
                    AlignGripCenterTowardItem(item);
                    armLock = CapturePoseFromDriver("GripCloseArmLock");
                }

                if (NeedsGancho1StepTowardCloseTarget(targetG1Z))
                {
                    StepGancho1TowardClose();
                }

                if (NeedsGancho2StepTowardCloseTarget(targetG2Z))
                {
                    StepGancho2TowardClose();
                }

                ApplyArmLockPose(armLock);
                SyncGanchoPhysicsTransforms();
                SyncGanchoCloseContactFromProbes(item.ItemId);

                sampleElapsed += Time.fixedDeltaTime;
                if (sampleElapsed >= GripCloseGanchoSampleIntervalSeconds)
                {
                    sampleElapsed = 0f;
                    LogGripCloseGanchoSample(item);
                }

                if (HasDualGanchoContactForGrab(item))
                {
                    onComplete?.Invoke(true);
                    yield break;
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            SyncGanchoCloseContactFromProbes(item.ItemId);
            onComplete?.Invoke(HasDualGanchoContactForGrab(item));
        }

        /// <summary>Gancho1 집게를 닫힘 방향(+Z 쪽, gancho1Z 감소)으로 한 스텝 회전합니다.</summary>
        void StepGancho1TowardClose()
        {
            m_driver.GetDofLimits(RobotArmDof.Gancho1Z, out var min, out _);
            var cur = m_driver.GetDofAngle(RobotArmDof.Gancho1Z);
            m_driver.SetDofAngle(RobotArmDof.Gancho1Z, Mathf.Max(cur - ganchoCloseStepDegrees, min));
        }

        /// <summary>Gancho2 집게를 닫힘 방향(−Z 쪽, gancho2Z 증가)으로 한 스텝 회전합니다.</summary>
        void StepGancho2TowardClose()
        {
            m_driver.GetDofLimits(RobotArmDof.Gancho2Z, out _, out var max);
            var cur = m_driver.GetDofAngle(RobotArmDof.Gancho2Z);
            m_driver.SetDofAngle(RobotArmDof.Gancho2Z, Mathf.Min(cur + ganchoCloseStepDegrees, max));
        }

        /// <summary>
        /// FixedJoint 유지 상태에서 grip center를 위로 올립니다( nominal LiftHold <see cref="MoveToPose"/>는 집기 DOF를 덮어씀).
        /// </summary>
        IEnumerator LiftHeldItemRoutine(FactoryItem item, float progressMarker)
        {
            if (item == null || m_gripCenter == null || !IsItemSecuredForPlace(item))
            {
                yield break;
            }

            ResolveSuctionGripperIfNeeded();
            m_suctionGripper?.SetSuction(true, item);

            if (m_suctionGripper != null && m_suctionGripper.IsHolding(item) && poseSet != null &&
                poseSet.TryGetPose("LiftHold", out _))
            {
                yield return MoveToPose("LiftHold", progressMarker);
                m_runtimeFromPose = CapturePoseFromDriver("LiftHold");
                yield break;
            }

            var liftTarget = m_gripCenter.position + Vector3.up * carryClearanceY;
            yield return ReachGripWorldTarget(
                liftTarget,
                gripAlignTolerance,
                placeReachTimeoutSeconds,
                _ => { });

            m_runtimeFromPose = CapturePoseFromDriver("LiftHold");
            TaskProgress = progressMarker;
        }

        IEnumerator ReachGripWorldTarget(
            Vector3 worldTarget,
            float tolerance,
            float timeoutSeconds,
            System.Action<bool> onComplete)
        {
            if (m_gripCenter == null || m_driver == null || !m_driver.IsReady)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (m_paused)
                {
                    yield return null;
                    continue;
                }

                var dist = GetGripCenterAlignDistance(worldTarget);
                if (dist <= tolerance)
                {
                    onComplete?.Invoke(true);
                    yield break;
                }

                AlignGripCenterTowardWorldPoint(worldTarget);
                elapsed += Time.deltaTime;
                yield return null;
            }

            onComplete?.Invoke(GetGripCenterAlignDistance(worldTarget) <= tolerance);
        }

        void AlignGripCenterTowardWorldPoint(Vector3 worldTarget)
        {
            if (m_gripCenter == null || m_driver == null || !m_driver.IsReady)
            {
                return;
            }

            if (GetGripCenterAlignDistance(worldTarget) <= gripAlignTolerance)
            {
                return;
            }

            var dofs = new[]
            {
                RobotArmDof.PrincipalY,
                RobotArmDof.PrincipalZ,
                RobotArmDof.Brazo1Z,
                RobotArmDof.Brazo2Z,
                RobotArmDof.Brazo3Z
            };

            var carrying = m_grabbedItem != null && IsItemSecuredForPlace(m_grabbedItem);
            var stepDeg = SelectAlignDofStepDegrees(GetGripCenterAlignDistance(worldTarget), carrying);

            foreach (var dof in dofs)
            {
                var cur = m_driver.GetDofAngle(dof);
                var bestDist = GetGripCenterAlignDistance(worldTarget);
                var bestAngle = cur;

                foreach (var delta in new[] { stepDeg, -stepDeg })
                {
                    m_driver.SetDofAngle(dof, cur + delta);
                    var trialDist = GetGripCenterAlignDistance(worldTarget);
                    if (trialDist < bestDist)
                    {
                        bestDist = trialDist;
                        bestAngle = cur + delta;
                    }
                }

                m_driver.SetDofAngle(dof, bestAngle);
            }

            if (carrying)
            {
                SyncGanchoPhysicsTransforms();
            }
        }

        void ReleaseHeldItemAtZone(FactoryItem item, ItemSortOutcome outcome, Transform zone)
        {
            m_suctionGripper?.SetSuction(false);

            if (m_grabJoint != null)
            {
                Destroy(m_grabJoint);
                m_grabJoint = null;
            }

            if (item == null)
            {
                m_grabbedItem = null;
                return;
            }

            item.IsHeldByRobot = false;
            m_grabbedItem = null;
            item.ClearRobotPipeline();
            item.RestoreWorldPhysics();

            if (outcome == ItemSortOutcome.Fail || outcome == ItemSortOutcome.Error)
            {
                item.SuppressUnprocessedStatIncrement = true;
            }

            SmartFactory.Logistics.FactoryLogisticsCenter.Instance?.NotifySortComplete(item);
            item.ScheduleZoneDespawn(m_itemSpawner);
        }

        void LogPlaceFailure(
            FactoryItem item,
            ItemSortOutcome outcome,
            Transform zone,
            string phase,
            string reason)
        {
            var zoneName = zone != null ? zone.name : "null";
            Debug.LogWarning(
                $"[RobotArm] {phase} item={item?.ItemId} outcome={outcome} zone={zoneName} arm={m_robotIndex} reason={reason}");
            if (item == null)
            {
                return;
            }

            PickLiftTrace.LogPhase(
                0,
                m_robotIndex,
                item.ItemId,
                phase,
                "{\"reason\":\"" + reason + "\",\"outcome\":\"" + outcome + "\",\"zone\":\"" + zoneName + "\"}");
        }


        void MaybeLogAlignSample(FactoryItem item, RobotArmPickSlot pickSlot)
        {
            if (m_pickRunId < 0)
            {
                return;
            }

            m_alignSampleFrame++;
            if (m_alignSampleFrame % 10 != 0)
            {
                return;
            }

            var alignDist = GetGripCenterAlignDistance(item.PickAlignWorldPosition);
            var itemPos = item.PickAlignWorldPosition;
            var grip = m_gripCenter.position;
            var pickDx = itemPos.x - pickSlot.PickAnchorX;
            PickLiftTrace.LogPhase(
                m_pickRunId,
                m_robotIndex,
                item.ItemId,
                "AlignSample",
                "{\"alignDist\":" + alignDist + ",\"gripCenter\":[" + F(grip.x) + "," + F(grip.y) + "," + F(grip.z) +
                "],\"itemPos\":[" + F(itemPos.x) + "," + F(itemPos.y) + "," + F(itemPos.z) + "],\"pickDx\":" +
                pickDx + ",\"gancho1Contact\":" + (m_ganchoProbe1 != null ? m_ganchoProbe1.PeekContactSeconds(item.ItemId) : 0f) +
                ",\"gancho2Contact\":" + (m_ganchoProbe2 != null ? m_ganchoProbe2.PeekContactSeconds(item.ItemId) : 0f) + "}");
        }

        void LogPickPhase(FactoryItem item, string phase, string innerJson)
        {
            if (m_pickRunId < 0 || item == null)
            {
                return;
            }

            var payload = innerJson ?? BuildLiftSampleJson(item);
            PickLiftTrace.LogPhase(m_pickRunId, m_robotIndex, item.ItemId, phase, payload);
        }

        string BuildGrabProbeJson(FactoryItem item)
        {
            var itemPos = item.PickAlignWorldPosition;
            return "{\"gancho1ContactSec\":" + m_gancho1CloseContactSec + ",\"gancho2ContactSec\":" +
                   m_gancho2CloseContactSec + ",\"itemPos\":[" + F(itemPos.x) + "," + F(itemPos.y) + "," +
                   F(itemPos.z) + "]}";
        }

        string BuildJointJson(FactoryItem item)
        {
            if (m_suctionGripper != null && m_suctionGripper.IsHolding(item))
            {
                return "{\"suctionHold\":true,\"contactSec\":" + m_suctionGripper.ContactSeconds + "}";
            }

            var connected = m_grabJoint != null && m_grabJoint.connectedBody == item.ItemRigidbody;
            var bodyName = m_grabJoint != null && m_grabJoint.connectedBody != null
                ? m_grabJoint.connectedBody.name
                : "";
            return "{\"jointCreated\":" + (m_grabJoint != null ? "true" : "false") + ",\"jointConnectedBody\":\"" +
                   bodyName + "\",\"jointConnectedOk\":" + (connected ? "true" : "false") + "}";
        }

        string BuildLiftSampleJson(FactoryItem item)
        {
            var itemPos = item.PickAlignWorldPosition;
            var grip = m_gripCenter != null ? m_gripCenter.position : Vector3.zero;
            var rel = grip - itemPos;
            var rb = item.ItemRigidbody;
            var velX = rb != null ? rb.linearVelocity.x : 0f;
            return "{\"itemY\":" + F(itemPos.y) + ",\"gripY\":" + F(grip.y) + ",\"relativeGripToItem\":[" +
                   F(rel.x) + "," + F(rel.y) + "," + F(rel.z) + "],\"itemVelX\":" + velX + "}";
        }

        static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        IEnumerator SortRoutine(FactoryItem item, Transform dropTarget, bool isMismatch, ItemSpawner spawner)
        {
            if (m_paused || item == null || dropTarget == null || endEffector == null || m_driver == null || !m_driver.IsReady)
            {
                yield break;
            }

            if (poseSet == null)
            {
                Debug.LogWarning($"{name}: RobotArmPoseSet이 없습니다.");
                yield break;
            }

            poseSet.EnsureDefaultPoses();
            IsBusy = true;
            TaskProgress = 0f;
            CurrentTask = isMismatch ? "MismatchSort" : "NormalSort";

            yield return MoveToPose("ApproachPick", 0.2f);
            yield return MoveToPose("GripClosed", 0.35f);
            Grab(item);
            TaskProgress = 0.45f;
            m_battery.Consume(2f);

            yield return MoveToPose("ApproachDrop", 0.7f);
            yield return MoveToPose("GripOpen", 0.85f);
            Release(item, spawner);
            TaskProgress = 1f;
            CurrentTask = "Completed";
            m_battery.Consume(1f);

            yield return MoveToPose("Idle", 1f);
            IsBusy = false;
            CurrentTask = "Idle";
        }

        IEnumerator MoveToPose(
            string poseName,
            float progressMarker,
            FactoryItem alignItemDuringBlend = null,
            RobotArmPose armLockDuringBlend = null,
            float blendDurationSeconds = -1f)
        {
            if (!poseSet.TryGetPose(poseName, out var targetPose))
            {
                Debug.LogWarning($"{name}: 포즈 '{poseName}' 없음");
                yield break;
            }

            var maintainArmAlign = alignItemDuringBlend != null;
            var lockArmDuringGripClose = maintainArmAlign && armLockDuringBlend != null;
            var from = maintainArmAlign ? CapturePoseFromDriver(poseName) : m_runtimeFromPose ?? targetPose;
            var duration = blendDurationSeconds > 0f
                ? Mathf.Max(0.05f, blendDurationSeconds)
                : Mathf.Max(0.05f, poseBlendDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (m_paused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                var t = elapsed / duration;
                if (maintainArmAlign)
                {
                    if (lockArmDuringGripClose)
                    {
                        ApplyArmLockPose(armLockDuringBlend);
                    }
                    else
                    {
                        m_driver.BlendPoses(from, targetPose, t);
                    }

                    m_driver.SetDofAngle(
                        RobotArmDof.Gancho1Z,
                        Mathf.Lerp(from.gancho1Z, targetPose.gancho1Z, t));
                    m_driver.SetDofAngle(
                        RobotArmDof.Gancho2Z,
                        Mathf.Lerp(from.gancho2Z, targetPose.gancho2Z, t));
                    StabilizeItemForGrip(alignItemDuringBlend);
                    if (!lockArmDuringGripClose)
                    {
                        AlignGripCenterTowardItem(alignItemDuringBlend);
                    }
                }
                else
                {
                    m_driver.BlendPoses(from, targetPose, t);
                }

                TaskProgress = progressMarker;
                yield return null;
            }

            if (maintainArmAlign)
            {
                if (lockArmDuringGripClose)
                {
                    ApplyArmLockPose(armLockDuringBlend);
                }

                m_driver.SetDofAngle(RobotArmDof.Gancho1Z, targetPose.gancho1Z);
                m_driver.SetDofAngle(RobotArmDof.Gancho2Z, targetPose.gancho2Z);
                StabilizeItemForGrip(alignItemDuringBlend);
                if (!lockArmDuringGripClose)
                {
                    AlignGripCenterTowardItem(alignItemDuringBlend);
                }

                m_runtimeFromPose = CapturePoseFromDriver(poseName);
            }
            else
            {
                m_driver.ApplyPose(targetPose);
                m_runtimeFromPose = ClonePose(targetPose);
            }
        }

        void Grab(FactoryItem item)
        {
            m_grabbedItem = item;
            if (m_grabJoint != null)
            {
                Destroy(m_grabJoint);
            }

            m_grabJoint = endEffector.gameObject.AddComponent<FixedJoint>();
            m_grabJoint.connectedBody = item.GetComponent<Rigidbody>();
            m_grabJoint.breakForce = Mathf.Infinity;
        }

        void Release(FactoryItem item, ItemSpawner spawner)
        {
            if (m_grabJoint != null)
            {
                Destroy(m_grabJoint);
                m_grabJoint = null;
            }

            if (item != null)
            {
                spawner?.Despawn(item);
            }

            m_grabbedItem = null;
        }

        void PublishStatus()
        {
            if (FactoryStateRegistry.Instance == null || endEffector == null || m_driver == null || !m_driver.IsReady)
            {
                return;
            }

            var jointAngles = m_driver.GetJointAnglesForStatus();
            var pos = endEffector.position;
            var rot = endEffector.rotation.eulerAngles;
            FactoryStateRegistry.Instance.UpdateRobot(m_robotIndex, new RobotArmStatus
            {
                id = $"RobotArm_{m_robotIndex + 1}",
                currentState = m_paused ? "Paused" : (IsBusy ? "Working" : "Idle"),
                currentTask = CurrentTask,
                taskProgress = TaskProgress,
                completionState = TaskProgress >= 1f ? "Done" : "InProgress",
                batteryPercent = m_battery.Percent,
                isNormal = string.IsNullOrEmpty(m_error),
                hasBug = !string.IsNullOrEmpty(m_error),
                errorDetail = m_error,
                jointAnglesDeg = jointAngles,
                endEffectorPosition = new[] { pos.x, pos.y, pos.z },
                endEffectorRotationEuler = new[] { rot.x, rot.y, rot.z }
            });
        }

        static RobotArmPose ClonePose(RobotArmPose source)
        {
            return new RobotArmPose
            {
                poseName = source.poseName,
                principalY = source.principalY,
                principalZ = source.principalZ,
                brazo1Z = source.brazo1Z,
                brazo2Z = source.brazo2Z,
                brazo3Z = source.brazo3Z,
                gancho1Z = source.gancho1Z,
                gancho2Z = source.gancho2Z
            };
        }

        /// <summary>
        /// 정렬 후 hill-climb DOF를 포즈 스냅샷으로 저장합니다(집게 닫힘 시 ApplyPose로 되돌리지 않음).
        /// </summary>
        RobotArmPose CapturePoseFromDriver(string poseName)
        {
            return new RobotArmPose
            {
                poseName = poseName,
                principalY = m_driver.GetDofAngle(RobotArmDof.PrincipalY),
                principalZ = m_driver.GetDofAngle(RobotArmDof.PrincipalZ),
                brazo1Z = m_driver.GetDofAngle(RobotArmDof.Brazo1Z),
                brazo2Z = m_driver.GetDofAngle(RobotArmDof.Brazo2Z),
                brazo3Z = m_driver.GetDofAngle(RobotArmDof.Brazo3Z),
                gancho1Z = m_driver.GetDofAngle(RobotArmDof.Gancho1Z),
                gancho2Z = m_driver.GetDofAngle(RobotArmDof.Gancho2Z)
            };
        }

        /// <summary>
        /// 프리팹에서 로봇팔 인스턴스를 생성합니다.
        /// </summary>
        public static RobotArmController CreateFromPrefab(
            Transform parent,
            GameObject prefab,
            Vector3 localPosition,
            int robotIndex,
            RobotArmRigBinding bindingOverride,
            RobotArmPoseSet poseSetOverride)
        {
            var instance = Object.Instantiate(prefab, parent);
            instance.name = $"RobotArm_{robotIndex + 1}";
            instance.transform.localPosition = localPosition;

            var controller = instance.GetComponent<RobotArmController>();
            if (controller == null)
            {
                controller = instance.AddComponent<RobotArmController>();
            }

            if (poseSetOverride != null)
            {
                controller.SetPoseSet(poseSetOverride);
            }

            var binding = bindingOverride != null ? bindingOverride : controller.RigBinding;
            if (binding != null)
            {
                instance.transform.localPosition += binding.localPositionOffset;
                instance.transform.localEulerAngles += binding.localEulerOffset;
                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, binding.localScale);
                controller.SetupFromBinding(binding);
            }

            controller.Configure(robotIndex);
            var zoneTargets = instance.GetComponent<RobotArmZoneTargets>();
            if (zoneTargets != null)
            {
                controller.SetZoneTargets(zoneTargets);
            }

            return controller;
        }

        /// <summary>
        /// 런타임에 간이 로봇팔 계층을 생성합니다(프리팹 없을 때 폴백).
        /// </summary>
        public static RobotArmController CreateRuntimeArm(
            Transform parent,
            Vector3 localPosition,
            int robotIndex,
            RobotArmPoseSet poseSetOverride)
        {
            var root = new GameObject($"RobotArm_{robotIndex + 1}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var model = new GameObject("Robotic-Arm-Rig");
            model.transform.SetParent(root.transform, false);

            Transform previous = model.transform;
            var principal = CreateNamedJoint(model.transform, "Principal", ref previous);
            var b1 = CreateNamedJoint(principal, "Brazo1", ref previous);
            var b2 = CreateNamedJoint(b1, "Brazo2", ref previous);
            var b3 = CreateNamedJoint(b2, "Brazo3", ref previous);
            CreateChildJoint(b3, "Gancho1", new Vector3(0f, 0.1f, 0.05f));
            CreateChildJoint(b3, "Gancho2", new Vector3(0f, 0.1f, -0.05f));
            var piston = new GameObject("Piston").transform;
            piston.SetParent(principal, false);
            piston.localPosition = new Vector3(0f, 0.25f, 0f);
            var effector = new GameObject("Piston_end").transform;
            effector.SetParent(piston, false);
            effector.localPosition = new Vector3(0f, 0.15f, 0f);

            var controller = root.AddComponent<RobotArmController>();
            controller.modelRoot = model.transform;
            controller.endEffector = effector;
            if (poseSetOverride != null)
            {
                controller.SetPoseSet(poseSetOverride);
            }

            var binding = ScriptableObject.CreateInstance<RobotArmRigBinding>();
            controller.SetupFromBinding(binding);
            controller.Configure(robotIndex);
            return controller;
        }

        static Transform CreateChildJoint(Transform parent, string name, Vector3 localPos)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPos;
            return joint;
        }

        static Transform CreateNamedJoint(Transform parent, string name, ref Transform previous)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = previous == parent ? Vector3.zero : new Vector3(0f, 0.2f, 0f);
            previous = joint;
            return joint;
        }
    }
}
