using System.Collections.Generic;
using SmartFactory.Brain;
using SmartFactory.Conveyor;
using SmartFactory.Items;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory.Core
{
    /// <summary>
    /// 벨트 물류 정체와 로봇팔 무움직임을 감시해 긴급 알림·중앙뇌 트리거를 발생시킵니다.
    /// </summary>
    public class FactoryLineHealthMonitor : MonoBehaviour
    {
        const float SampleIntervalSeconds = 0.5f;

        [SerializeField] ConveyorBelt conveyor;
        [SerializeField] RobotArmController[] robotArms;
        [SerializeField] BrainScheduler brainScheduler;
        [SerializeField] float robotStillThresholdSeconds = 5f;
        [SerializeField] float logisticsStagnationThresholdSeconds = 3f;
        [SerializeField] int minStuckItemsOnBelt = 2;
        [SerializeField] float beltItemSpeedThreshold = 0.08f;
        [SerializeField] float endEffectorMoveEpsilon = 0.003f;
        [SerializeField] float emergencyNotifyCooldownSeconds = 45f;

        readonly Dictionary<int, Vector3> m_lastEndEffectorPos = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> m_lastEndEffectorMoveTime = new Dictionary<int, float>();

        float m_nextSampleTime;
        float m_logisticsStagnationAccum;
        float m_lastEmergencyNotifyTime = float.NegativeInfinity;
        bool m_emergencyLatched;

        /// <summary>
        /// 감시 대상 참조를 주입합니다.
        /// </summary>
        public void Configure(ConveyorBelt belt, RobotArmController[] arms, BrainScheduler scheduler)
        {
            conveyor = belt;
            robotArms = arms;
            brainScheduler = scheduler;
        }

        void Update()
        {
            if (Time.time < m_nextSampleTime)
            {
                return;
            }

            m_nextSampleTime = Time.time + SampleIntervalSeconds;
            TickSample();
        }

        void TickSample()
        {
            var registry = FactoryStateRegistry.Instance;
            if (registry == null)
            {
                return;
            }

            var beltRunning = conveyor != null && conveyor.IsRunning && conveyor.Speed > 0.01f;
            var stuckOnBelt = CountStuckBeltItems(beltRunning);
            var logisticsStagnation = beltRunning && stuckOnBelt >= minStuckItemsOnBelt;

            if (logisticsStagnation)
            {
                m_logisticsStagnationAccum += SampleIntervalSeconds;
            }
            else
            {
                m_logisticsStagnationAccum = 0f;
                if (!AnyRobotStalled())
                {
                    m_emergencyLatched = false;
                }
            }

            var logisticsSustained = m_logisticsStagnationAccum >= logisticsStagnationThresholdSeconds;
            var stalledIds = UpdateRobotStallTracking(registry, logisticsSustained, stuckOnBelt);
            var robotsStill = stalledIds.Count > 0;
            var emergency = logisticsSustained && robotsStill;

            var message = emergency
                ? $"긴급: 벨트 물류 정체({stuckOnBelt}개) + 로봇팔 {robotStillThresholdSeconds:F0}초 이상 무움직임 ({string.Join(", ", stalledIds)})"
                : string.Empty;

            registry.UpdateLineHealth(new LineHealthStatus
            {
                logisticsStagnation = logisticsStagnation,
                logisticsStagnationSeconds = m_logisticsStagnationAccum,
                emergencyActive = emergency,
                emergencyMessage = message,
                stalledRobotIds = stalledIds
            });

            if (emergency && !m_emergencyLatched &&
                Time.time - m_lastEmergencyNotifyTime >= emergencyNotifyCooldownSeconds)
            {
                m_emergencyLatched = true;
                m_lastEmergencyNotifyTime = Time.time;
                registry.AddNotification("line", message, "urgent");
                brainScheduler?.TriggerImmediate();
            }
        }

        bool AnyRobotStalled()
        {
            if (robotArms == null)
            {
                return false;
            }

            var now = Time.time;
            foreach (var arm in robotArms)
            {
                if (arm == null)
                {
                    continue;
                }

                if (m_lastEndEffectorMoveTime.TryGetValue(arm.ConfiguredIndex, out var lastMove) &&
                    now - lastMove >= robotStillThresholdSeconds &&
                    (arm.IsBusy || arm.CurrentTask != "Idle"))
                {
                    return true;
                }
            }

            return false;
        }

        List<string> UpdateRobotStallTracking(
            FactoryStateRegistry registry,
            bool logisticsSustained,
            int stuckOnBelt)
        {
            var stalled = new List<string>();
            if (robotArms == null)
            {
                return stalled;
            }

            var now = Time.time;
            foreach (var arm in robotArms)
            {
                if (arm == null || arm.EndEffector == null)
                {
                    continue;
                }

                var index = arm.ConfiguredIndex;
                var pos = arm.EndEffector.position;
                if (!m_lastEndEffectorPos.TryGetValue(index, out var prev))
                {
                    m_lastEndEffectorPos[index] = pos;
                    m_lastEndEffectorMoveTime[index] = now;
                }
                else if ((pos - prev).sqrMagnitude >= endEffectorMoveEpsilon * endEffectorMoveEpsilon)
                {
                    m_lastEndEffectorPos[index] = pos;
                    m_lastEndEffectorMoveTime[index] = now;
                }

                if (!m_lastEndEffectorMoveTime.TryGetValue(index, out var lastMove))
                {
                    lastMove = now;
                }

                var since = now - lastMove;
                var trackMotion = arm.IsBusy || arm.CurrentTask != "Idle" ||
                                  (logisticsSustained && stuckOnBelt >= minStuckItemsOnBelt);
                var isStalled = trackMotion && since >= robotStillThresholdSeconds;
                registry.UpdateRobotMovementStall(index, isStalled, since);
                if (isStalled)
                {
                    stalled.Add($"RobotArm_{index + 1}");
                }
            }

            return stalled;
        }

        int CountStuckBeltItems(bool beltRunning)
        {
            if (!beltRunning)
            {
                return 0;
            }

            var items = Object.FindObjectsByType<FactoryItem>(FindObjectsSortMode.None);
            var count = 0;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null || !item.gameObject.activeInHierarchy || item.IsHeldByRobot ||
                    item.IsRobotPipelineActive || item.IsZoneDespawnScheduled)
                {
                    continue;
                }

                var rb = item.ItemRigidbody;
                if (rb == null || rb.isKinematic)
                {
                    continue;
                }

                var vx = Mathf.Abs(rb.linearVelocity.x);
                if (vx <= beltItemSpeedThreshold)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
