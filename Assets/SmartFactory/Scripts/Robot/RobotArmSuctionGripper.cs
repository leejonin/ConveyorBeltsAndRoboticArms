using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// Gancho1 진공 흡착: 접촉 시 물류를 붙이고 <see cref="SuctionFollowTarget"/>로 추종합니다.
    /// </summary>
    public class RobotArmSuctionGripper : MonoBehaviour
    {
        [SerializeField] bool suctionOn;
        [Tooltip("부착 후 Gancho1 추종 강도(클수록 빠르게 붙음).")]
        [SerializeField] float followSpeed = 65f;
        [Tooltip("흡착 On·접촉 전 대상 물류를 Gancho1 쪽으로 당기는 힘(N).")]
        [SerializeField] float suctionPullStrength = 320f;
        [Tooltip("Gancho1과 물류가 이 거리(m) 이내면 Collider 없이도 부착합니다.")]
        [SerializeField] float proximityAttachDistance = 0.14f;

        FactoryItem m_expectedItem;
        FactoryItem m_heldItem;
        Transform m_originalItemParent;
        SuctionFollowTarget m_follow;
        float m_contactSeconds;
        bool m_savedKinematic;
        bool m_savedUseGravity;
        FactoryItem m_gravitySuspendedItem;
        bool m_savedUseGravityBeforeSuction;
        FactoryItem m_pickCollisionIgnoredItem;

        /// <summary>Inspector 흡착 On 상태.</summary>
        public bool SuctionOn => suctionOn;

        /// <summary>현재 붙어 있는 물류.</summary>
        public FactoryItem HeldItem => m_heldItem;

        /// <summary>이번 집기 run에서 접촉 누적 시간(초).</summary>
        public float ContactSeconds => m_contactSeconds;

        /// <summary>
        /// 흡착 On/Off. On이면 대상·부착 물류 Rigidbody.useGravity=false, Off·분리 시 복원합니다.
        /// </summary>
        /// <param name="on">흡착 활성.</param>
        /// <param name="expectedItem">집기 대상(지정 시 해당 물류만 흡착).</param>
        public void SetSuction(bool on, FactoryItem expectedItem = null)
        {
            if (on && m_heldItem != null && expectedItem != null && m_heldItem == expectedItem)
            {
                suctionOn = true;
                m_expectedItem = expectedItem;
                EnforceItemNoGravity(m_heldItem);
                return;
            }

            suctionOn = on;
            if (on)
            {
                m_expectedItem = expectedItem;
                if (expectedItem != null)
                {
                    SuspendGravityForItem(expectedItem);
                }
            }
            else
            {
                m_expectedItem = null;
                Detach();
                RestorePreAttachGravity();
                RestorePickCollisionIgnore();
            }
        }

        /// <summary>
        /// 집기·흡착 접근 중 Gancho Collider가 물류를 밀어내지 않도록 충돌을 끕니다.
        /// </summary>
        /// <param name="item">대상 물류.</param>
        /// <param name="ignore">true면 IgnoreCollision, false면 복원.</param>
        public void SetPickCollisionIgnore(FactoryItem item, bool ignore)
        {
            if (item == null)
            {
                return;
            }

            var body = item.BodyCollider;
            if (body == null)
            {
                return;
            }

            var cols = GetComponentsInChildren<Collider>(false);
            for (var i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col != null && col.enabled)
                {
                    Physics.IgnoreCollision(col, body, ignore);
                }
            }

            if (ignore)
            {
                m_pickCollisionIgnoredItem = item;
            }
            else if (m_pickCollisionIgnoredItem == item)
            {
                m_pickCollisionIgnoredItem = null;
            }
        }

        /// <summary>집기용 IgnoreCollision을 모두 해제합니다.</summary>
        void RestorePickCollisionIgnore()
        {
            if (m_pickCollisionIgnoredItem == null)
            {
                return;
            }

            SetPickCollisionIgnore(m_pickCollisionIgnoredItem, false);
        }

        /// <summary>
        /// 지정 물류를 흡착 유지 중인지 확인합니다.
        /// </summary>
        public bool IsHolding(FactoryItem item)
        {
            return item != null && m_heldItem == item;
        }

        void OnValidate()
        {
            if (!suctionOn && m_heldItem != null)
            {
                Detach();
                RestorePreAttachGravity();
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            TryRegisterContact(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            TryRegisterContact(collision);
        }

        void FixedUpdate()
        {
            if (suctionOn && m_heldItem != null)
            {
                EnforceItemNoGravity(m_heldItem);
            }
            else if (suctionOn && m_expectedItem != null && m_heldItem == null)
            {
                SuspendGravityForItem(m_expectedItem);
            }

            ApplyPreAttachSuctionPull();
            TryProximityAttach();
        }

        /// <summary>흡착 대상·부착 물류의 Rigidbody 중력을 끕니다.</summary>
        void SuspendGravityForItem(FactoryItem item)
        {
            if (item == null)
            {
                return;
            }

            var rb = item.ItemRigidbody;
            if (rb == null)
            {
                return;
            }

            if (m_gravitySuspendedItem != item)
            {
                m_gravitySuspendedItem = item;
                m_savedUseGravityBeforeSuction = rb.useGravity;
            }

            rb.useGravity = false;
        }

        /// <summary>부착 전에만 중력을 껐던 물류의 useGravity를 복원합니다.</summary>
        void RestorePreAttachGravity()
        {
            if (m_gravitySuspendedItem == null || m_gravitySuspendedItem == m_heldItem)
            {
                m_gravitySuspendedItem = null;
                return;
            }

            var rb = m_gravitySuspendedItem.ItemRigidbody;
            if (rb != null)
            {
                rb.useGravity = m_savedUseGravityBeforeSuction;
            }

            m_gravitySuspendedItem = null;
        }

        /// <summary>흡착 유지 중 매 FixedUpdate마다 중력·kinematic 상태를 고정합니다.</summary>
        void EnforceItemNoGravity(FactoryItem item)
        {
            if (item == null)
            {
                return;
            }

            var rb = item.ItemRigidbody;
            if (rb == null)
            {
                return;
            }

            rb.useGravity = false;
            rb.isKinematic = true;
        }

        /// <summary>
        /// 부착 전 흡착 On 상태에서 중력을 끄고 대상 물류를 Gancho1 쪽으로 당깁니다.
        /// </summary>
        void ApplyPreAttachSuctionPull()
        {
            if (!suctionOn || m_heldItem != null || m_expectedItem == null)
            {
                return;
            }

            SuspendGravityForItem(m_expectedItem);
            var rb = m_expectedItem.ItemRigidbody;
            if (rb == null || rb.isKinematic)
            {
                return;
            }

            var bottomWorld = transform.TransformPoint(ComputeGanchoBottomLocal());
            var targetPivot = m_expectedItem.PickAlignWorldPosition;
            var toGancho = bottomWorld - targetPivot;
            var dist = toGancho.magnitude;
            if (dist < 0.0001f)
            {
                return;
            }

            // 벨트 밖(X/Z)으로 밀지 않도록 수직(+Y) 당김만 적용합니다.
            var verticalGap = toGancho.y;
            if (verticalGap <= 0.0001f)
            {
                return;
            }

            var pullMag = suctionPullStrength * (1f + Mathf.Clamp(verticalGap, 0f, 0.25f));
            rb.AddForce(Vector3.up * pullMag, ForceMode.Force);
        }

        void TryRegisterContact(Collision collision)
        {
            if (!suctionOn || m_heldItem != null || collision == null)
            {
                return;
            }

            var item = ResolveFactoryItem(collision);
            if (item == null || !IsValidBodyHit(item, collision))
            {
                return;
            }

            m_contactSeconds += Time.fixedDeltaTime;
            Attach(item);
        }

        FactoryItem ResolveFactoryItem(Collision collision)
        {
            var rb = collision.rigidbody;
            if (rb == null)
            {
                return null;
            }

            var item = rb.GetComponent<FactoryItem>();
            if (item == null)
            {
                return null;
            }

            if (m_expectedItem != null && item != m_expectedItem)
            {
                return null;
            }

            return item;
        }

        static bool IsValidBodyHit(FactoryItem item, Collision collision)
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

        /// <summary>
        /// 물류를 흡착하고 <see cref="SuctionFollowTarget"/>로 Gancho1을 월드 추종합니다(스케일 왜곡 방지).
        /// </summary>
        void Attach(FactoryItem item)
        {
            if (item == null)
            {
                return;
            }

            var rb = item.ItemRigidbody;
            if (rb == null)
            {
                return;
            }

            m_heldItem = item;
            m_originalItemParent = item.transform.parent;
            m_savedKinematic = rb.isKinematic;
            m_savedUseGravity = m_gravitySuspendedItem == item
                ? m_savedUseGravityBeforeSuction
                : rb.useGravity;
            m_gravitySuspendedItem = null;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            m_follow = item.GetComponent<SuctionFollowTarget>();
            if (m_follow == null)
            {
                m_follow = item.gameObject.AddComponent<SuctionFollowTarget>();
            }

            // Gancho1 비균일 scale(0.001,0.0001,0.001) 자식 parenting 시 mesh가 늘어남 → 월드 추종만 사용.
            var attachLocalPos = transform.InverseTransformPoint(item.transform.position);
            var attachLocalRot = Quaternion.Inverse(transform.rotation) * item.transform.rotation;
            m_follow.Configure(transform, attachLocalPos, attachLocalRot);
            m_follow.enabled = true;

            item.IsHeldByRobot = true;
        }

        /// <summary>
        /// Collider 접촉 없이도 Gancho1에 충분히 가까우면 부착합니다(얇은 흡착판 대응).
        /// </summary>
        void TryProximityAttach()
        {
            if (!suctionOn || m_heldItem != null || m_expectedItem == null)
            {
                return;
            }

            var bottomWorld = transform.TransformPoint(ComputeGanchoBottomLocal());
            var itemPivot = m_expectedItem.PickAlignWorldPosition;
            if (Vector3.Distance(bottomWorld, itemPivot) <= proximityAttachDistance)
            {
                Attach(m_expectedItem);
            }
        }

        /// <summary>
        /// Gancho1 아랫면에 물류 윗면이 맞닿도록 부모(Gancho1) 기준 로컬 오프셋을 계산합니다.
        /// </summary>
        (Vector3 positionOffset, Quaternion rotationOffset) ComputeAttachOffsets(FactoryItem item)
        {
            var bottomLocal = ComputeGanchoBottomLocal();
            var halfItemUp = 0.05f;
            var bodyCol = item.BodyCollider;
            if (bodyCol != null)
            {
                halfItemUp = bodyCol.bounds.extents.y;
            }

            var bottomWorld = transform.TransformPoint(bottomLocal);
            var pivotWorld = bottomWorld + Vector3.up * halfItemUp;
            var positionOffset = transform.InverseTransformPoint(pivotWorld);
            var rotationOffset = Quaternion.Inverse(transform.rotation) * item.transform.rotation;

            return (positionOffset, rotationOffset);
        }

        /// <summary>Gancho1 Collider 아랫면 중심의 로컬 좌표.</summary>
        Vector3 ComputeGanchoBottomLocal()
        {
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                return box.center + new Vector3(0f, -box.size.y * 0.5f, 0f);
            }

            return Vector3.zero;
        }

        /// <summary>
        /// 흡착 해제 후 Rigidbody를 복원합니다.
        /// </summary>
        public void Detach()
        {
            if (m_follow != null)
            {
                m_follow.enabled = false;
            }

            if (m_heldItem != null)
            {
                if (m_heldItem.transform.parent == transform)
                {
                    m_heldItem.transform.SetParent(m_originalItemParent, true);
                }

                m_heldItem.IsHeldByRobot = false;
                var rb = m_heldItem.ItemRigidbody;
                if (rb != null)
                {
                    rb.isKinematic = m_savedKinematic;
                    rb.useGravity = m_savedUseGravity;
                }

                m_heldItem.RestoreWorldPhysics();
            }

            RestorePickCollisionIgnore();

            m_heldItem = null;
            m_originalItemParent = null;
            m_expectedItem = null;
        }

        /// <summary>새 집기 run 전 접촉 누적을 초기화합니다.</summary>
        public void ResetContactTracking()
        {
            m_contactSeconds = 0f;
        }
    }
}
