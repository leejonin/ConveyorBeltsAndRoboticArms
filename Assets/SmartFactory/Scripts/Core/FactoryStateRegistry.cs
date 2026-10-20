using System;
using System.Collections.Generic;
using SmartFactory.Logistics;
using UnityEngine;

namespace SmartFactory.Core
{
    /// <summary>
    /// 장비·분류·중앙뇌 상태를 집계합니다. 제어판과 중앙뇌가 동일 스냅샷을 참조합니다.
    /// </summary>
    public class FactoryStateRegistry : MonoBehaviour
    {
        public static FactoryStateRegistry Instance { get; private set; }

        readonly List<RobotArmStatus> m_robots = new List<RobotArmStatus>();
        readonly List<NotificationEntry> m_notifications = new List<NotificationEntry>();

        int m_consecutiveErrorAlertThreshold = 5;
        float m_lastConsecutiveErrorAlertTime = float.NegativeInfinity;
        const float ConsecutiveErrorAlertCooldownSeconds = 25f;

        public ConveyorStatus Conveyor { get; private set; } = new ConveyorStatus();
        public BrainStatus Brain { get; private set; } = new BrainStatus();
        public SortingStats Sorting { get; private set; } = new SortingStats();
        public LineHealthStatus LineHealth { get; private set; } = new LineHealthStatus();
        public LogisticsSnapshotDto Logistics { get; private set; } = new LogisticsSnapshotDto();

        public event Action SnapshotChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// 로봇팔 슬롯 수를 초기화합니다.
        /// </summary>
        /// <param name="count">로봇팔 개수.</param>
        public void InitializeRobots(int count)
        {
            m_robots.Clear();
            for (var i = 0; i < count; i++)
            {
                m_robots.Add(new RobotArmStatus
                {
                    id = $"RobotArm_{i + 1}",
                    currentState = "Idle",
                    currentTask = "None",
                    completionState = "Waiting",
                    batteryPercent = 100f,
                    isNormal = true,
                    hasBug = false,
                    jointAnglesDeg = Array.Empty<float>(),
                    endEffectorPosition = new float[3],
                    endEffectorRotationEuler = new float[3]
                });
            }

            NotifyChanged();
        }

        /// <summary>
        /// 로봇팔 상태를 갱신합니다.
        /// </summary>
        public void UpdateRobot(int index, RobotArmStatus status)
        {
            if (index < 0 || index >= m_robots.Count)
            {
                return;
            }

            m_robots[index] = status;
            NotifyChanged();
        }

        /// <summary>
        /// 컨베이어 상태를 갱신합니다.
        /// </summary>
        public void UpdateConveyor(ConveyorStatus status)
        {
            Conveyor = status;
            NotifyChanged();
        }

        /// <summary>
        /// 중앙뇌 상태를 갱신합니다.
        /// </summary>
        public void UpdateBrain(BrainStatus status)
        {
            Brain = status;
            NotifyChanged();
        }

        /// <summary>
        /// 불일치 물품 개수를 증가시킵니다.
        /// </summary>
        public void IncrementMismatch()
        {
            Sorting.mismatchCount++;
            NotifyChanged();
        }

        /// <summary>
        /// 인식 실패 개수를 증가시킵니다.
        /// </summary>
        public void IncrementRecognitionFailure()
        {
            Sorting.recognitionFailureCount++;
            NotifyChanged();
        }

        /// <summary>
        /// 미처리 물품 개수를 증가시킵니다.
        /// </summary>
        public void IncrementUnprocessed()
        {
            Sorting.unprocessedCount++;
            NotifyChanged();
        }

        /// <summary>
        /// 연속 에러 물류 알림 임계값을 설정합니다(대시보드).
        /// </summary>
        public void SetConsecutiveErrorAlertThreshold(int threshold)
        {
            m_consecutiveErrorAlertThreshold = Mathf.Max(1, threshold);
            Sorting.consecutiveErrorAlertThreshold = m_consecutiveErrorAlertThreshold;
            NotifyChanged();
        }

        /// <summary>
        /// FactoryConfig 기본값으로 알림 임계값을 초기화합니다.
        /// </summary>
        public void InitializeConsecutiveErrorAlertThreshold(int threshold)
        {
            SetConsecutiveErrorAlertThreshold(threshold);
        }

