using System.Collections.Generic;
using SmartFactory.Conveyor;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory.Brain
{
    /// <summary>
    /// 중앙뇌 판단 결과 중 허용된 명령만 실행합니다.
    /// </summary>
    public class BrainCommandExecutor : MonoBehaviour
    {
        [SerializeField] ConveyorBelt conveyor;
        [SerializeField] ConveyorPickDispatcher pickDispatcher;
        readonly List<ConveyorPickDispatcher> m_pickDispatchers = new List<ConveyorPickDispatcher>();
        readonly Dictionary<string, RobotArmController> m_robots = new Dictionary<string, RobotArmController>();

        /// <summary>
        /// 로봇팔 레지스트리를 등록합니다.
        /// </summary>
        public void RegisterRobot(string id, RobotArmController arm)
        {
            m_robots[id] = arm;
        }

        /// <summary>
        /// 집기 디스패처 참조를 주입합니다(긴급 초기화 시 job 정리).
        /// </summary>
        public void SetPickDispatcher(ConveyorPickDispatcher dispatcher)
        {
            pickDispatcher = dispatcher;
            RegisterPickDispatcher(dispatcher);
        }

        /// <summary>추가 구역 디스패처를 등록합니다(3구역).</summary>
        public void RegisterPickDispatcher(ConveyorPickDispatcher dispatcher)
        {
            if (dispatcher == null || m_pickDispatchers.Contains(dispatcher))
            {
                return;
            }

            m_pickDispatchers.Add(dispatcher);
        }

        /// <summary>
        /// 대시보드에서 호출하는 로봇팔 긴급 초기화.
        /// </summary>
        /// <returns>성공 여부.</returns>
        public bool ResetRobot(string robotId)
        {
            if (string.IsNullOrEmpty(robotId) || !m_robots.TryGetValue(robotId, out var arm) || arm == null)
            {
                return false;
            }

            for (var i = 0; i < m_pickDispatchers.Count; i++)
            {
                m_pickDispatchers[i]?.ClearJobsForArm(arm.ConfiguredIndex);
            }

            pickDispatcher?.ClearJobsForArm(arm.ConfiguredIndex);
            arm.EmergencyReset();
            Core.FactoryStateRegistry.Instance?.AddNotification(robotId, "로봇팔 긴급 초기화 완료", "normal");
            return true;
        }

        /// <summary>
        /// 판단 결과를 실행합니다.
        /// </summary>
        public void Execute(BrainDecision decision)
        {
            if (decision == null)
            {
                return;
            }

            foreach (var action in decision.actions)
            {
                ExecuteAction(action);
            }

            foreach (var escalation in decision.escalations)
            {
                Core.FactoryStateRegistry.Instance?.AddNotification(escalation.to, escalation.message);
            }
        }

        void ExecuteAction(BrainAction action)
        {
            if (action == null)
            {
                return;
            }

            if (action.target == "Conveyor" && conveyor != null)
            {
                switch (action.command)
                {
                    case "start":
                        conveyor.StartBelt();
                        break;
                    case "stop":
                        conveyor.StopBelt();
                        break;
                    case "setSpeed":
                        if (float.TryParse(action.args, out var speed))
                        {
                            conveyor.SetSpeed(speed);
                        }
                        break;
                }

                return;
            }

            if (m_robots.TryGetValue(action.target, out var arm) && arm != null)
            {
                switch (action.command)
                {
                    case "pause":
                        arm.Pause();
                        break;
                    case "resume":
                        arm.Resume();
                        break;
                    case "reset":
                        ResetRobot(action.target);
                        break;
                }
            }
        }
    }
}
