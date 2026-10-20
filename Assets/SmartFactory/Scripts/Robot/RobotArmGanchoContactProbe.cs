using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// Gancho_end Collider와 물류 본체 Collider의 물리 충돌(Enter/Stay/Exit) 누적. Collider 크기는 변경하지 않습니다.
    /// </summary>
    public class RobotArmGanchoContactProbe : MonoBehaviour
    {
        [SerializeField] int ganchoIndex;
        int m_trackedItemId = -1;
        float m_contactSeconds;
        bool m_bodyContactActive;

        /// <summary>프로브 인덱스(1 또는 2).</summary>
        public int GanchoIndex => ganchoIndex;

        /// <summary>집게 끝 Transform(접촉 거리 판정용).</summary>
        public Transform TipTransform => transform;

        /// <summary>
        /// 빌더/런타임에서 gancho 인덱스를 설정합니다.
        /// </summary>
        public void Configure(int index)
        {
            ganchoIndex = index;
        }

        /// <summary>
        /// 리셋하지 않고 누적 접촉 시간을 조회합니다.
        /// </summary>
        public float PeekContactSeconds(int itemId)
        {
            return m_trackedItemId == itemId ? m_contactSeconds : 0f;
        }

        /// <summary>
        /// 새 run 시작 시 접촉 누적을 초기화합니다.
        /// </summary>
        public void ResetTracking(int itemId)
        {
            m_trackedItemId = itemId;
            m_contactSeconds = 0f;
            m_bodyContactActive = false;
        }

        /// <summary>
        /// 현재 프레임 직전 물리 스텝에서 물류 본체 Collider와 이 Gancho Collider가 접촉 중인지 여부.
        /// </summary>
        public bool IsBodyContactingItem(int itemId)
        {
            return m_trackedItemId == itemId && m_bodyContactActive;
        }

        void OnCollisionEnter(Collision collision)
        {
            RegisterBodyContact(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            RegisterBodyContact(collision);
        }

        void OnCollisionExit(Collision collision)
        {
            if (PickLiftTrace.ActiveItemId < 0)
            {
                return;
            }

            var item = collision.rigidbody != null ? collision.rigidbody.GetComponent<FactoryItem>() : null;
            if (item == null || item.ItemId != m_trackedItemId)
            {
                return;
            }

            if (!IsBodyColliderHit(item, collision))
            {
                return;
            }

            m_bodyContactActive = false;
        }

        void RegisterBodyContact(Collision collision)
        {
            if (PickLiftTrace.ActiveItemId < 0)
            {
                return;
            }

            var item = collision.rigidbody != null ? collision.rigidbody.GetComponent<FactoryItem>() : null;
            if (item == null || item.ItemId != PickLiftTrace.ActiveItemId)
            {
                return;
            }

            if (!IsBodyColliderHit(item, collision))
            {
                return;
            }

            AccumulateBodyContact(item);
        }

        /// <summary>
        /// 물류 Rigidbody 쪽 OnCollision에서 Gancho_end 접촉을 동일 규칙으로 누적합니다.
        /// </summary>
        public void RegisterBodyContactFromItem(FactoryItem item, Collision collision)
        {
            if (item == null || PickLiftTrace.ActiveItemId < 0 || item.ItemId != PickLiftTrace.ActiveItemId)
            {
                return;
            }

            if (collision == null || collision.collider.GetComponent<RobotArmGanchoContactProbe>() != this)
            {
                return;
            }

            AccumulateBodyContact(item);
        }

        void AccumulateBodyContact(FactoryItem item)
        {
            if (m_trackedItemId != item.ItemId)
            {
                m_trackedItemId = item.ItemId;
                m_contactSeconds = 0f;
            }

            m_bodyContactActive = true;
            m_contactSeconds += Time.fixedDeltaTime;
        }

        static bool IsBodyColliderHit(FactoryItem item, Collision collision)
        {
            var col = collision.collider;
            if (col == null || col.GetComponentInParent<FactoryItem>() != item)
            {
                return false;
            }

            if (item.QrLabelTransform != null && col.transform.IsChildOf(item.QrLabelTransform))
            {
                return false;
            }

            if (col.transform.name == "QrLabel")
            {
                return false;
            }

            var body = item.BodyCollider;
            return body == null || col == body;
        }
    }
}
