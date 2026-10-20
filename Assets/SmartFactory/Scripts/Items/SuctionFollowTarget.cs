using UnityEngine;

namespace SmartFactory.Items
{
    /// <summary>
    /// 흡착 중 물류가 Gancho1(흡착부)의 아랫면·회전을 따라가도록 Lerp/Slerp합니다.
    /// SmartFactory.Robot.RobotArmSuctionGripper가 Attach 시 활성화합니다.
    /// </summary>
    public class SuctionFollowTarget : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float speed = 65f;
        [SerializeField] Vector3 localPositionOffset;
        [SerializeField] Quaternion localRotationOffset = Quaternion.identity;

        /// <summary>따라갈 대상(Gancho1).</summary>
        public Transform Target => target;

        /// <summary>위치·회전 추종 속도.</summary>
        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0.01f, value);
        }

        /// <summary>
        /// 추종 대상과 Gancho1 로컬 공간 오프셋(아랫면 접촉)을 설정합니다.
        /// </summary>
        /// <param name="gancho">흡착부 Transform.</param>
        /// <param name="positionOffsetGanchoLocal">Gancho1 로컬 좌표에서 물류 pivot이 놓일 위치.</param>
        /// <param name="rotationOffsetGanchoLocal">Gancho1 로컬 기준 물류 회전 오프셋.</param>
        public void Configure(Transform gancho, Vector3 positionOffsetGanchoLocal, Quaternion rotationOffsetGanchoLocal)
        {
            target = gancho;
            localPositionOffset = positionOffsetGanchoLocal;
            localRotationOffset = rotationOffsetGanchoLocal;
        }

        /// <summary>부착 직후 목표 위치·회전으로 즉시 맞춥니다.</summary>
        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.TransformPoint(localPositionOffset);
            transform.rotation = target.rotation * localRotationOffset;
        }

        /// <summary>
        /// 한 물리 스텝 분량만큼 목표를 따라갑니다.
        /// </summary>
        /// <param name="deltaTime">FixedUpdate의 fixedDeltaTime 등.</param>
        /// <param name="speedOverride">0 이하면 <see cref="speed"/> 사용.</param>
        public void StepFollow(float deltaTime, float speedOverride = -1f)
        {
            if (target == null)
            {
                return;
            }

            var followSpeed = speedOverride > 0f ? speedOverride : speed;
            var blend = 1f - Mathf.Exp(-followSpeed * deltaTime);
            var desiredPosition = target.TransformPoint(localPositionOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);

            var desiredRotation = target.rotation * localRotationOffset;
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }

        void Update()
        {
            StepFollow(Time.deltaTime);
        }
    }
}
