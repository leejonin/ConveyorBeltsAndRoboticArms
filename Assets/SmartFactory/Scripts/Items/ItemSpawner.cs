using System.Collections;
using System.Collections.Generic;
using SmartFactory.Core;
using SmartFactory.Logistics;
using UnityEngine;

namespace SmartFactory.Items
{
    /// <summary>
    /// 1~5초 랜덤 간격으로 원·큐브·삼각형 물품을 생성합니다.
    /// 에러 물류(이상 QR·QR 없음·이상 형태)는 ErrorZone 분류용으로 주입됩니다.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        [SerializeField] FactoryConfig config;
        [SerializeField] Transform spawnPoint;
        [SerializeField] Material circleMaterial;
        [SerializeField] Material cubeMaterial;
        [SerializeField] Material triangleMaterial;
        [SerializeField] bool autoSpawnEnabled = true;

        readonly Queue<FactoryItem> m_pool = new Queue<FactoryItem>();
        int m_nextId = 1;
        float m_errorLogisticsBurstUntil;
        Coroutine m_burstNotifyRoutine;
        float m_spawnIntervalScale = 1f;

        /// <summary>자동 스폰 루프 사용 여부.</summary>
        public bool AutoSpawnEnabled => autoSpawnEnabled;

        void Start()
        {
            if (config == null)
            {
                Debug.LogError("ItemSpawner: FactoryConfig가 필요합니다.");
                return;
            }

            if (autoSpawnEnabled)
            {
                StartCoroutine(SpawnLoop());
            }
        }

        IEnumerator SpawnLoop()
        {
            while (true)
            {
                var wait = Random.Range(config.spawnIntervalMin, config.spawnIntervalMax) * m_spawnIntervalScale;
                yield return new WaitForSeconds(wait);
                SpawnOne();
            }
        }

        /// <summary>
        /// 물품 하나를 생성합니다(에러 물류·버스트 확률 포함).
        /// </summary>
        public FactoryItem SpawnOne()
        {
            if (ShouldSpawnErrorLogistics())
            {
                return SpawnErrorLogistics(PickRandomErrorLogisticsKind());
            }

            var meshShape = RandomShape();
            var qrShape = meshShape;
            if (Random.value < config.mismatchInjectionRate)
            {
                qrShape = RandomShapeExcept(meshShape);
            }

            return SpawnExplicit(meshShape, qrShape);
        }

        bool ShouldSpawnErrorLogistics()
        {
            if (config == null)
            {
                return false;
            }

            if (Time.time < m_errorLogisticsBurstUntil)
            {
                return true;
            }

            if (Random.value < config.errorLogisticsBurstRate)
            {
                BeginErrorLogisticsBurst(config.errorLogisticsBurstDurationSeconds);
                return true;
            }

            return Random.value < config.errorLogisticsSpawnRate;
        }

        void BeginErrorLogisticsBurst(float durationSeconds)
        {
            m_errorLogisticsBurstUntil = Time.time + durationSeconds;
            FactoryStateRegistry.Instance?.SetErrorLogisticsBurstActive(true);
            if (m_burstNotifyRoutine != null)
            {
                StopCoroutine(m_burstNotifyRoutine);
            }

            m_burstNotifyRoutine = StartCoroutine(ClearBurstFlagAfter(durationSeconds));
            FactoryStateRegistry.Instance?.AddNotification(
                "spawner",
                $"에러 물류 버스트 시작({durationSeconds:F0}초간 에러 물류만 생성)",
                "urgent");
        }

        IEnumerator ClearBurstFlagAfter(float durationSeconds)
        {
            yield return new WaitForSeconds(durationSeconds);
            m_burstNotifyRoutine = null;
            if (Time.time >= m_errorLogisticsBurstUntil)
            {
                FactoryStateRegistry.Instance?.SetErrorLogisticsBurstActive(false);
            }
        }

        static ErrorLogisticsKind PickRandomErrorLogisticsKind()
        {
            var roll = Random.Range(0, 3);
            return roll switch
            {
                0 => ErrorLogisticsKind.InvalidQr,
                1 => ErrorLogisticsKind.MissingQr,
                _ => ErrorLogisticsKind.AbnormalMesh
            };
        }

