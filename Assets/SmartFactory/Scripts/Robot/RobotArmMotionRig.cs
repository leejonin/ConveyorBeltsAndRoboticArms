using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 바인딩 해석 결과: 로봇팔_움직임.md에 정의된 회전 제어 대상 Transform.
    /// </summary>
    public sealed class RobotArmMotionRig
    {
        public Transform Principal;
        public Transform Brazo1;
        public Transform Brazo2;
        public Transform Brazo3;
        public Transform Gancho1;
        public Transform Gancho2;
        /// <summary>Brazo3 직속 집게 중심(물류 좌표와 일치해야 Gancho로 집기).</summary>
        public Transform GripCenter;
        public Transform EndEffector;
        public Transform ArmRigMonitor;
        public Transform BaseMonitor;

        public bool IsValid =>
            Principal != null && Brazo1 != null && Brazo2 != null && Brazo3 != null &&
            Gancho1 != null && EndEffector != null;
    }
}
