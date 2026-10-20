using SmartFactory.Core;
using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Conveyor
{
    /// <summary>
    /// 벨트는 월드에 고정하고, 접촉 중인 물류 Rigidbody를 +X(설정 방향)로 운반합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ConveyorBelt : MonoBehaviour
    {
        [SerializeField] Vector3 moveDirection = Vector3.right;
        [SerializeField] float speed = 1.2f;

        Rigidbody m_rigidbody;
        bool m_running = true;
        string m_error;

        public float Speed => speed;
        public bool IsRunning => m_running;

        void Awake()
        {
            m_rigidbody = GetComponent<Rigidbody>();
            m_rigidbody.isKinematic = true;
            moveDirection.Normalize();
        }

        void FixedUpdate()
        {
            PublishStatus();
        }

        void OnCollisionStay(Collision collision)
        {
            if (!m_running)
            {
                return;
            }

            var body = collision.rigidbody;
            if (body == null || body.isKinematic)
            {
                return;
            }

            var factoryItem = body.GetComponent<FactoryItem>();
            if (factoryItem != null && factoryItem.IsHeldByRobot)
            {
                factoryItem.SetBeltTransportSkippedThisFixedStep(true);
                return;
            }

            if (factoryItem != null && factoryItem.IsPickBeltStabilized)
            {
                factoryItem.SetBeltTransportSkippedThisFixedStep(true);
                var stabilizedVel = body.linearVelocity;
                body.linearVelocity = new Vector3(0f, stabilizedVel.y, stabilizedVel.z);
                return;
            }

            if (factoryItem != null)
            {
                factoryItem.SetBeltTransportSkippedThisFixedStep(false);
            }

            var transport = moveDirection * speed;
            var velocity = body.linearVelocity;
            body.linearVelocity = new Vector3(transport.x, velocity.y, transport.z);
        }

        /// <summary>
        /// 벨트 속도를 설정합니다.
        /// </summary>
        public void SetSpeed(float newSpeed)
        {
            speed = Mathf.Max(0f, newSpeed);
            PublishStatus();
        }

        /// <summary>
        /// 벨트를 시작합니다.
        /// </summary>
        public void StartBelt()
        {
            m_running = true;
            m_error = null;
            PublishStatus();
        }

        /// <summary>
        /// 벨트를 정지합니다.
        /// </summary>
        public void StopBelt()
        {
            m_running = false;
            PublishStatus();
        }

        /// <summary>
        /// 오류 상태를 설정합니다.
        /// </summary>
        public void SetError(string message)
        {
            m_error = message;
            m_running = false;
            PublishStatus();
        }

        void PublishStatus()
        {
            if (FactoryStateRegistry.Instance == null)
            {
                return;
            }

            FactoryStateRegistry.Instance.UpdateConveyor(new ConveyorStatus
            {
                isRunning = m_running,
                speed = m_running ? speed : 0f,
                errorDetail = m_error
            });
        }
    }
}