        /// <summary>
        /// 에러 물류 하나를 생성합니다.
        /// </summary>
        public FactoryItem SpawnErrorLogistics(ErrorLogisticsKind kind)
        {
            var meshShape = RandomShape();
            var qrShape = meshShape;
            if (spawnPoint == null)
            {
                Debug.LogError("ItemSpawner: spawnPoint가 필요합니다.");
                return null;
            }

            var item = GetFromPool();
            item.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            item.gameObject.SetActive(true);
            var itemId = m_nextId++;
            StripLegacyRootCollider(item.gameObject);

            Transform qrLabel = null;
            switch (kind)
            {
                case ErrorLogisticsKind.MissingQr:
                    RemoveQrLabel(item.gameObject);
                    ApplyVisual(item.gameObject, meshShape);
                    break;
                case ErrorLogisticsKind.InvalidQr:
                    ApplyVisual(item.gameObject, meshShape);
                    qrLabel = AttachInvalidQrLabel(item.gameObject, itemId);
                    break;
                case ErrorLogisticsKind.AbnormalMesh:
                    ApplyAbnormalVisual(item.gameObject);
                    AttachQrLabel(item.gameObject, itemId, qrShape);
                    qrLabel = item.gameObject.transform.Find("QrLabel");
                    meshShape = ShapeType.Cube;
                    break;
                default:
                    ApplyVisual(item.gameObject, meshShape);
                    AttachQrLabel(item.gameObject, itemId, qrShape);
                    qrLabel = item.gameObject.transform.Find("QrLabel");
                    break;
            }

            item.Initialize(itemId, meshShape, qrShape, qrLabel, kind);
            ApplyLogisticsMetadata(item, meshShape, qrShape);
            return item;
        }

        /// <summary>
        /// 메시 형태와 QR 형태를 지정해 물품 하나를 생성합니다.
        /// </summary>
        /// <param name="meshShape">물류 본체 도형.</param>
        /// <param name="qrShape">QR 페이로드 도형.</param>
        public FactoryItem SpawnExplicit(ShapeType meshShape, ShapeType qrShape)
        {
            if (spawnPoint == null)
            {
                Debug.LogError("ItemSpawner: spawnPoint가 필요합니다.");
                return null;
            }

            var item = GetFromPool();
            item.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            item.gameObject.SetActive(true);
            var itemId = m_nextId++;
            StripLegacyRootCollider(item.gameObject);
            ApplyVisual(item.gameObject, meshShape);
            AttachQrLabel(item.gameObject, itemId, qrShape);
            item.Initialize(itemId, meshShape, qrShape, item.transform.Find("QrLabel"));
            ApplyLogisticsMetadata(item, meshShape, qrShape);
            return item;
        }

        /// <summary>혼잡 완화용 스폰 간격 배율(시뮬레이션).</summary>
        public void ApplySpawnRateScale(float scale)
        {
            m_spawnIntervalScale = Mathf.Max(1f, scale);
        }

        void ApplyLogisticsMetadata(FactoryItem item, ShapeType meshShape, ShapeType qrShape)
        {
            if (item == null || config == null)
            {
                return;
            }

            var regionSlots = Mathf.Clamp(config.regionCount, 1, 3);
            var dest = (DestinationRegion)Random.Range(0, regionSlots);
            var roll = Random.value;
            var priority = PackagePriority.Normal;
            if (roll < config.urgentPackageRate)
            {
                priority = PackagePriority.Urgent;
            }
            else if (roll < config.urgentPackageRate + config.highPriorityPackageRate)
            {
                priority = PackagePriority.High;
            }

            var accept = Time.time;
            var deadlineMinutes = Random.Range(config.deadlineMinMinutes, config.deadlineMaxMinutes);
            var deadline = accept + deadlineMinutes * 60f;
            item.ApplyLogisticsMetadata(dest, priority, accept, deadline);

            var label = item.transform.Find("QrLabel");
            if (label != null)
            {
                var payload = QrLabelGenerator.BuildPayload(item.ItemId, qrShape, dest, priority, deadline);
                var texture = QrLabelGenerator.CreateQrTexture(payload);
                var renderer = label.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.mainTexture = texture;
                    renderer.sharedMaterial = mat;
                }
            }

