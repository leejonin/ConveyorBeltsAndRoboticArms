using SmartFactory.Core;
using UnityEngine;

namespace SmartFactory.Items
{
    /// <summary>
    /// 벨트 끝까지 처리되지 않은 물품을 회수합니다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class UnprocessedZone : MonoBehaviour
    {
        const float MinTriggerHeightLocal = 1.5f;

        [SerializeField] ItemSpawner spawner;

        BoxCollider m_box;
        Rigidbody m_zoneRigidbody;

        void Awake()
        {
            EnsureTriggerPhysics();
        }

        void FixedUpdate()
        {
            PollOverlappingItems();
        }

        void OnTriggerEnter(Collider other)
        {
            TryConsumeFromCollider(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryConsumeFromCollider(other);
        }

        /// <summary>
        /// 트리거 Box·kinematic Rigidbody를 설정해 고속 벨트 물류도 놓치지 않습니다.
        /// </summary>
        void EnsureTriggerPhysics()
        {
            m_box = GetComponent<BoxCollider>();
            m_box.isTrigger = true;
            if (m_box.size.y < MinTriggerHeightLocal)
            {
                m_box.size = new Vector3(m_box.size.x, MinTriggerHeightLocal, m_box.size.z);
            }

            m_zoneRigidbody = GetComponent<Rigidbody>();
            if (m_zoneRigidbody == null)
            {
                m_zoneRigidbody = gameObject.AddComponent<Rigidbody>();
            }

            m_zoneRigidbody.isKinematic = true;
            m_zoneRigidbody.useGravity = false;
        }

        void PollOverlappingItems()
        {
            if (m_box == null)
            {
                return;
            }

            var center = transform.TransformPoint(m_box.center);
            var halfExtents = Vector3.Scale(m_box.size * 0.5f, transform.lossyScale);
            var hits = Physics.OverlapBox(
                center,
                halfExtents,
                transform.rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < hits.Length; i++)
            {
                var item = hits[i].GetComponentInParent<FactoryItem>();
                TryConsumeItem(item, trustTriggerOverlap: true);
            }
        }

        void TryConsumeFromCollider(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var item = other.GetComponentInParent<FactoryItem>();
            TryConsumeItem(item, trustTriggerOverlap: true);
        }

        /// <summary>
        /// 트리거 재진입 없이 Zone 안에 놓인 물류를 회수합니다(로봇 FAIL Zone 배치 직후).
        /// </summary>
        /// <param name="item">대상 물류.</param>
        /// <param name="trustTriggerOverlap">true면 bounds 재검사 생략(Physics 트리거·OverlapBox 경로).</param>
        /// <returns>Despawn 처리했으면 true.</returns>
        public bool TryConsumeItem(FactoryItem item, bool trustTriggerOverlap = false)
        {
            if (item == null || !item.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (item.IsHeldByRobot || item.IsRobotPipelineActive)
            {
                return false;
            }

            if (item.IsZoneDespawnScheduled)
            {
                return false;
            }

            if (item.IsErrorLogistics)
            {
                if (!item.SuppressUnprocessedStatIncrement)
                {
                    FactoryStateRegistry.Instance?.AddNotification(
                        "sorting",
                        $"에러 물류 #{item.ItemId}가 ErrorZone 배치 전 벨트 끝에서 회수됨",
                        "urgent");
                }

                item.SuppressUnprocessedStatIncrement = true;
                var errorSpawner = ResolveSpawner();
                if (errorSpawner != null)
                {
                    errorSpawner.Despawn(item);
                }

                return errorSpawner != null;
            }

            if (!trustTriggerOverlap && !IsItemOverlappingZone(item))
            {
                return false;
            }

            var resolvedSpawner = ResolveSpawner();
            if (resolvedSpawner == null)
            {
                return false;
            }

            if (!item.SuppressUnprocessedStatIncrement)
            {
                FactoryStateRegistry.Instance?.IncrementUnprocessed();
            }

            item.SuppressUnprocessedStatIncrement = false;
            resolvedSpawner.Despawn(item);
            return true;
        }

        ItemSpawner ResolveSpawner()
        {
            if (spawner != null)
            {
                return spawner;
            }

            spawner = FindFirstObjectByType<ItemSpawner>();
            return spawner;
        }

        /// <summary>물류 본체 Collider가 이 Zone 트리거와 겹치는지 확인합니다.</summary>
        bool IsItemOverlappingZone(FactoryItem item)
        {
            var zoneCol = m_box != null ? m_box : GetComponent<Collider>();
            var body = item.BodyCollider;
            if (zoneCol == null || body == null)
            {
                return false;
            }

            return zoneCol.bounds.Intersects(body.bounds);
        }
    }
}
