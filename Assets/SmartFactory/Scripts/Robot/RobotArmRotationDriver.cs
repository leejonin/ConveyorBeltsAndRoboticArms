using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 로봇팔_움직임.md: 지정 Transform의 로컬 Y/Z 회전만 적용하고 각도를 clamp합니다.
    /// </summary>
    public class RobotArmRotationDriver
    {
        readonly RobotArmRigBinding m_binding;
        RobotArmMotionRig m_rig;
        Quaternion m_principalBind;
        Quaternion m_brazo1Bind;
        Quaternion m_brazo2Bind;
        Quaternion m_brazo3Bind;
        Quaternion m_gancho1Bind;
        Quaternion m_gancho2Bind;
        Quaternion m_armRigBind;
        Quaternion m_baseBind;
        Vector3 m_effectorBindPos;
        int m_robotIndex;

        float m_currentPrincipalY;
        float m_currentPrincipalZ;
        float m_currentBrazo1Z;
        float m_currentBrazo2Z;
        float m_currentBrazo3Z;
        float m_currentGancho1Z;
        float m_currentGancho2Z;

        public RobotArmMotionRig Rig => m_rig;
        public bool IsReady => m_rig != null && m_rig.IsValid && m_binding != null;

        public RobotArmRotationDriver(RobotArmRigBinding binding)
        {
            m_binding = binding;
        }

        /// <summary>
        /// 로봇 인덱스(디버그 로그용).
        /// </summary>
        public void SetRobotIndex(int index)
        {
            m_robotIndex = index;
        }

        /// <summary>
        /// modelRoot에서 본을 해석하고 bind pose를 저장합니다.
        /// </summary>
        public bool Initialize(Transform modelRoot)
        {
            if (m_binding == null || modelRoot == null)
            {
                return false;
            }

            if (!m_binding.TryResolveMotionRig(modelRoot, out m_rig))
            {
                return false;
            }

            CacheBindPose();
            ApplyCurrentAngles();
            return true;
        }

        void CacheBindPose()
        {
            m_principalBind = m_rig.Principal.localRotation;
            m_brazo1Bind = m_rig.Brazo1.localRotation;
            m_brazo2Bind = m_rig.Brazo2.localRotation;
            m_brazo3Bind = m_rig.Brazo3.localRotation;
            m_gancho1Bind = m_rig.Gancho1 != null ? m_rig.Gancho1.localRotation : Quaternion.identity;
            m_gancho2Bind = m_rig.Gancho2 != null ? m_rig.Gancho2.localRotation : Quaternion.identity;
            if (m_rig.ArmRigMonitor != null)
            {
                m_armRigBind = m_rig.ArmRigMonitor.localRotation;
            }

            if (m_rig.BaseMonitor != null)
            {
                m_baseBind = m_rig.BaseMonitor.localRotation;
            }

            m_effectorBindPos = m_rig.EndEffector.localPosition;
            m_currentPrincipalY = 0f;
            m_currentPrincipalZ = 0f;
            m_currentBrazo1Z = 0f;
            m_currentBrazo2Z = 0f;
            m_currentBrazo3Z = 0f;
            m_currentGancho1Z = m_binding.gancho1ZMax;
            m_currentGancho2Z = m_binding.gancho2ZMin;
        }

        /// <summary>
        /// 포즈 각도를 즉시 적용합니다.
        /// </summary>
        public void ApplyPose(RobotArmPose pose)
        {
            if (pose == null || !IsReady)
            {
                return;
            }

            m_currentPrincipalY = ClampPrincipalY(pose.principalY);
            m_currentPrincipalZ = Clamp(pose.principalZ, m_binding.principalZMin, m_binding.principalZMax);
            m_currentBrazo1Z = ClampBrazo(pose.brazo1Z);
            m_currentBrazo2Z = ClampBrazo(pose.brazo2Z);
            m_currentBrazo3Z = ClampBrazo(pose.brazo3Z);
            m_currentGancho1Z = Clamp(pose.gancho1Z, m_binding.gancho1ZMin, m_binding.gancho1ZMax);
            m_currentGancho2Z = Clamp(pose.gancho2Z, m_binding.gancho2ZMin, m_binding.gancho2ZMax);
            ApplyCurrentAngles();
        }

        /// <summary>
        /// 두 포즈 사이를 t(0~1)로 보간합니다.
        /// </summary>
        public void BlendPoses(RobotArmPose from, RobotArmPose to, float t)
        {
            if (from == null || to == null || !IsReady)
            {
                return;
            }

            t = Mathf.Clamp01(t);
            m_currentPrincipalY = ClampPrincipalY(Mathf.Lerp(from.principalY, to.principalY, t));
            m_currentPrincipalZ = Clamp(Mathf.Lerp(from.principalZ, to.principalZ, t), m_binding.principalZMin, m_binding.principalZMax);
            m_currentBrazo1Z = ClampBrazo(Mathf.Lerp(from.brazo1Z, to.brazo1Z, t));
            m_currentBrazo2Z = ClampBrazo(Mathf.Lerp(from.brazo2Z, to.brazo2Z, t));
            m_currentBrazo3Z = ClampBrazo(Mathf.Lerp(from.brazo3Z, to.brazo3Z, t));
            m_currentGancho1Z = Clamp(Mathf.Lerp(from.gancho1Z, to.gancho1Z, t), m_binding.gancho1ZMin, m_binding.gancho1ZMax);
            m_currentGancho2Z = Clamp(Mathf.Lerp(from.gancho2Z, to.gancho2Z, t), m_binding.gancho2ZMin, m_binding.gancho2ZMax);
            ApplyCurrentAngles();
        }

        void ApplyCurrentAngles()
        {
            m_rig.Principal.localRotation = m_principalBind * Quaternion.Euler(0f, m_currentPrincipalY, 0f) * Quaternion.Euler(0f, 0f, m_currentPrincipalZ);
            ApplyLocalZ(m_rig.Brazo1, m_brazo1Bind, m_currentBrazo1Z);
            ApplyLocalZ(m_rig.Brazo2, m_brazo2Bind, m_currentBrazo2Z);
            ApplyLocalZ(m_rig.Brazo3, m_brazo3Bind, m_currentBrazo3Z);
            // 흡착형: Gancho1은 회전 DOF 없음(흡착부 고정).
        }

        static void ApplyLocalZ(Transform joint, Quaternion bind, float zDegrees)
        {
            joint.localRotation = bind * Quaternion.Euler(0f, 0f, zDegrees);
        }

        float ClampBrazo(float z) => Clamp(z, m_binding.brazoZMin, m_binding.brazoZMax);

        float ClampPrincipalY(float y) => Clamp(y, m_binding.principalYMin, m_binding.principalYMax);

        static float Clamp(float v, float min, float max) => Mathf.Clamp(v, min, max);

        /// <summary>
        /// 단일 DOF 각도를 설정하고 즉시 회전을 적용합니다.
        /// </summary>
        public void SetDofAngle(RobotArmDof dof, float degrees)
        {
            if (!IsReady)
            {
                return;
            }

            switch (dof)
            {
                case RobotArmDof.PrincipalY:
                    m_currentPrincipalY = ClampPrincipalY(degrees);
                    break;
                case RobotArmDof.PrincipalZ:
                    m_currentPrincipalZ = Clamp(degrees, m_binding.principalZMin, m_binding.principalZMax);
                    break;
                case RobotArmDof.Brazo1Z:
                    m_currentBrazo1Z = ClampBrazo(degrees);
                    break;
                case RobotArmDof.Brazo2Z:
                    m_currentBrazo2Z = ClampBrazo(degrees);
                    break;
                case RobotArmDof.Brazo3Z:
                    m_currentBrazo3Z = ClampBrazo(degrees);
                    break;
                case RobotArmDof.Gancho1Z:
                    m_currentGancho1Z = Clamp(degrees, m_binding.gancho1ZMin, m_binding.gancho1ZMax);
                    break;
                case RobotArmDof.Gancho2Z:
                    m_currentGancho2Z = Clamp(degrees, m_binding.gancho2ZMin, m_binding.gancho2ZMax);
                    break;
            }

            ApplyCurrentAngles();
        }

        /// <summary>
        /// 단일 DOF 현재 각도(도)를 반환합니다.
        /// </summary>
        public float GetDofAngle(RobotArmDof dof)
        {
            switch (dof)
            {
                case RobotArmDof.PrincipalY: return m_currentPrincipalY;
                case RobotArmDof.PrincipalZ: return m_currentPrincipalZ;
                case RobotArmDof.Brazo1Z: return m_currentBrazo1Z;
                case RobotArmDof.Brazo2Z: return m_currentBrazo2Z;
                case RobotArmDof.Brazo3Z: return m_currentBrazo3Z;
                case RobotArmDof.Gancho1Z: return m_currentGancho1Z;
                case RobotArmDof.Gancho2Z: return m_currentGancho2Z;
                default: return 0f;
            }
        }

        /// <summary>
        /// 바인딩 limit 기준 DOF 허용 범위(도).
        /// </summary>
        public void GetDofLimits(RobotArmDof dof, out float min, out float max)
        {
            switch (dof)
            {
                case RobotArmDof.PrincipalY:
                    min = m_binding.principalYMin;
                    max = m_binding.principalYMax;
                    return;
                case RobotArmDof.PrincipalZ:
                    min = m_binding.principalZMin;
                    max = m_binding.principalZMax;
                    return;
                case RobotArmDof.Brazo1Z:
                case RobotArmDof.Brazo2Z:
                case RobotArmDof.Brazo3Z:
                    min = m_binding.brazoZMin;
                    max = m_binding.brazoZMax;
                    return;
                case RobotArmDof.Gancho1Z:
                    min = m_binding.gancho1ZMin;
                    max = m_binding.gancho1ZMax;
                    return;
                case RobotArmDof.Gancho2Z:
                    min = m_binding.gancho2ZMin;
                    max = m_binding.gancho2ZMax;
                    return;
                default:
                    min = -180f;
                    max = 180f;
                    return;
            }
        }

        /// <summary>
        /// 상태 보고용 관절 각도(도).
        /// </summary>
        public float[] GetJointAnglesForStatus()
        {
            return new[]
            {
                m_currentPrincipalY,
                m_currentPrincipalZ,
                m_currentBrazo1Z,
                m_currentBrazo2Z,
                m_currentBrazo3Z,
                m_currentGancho1Z,
                m_currentGancho2Z
            };
        }
    }
}