            FactoryLogisticsCenter.Instance?.RegisterSpawnedPackage(item);
        }

        FactoryItem GetFromPool()
        {
            if (m_pool.Count > 0)
            {
                return m_pool.Dequeue();
            }

            var go = new GameObject("FactoryItem");
            go.AddComponent<Rigidbody>();
            var item = go.AddComponent<FactoryItem>();
            return item;
        }

        /// <summary>
        /// Zone 배치 후 지연 Despawn을 예약합니다.
        /// </summary>
        public void ScheduleDespawn(FactoryItem item, float delaySeconds = FactoryItem.DefaultZoneDespawnDelaySeconds)
        {
            item?.ScheduleZoneDespawn(this, delaySeconds);
        }

        /// <summary>
        /// 물품을 풀에 반환합니다.
        /// </summary>
        public void Despawn(FactoryItem item)
        {
            if (item == null)
            {
                return;
            }

            item.Release();
            m_pool.Enqueue(item);
        }

        static ShapeType RandomShape()
        {
            return (ShapeType)Random.Range(0, 3);
        }

        static ShapeType RandomShapeExcept(ShapeType except)
        {
            ShapeType candidate;
            do
            {
                candidate = RandomShape();
            } while (candidate == except);

            return candidate;
        }

        void ApplyVisual(GameObject root, ShapeType shape)
        {
            var visual = root.transform.Find("Visual");
            if (visual != null)
            {
                Destroy(visual.gameObject);
            }

            GameObject primitive;
            switch (shape)
            {
                case ShapeType.Circle:
                    primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    primitive.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
                    if (circleMaterial != null)
                    {
                        primitive.GetComponent<Renderer>().sharedMaterial = circleMaterial;
                    }

                    break;
                case ShapeType.Triangle:
                    primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    primitive.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                    if (triangleMaterial != null)
                    {
                        primitive.GetComponent<Renderer>().sharedMaterial = triangleMaterial;
                    }

                    break;
                default:
                    primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    primitive.transform.localScale = Vector3.one * 0.35f;
                    if (cubeMaterial != null)
                    {
                        primitive.GetComponent<Renderer>().sharedMaterial = cubeMaterial;
                    }

                    break;
            }

            primitive.name = "Visual";
            primitive.transform.SetParent(root.transform, false);
        }

        void ApplyAbnormalVisual(GameObject root)
        {
            var visual = root.transform.Find("Visual");
            if (visual != null)
            {
                Destroy(visual.gameObject);
            }

            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            primitive.name = "Visual";
            primitive.transform.SetParent(root.transform, false);
            primitive.transform.localScale = new Vector3(0.55f, 0.12f, 0.28f);
            if (cubeMaterial != null)
            {
                primitive.GetComponent<Renderer>().sharedMaterial = cubeMaterial;
            }
        }

        /// <summary>
        /// 구버전 풀 항목의 루트 BoxCollider를 제거합니다(물리는 Visual 자식 콜라이더만 사용).
        /// </summary>
        static void StripLegacyRootCollider(GameObject root)
        {
            var rootCol = root.GetComponent<Collider>();
            if (rootCol != null)
            {
                Destroy(rootCol);
            }
        }

        static void RemoveQrLabel(GameObject root)
        {
            var existing = root.transform.Find("QrLabel");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        void AttachQrLabel(GameObject root, int itemId, ShapeType qrShape)
        {
            RemoveQrLabel(root);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "QrLabel";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, FactoryItem.QrLabelLocalOffset.y, 0f);
            quad.transform.localRotation =
                Quaternion.Inverse(root.transform.rotation) * FactoryItem.QrLabelWorldRotation;
            quad.transform.localScale = Vector3.one * 0.2f;
            Destroy(quad.GetComponent<Collider>());

            var payload = QrLabelGenerator.BuildPayload(itemId, qrShape);
            var texture = QrLabelGenerator.CreateQrTexture(payload);
            ApplyQrMaterial(quad, texture);
        }

        Transform AttachInvalidQrLabel(GameObject root, int itemId)
        {
            RemoveQrLabel(root);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "QrLabel";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, FactoryItem.QrLabelLocalOffset.y, 0f);
            quad.transform.localRotation =
                Quaternion.Inverse(root.transform.rotation) * FactoryItem.QrLabelWorldRotation;
            quad.transform.localScale = Vector3.one * 0.2f;
            Destroy(quad.GetComponent<Collider>());

            var payload = $"INVALID_QR_{itemId}_{Random.Range(1000, 9999)}";
            var texture = QrLabelGenerator.CreateQrTexture(payload);
            ApplyQrMaterial(quad, texture);
            return quad.transform;
        }

        static void ApplyQrMaterial(GameObject quad, Texture2D texture)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.mainTexture = texture;
            quad.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