        /// <summary>
        /// 에러 물류 전용 스폰 버스트 상태를 갱신합니다.
        /// </summary>
        public void SetErrorLogisticsBurstActive(bool active)
        {
            Sorting.errorLogisticsBurstActive = active;
            NotifyChanged();
        }

        /// <summary>
        /// 에러 물류가 스테이션에서 ErrorZone으로 배정됐을 때 호출합니다.
        /// </summary>
        public void RecordErrorLogisticsProcessed()
        {
            Sorting.errorLogisticsCount++;
            Sorting.consecutiveErrorLogisticsCount++;
            Sorting.consecutiveErrorAlertThreshold = m_consecutiveErrorAlertThreshold;
            if (Sorting.consecutiveErrorLogisticsCount >= m_consecutiveErrorAlertThreshold &&
                Time.time - m_lastConsecutiveErrorAlertTime >= ConsecutiveErrorAlertCooldownSeconds)
            {
                m_lastConsecutiveErrorAlertTime = Time.time;
                AddNotification(
                    "sorting",
                    $"긴급: 연속 에러 물류 {Sorting.consecutiveErrorLogisticsCount}개 " +
                    $"(임계 {m_consecutiveErrorAlertThreshold}개 이상)",
                    "urgent");
            }

            NotifyChanged();
        }

        /// <summary>
        /// 일반 물류가 스테이션에서 처리됐을 때 연속 에러 물류 카운터를 리셋합니다.
        /// </summary>
        public void ResetConsecutiveErrorLogistics()
        {
            if (Sorting.consecutiveErrorLogisticsCount == 0)
            {
                return;
            }

            Sorting.consecutiveErrorLogisticsCount = 0;
            NotifyChanged();
        }

        /// <summary>
        /// 알림 로그를 추가합니다.
        /// </summary>
        public void AddNotification(string target, string message, string severity = "normal")
        {
            m_notifications.Insert(0, new NotificationEntry
            {
                timestampUtc = DateTime.UtcNow.ToString("o"),
                target = target,
                message = message,
                severity = severity
            });

            if (m_notifications.Count > 50)
            {
                m_notifications.RemoveAt(m_notifications.Count - 1);
            }

            NotifyChanged();
        }

        /// <summary>
        /// 벨트·로봇 정체 감시 결과를 갱신합니다.
        /// </summary>
        public void UpdateLineHealth(LineHealthStatus status)
        {
            LineHealth = status ?? new LineHealthStatus();
            NotifyChanged();
        }

        /// <summary>물류센터 확장 스냅샷을 갱신합니다.</summary>
        public void UpdateLogistics(LogisticsSnapshotDto logistics)
        {
            Logistics = logistics ?? new LogisticsSnapshotDto();
            NotifyChanged();
        }

        /// <summary>로봇팔 구역 메타데이터를 갱신합니다.</summary>
        public void UpdateRobotRegionMeta(int index, string regionId, int armIndexInRegion)
        {
            if (index < 0 || index >= m_robots.Count)
            {
                return;
            }

            var r = m_robots[index];
            r.regionId = regionId;
            r.armIndexInRegion = armIndexInRegion;
            m_robots[index] = r;
            NotifyChanged();
        }

        /// <summary>
        /// 로봇팔 정체 플래그만 병합 갱신합니다(나머지 필드는 유지).
        /// </summary>
        public void UpdateRobotMovementStall(int index, bool stalled, float secondsSinceMove)
        {
            if (index < 0 || index >= m_robots.Count)
            {
                return;
            }

            var r = m_robots[index];
            r.isMovementStalled = stalled;
            r.secondsSinceEndEffectorMove = secondsSinceMove;
            m_robots[index] = r;
            NotifyChanged();
        }

        /// <summary>
        /// 현재 스냅샷을 생성합니다.
        /// </summary>
        public FactorySnapshot CreateSnapshot()
        {
            return new FactorySnapshot
            {
                timestampUtc = DateTime.UtcNow.ToString("o"),
                conveyor = Conveyor,
                robots = new List<RobotArmStatus>(m_robots),
                brain = Brain,
                sorting = Sorting,
                notifications = new List<NotificationEntry>(m_notifications),
                lineHealth = LineHealth,
                logistics = Logistics
            };
        }

        void NotifyChanged()
        {
            SnapshotChanged?.Invoke();
        }
    }
}
