using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// Brazo3 등 Joint 호스트에서 OnJointBreak를 RobotArmController로 전달합니다.
    /// </summary>
    public class RobotArmGrabJointHost : MonoBehaviour
    {
        RobotArmController m_owner;

        /// <summary>
        /// 소유 컨트롤러를 연결합니다.
        /// </summary>
        public void Bind(RobotArmController owner)
        {
            m_owner = owner;
        }

        void OnJointBreak(float breakForce)
        {
            m_owner?.NotifyGrabJointBroken(breakForce);
        }
    }
}
