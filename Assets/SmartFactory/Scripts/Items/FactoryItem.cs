using System.Collections;
using SmartFactory.Core;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory.Items
{
    /// <summary>
    /// 컨베이어 위 물품 데이터와 QR 라벨 참조를 보관합니다.
    /// </summary>
    public class FactoryItem : MonoBehaviour
    {
        /// <summary>로봇 분류 Zone 배치 후 풀 반환까지 대기 시간(초).</summary>
        public const float DefaultZoneDespawnDelaySeconds = 2f;

        /// <summary>QR 라벨 로컬 오프셋(물품 윗면).</summary>
        public static readonly Vector3 QrLabelLocalOffset = new Vector3(0f, 0.25f, 0f);

        /// <summary>Quad 앞면(+Z)이 월드 +Y를 향하는 회전(Unity LH, FromToRotation과 반대 부호).</summary>
        public static readonly Quaternion QrLabelWorldRotation = Quaternion.Euler(90f, 0f, 0f);

        public int ItemId { get; private set; }
        public ShapeType MeshShape { get; private set; }
        public ShapeType QrShape { get; private set; }
        public Transform QrLabelTransform { get; private set; }
        public bool IsQueued { get; set; }

        /// <summary>집기에 배정된 로봇팔 인덱스(-1 미배정).</summary>
        public int AssignedArmIndex { get; set; } = -1;

        /// <summary>로봇팔에 잡혀 벨트 운반에서 제외된 상태.</summary>
        public bool IsHeldByRobot { get; set; }

        /// <summary>SchedulePick부터 Zone 배치 완료까지 로봇 파이프라인 진행 중.</summary>
        public bool IsRobotPipelineActive { get; private set; }

        /// <summary>스테이션 판정 결과(Zone 목적지).</summary>
        public ItemSortOutcome SortOutcome { get; private set; } = ItemSortOutcome.Error;

        /// <summary>로봇 FAIL Zone 배치 Despawn 시 Unprocessed 통계 중복 방지.</summary>
        public bool SuppressUnprocessedStatIncrement { get; set; }

        ErrorLogisticsKind m_errorLogisticsKind;
        DestinationRegion m_destinationRegion = DestinationRegion.A;
        PackagePriority m_priority = PackagePriority.Normal;
        PackageLifecycleState m_lifecycleState = PackageLifecycleState.Waiting;
        RecognitionErrorCode m_lastRecognitionError = RecognitionErrorCode.None;
        float m_acceptSimTime;
        float m_deadlineSimTime;
        int m_retryCount;
        bool m_routedToBranch;
        bool m_scanComplete;

        /// <summary>에러 물류 유형(스폰 주입). None이면 일반 물류.</summary>
        public ErrorLogisticsKind ErrorLogisticsKind => m_errorLogisticsKind;

        /// <summary>목적지 구역.</summary>
        public DestinationRegion DestinationRegion => m_destinationRegion;

        /// <summary>주문 우선순위.</summary>
        public PackagePriority Priority => m_priority;

        /// <summary>처리 단계.</summary>
        public PackageLifecycleState LifecycleState => m_lifecycleState;

        /// <summary>표시용 택배 ID.</summary>
        public string PackageDisplayId => $"PKG-{ItemId:D3}";

        /// <summary>접수 시각(Time.time).</summary>
        public float AcceptSimTime => m_acceptSimTime;

        /// <summary>마감 시각(Time.time).</summary>
        public float DeadlineSimTime => m_deadlineSimTime;

        /// <summary>재시도 횟수.</summary>
        public int RetryCount => m_retryCount;

        /// <summary>지선으로 이송됐는지.</summary>
        public bool HasRoutedToBranch => m_routedToBranch;

        /// <summary>ChangeZone 스캔·판정이 완료됐는지.</summary>
        public bool HasScannedSuccessfully => m_scanComplete;

        /// <summary>마지막 인식 오류 코드.</summary>
        public RecognitionErrorCode LastRecognitionError => m_lastRecognitionError;

        /// <summary>에러 물류 여부.</summary>
        public bool IsErrorLogistics => m_errorLogisticsKind != ErrorLogisticsKind.None;

        /// <summary>집기 정렬·집게 닫힘 동안 컨베이어 +X 운반을 억제합니다.</summary>
        public bool IsPickBeltStabilized { get; private set; }

        Rigidbody m_rigidbody;
        bool m_beltHeldSkip;
        Coroutine m_zoneDespawnRoutine;

        /// <summary>Zone 배치 후 지연 Despawn이 예약됐는지(UnprocessedZone 즉시 회수 제외용).</summary>
        public bool IsZoneDespawnScheduled { get; private set; }

        /// <summary>직전 FixedUpdate에서 벨트가 +X 운반을 건너뛰었는지(held 또는 미접촉).</summary>
        public bool BeltHeldSkipLastStep => m_beltHeldSkip;

        /// <summary>
        /// ConveyorBelt가 이 FixedUpdate에서 속도를 적용했는지 기록합니다.
        /// </summary>
        internal void SetBeltTransportSkippedThisFixedStep(bool skipped)
        {
            m_beltHeldSkip = skipped;
        }

        /// <summary>물류 Rigidbody.</summary>
        public Rigidbody ItemRigidbody => m_rigidbody;

        /// <summary>
        /// 집기 정렬·TryGrab 판정에 쓰는 월드 좌표(Visual primitive Collider bounds 중심, 없으면 pivot).
        /// </summary>
        public Vector3 PickAlignWorldPosition
        {
            get
            {
                var col = GetBodyCollider();
                return col != null ? col.bounds.center : transform.position;
            }
        }

        /// <summary>
        /// 본체 Visual의 물리 Collider(QrLabel 제외).
        /// </summary>
        Collider GetBodyCollider()
        {
            var visual = transform.Find("Visual");
            if (visual != null)
            {
                var visualCol = visual.GetComponent<Collider>();
                if (visualCol != null)
                {
                    return visualCol;
                }
            }

            var cols = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null)
                {
                    continue;
                }

                if (QrLabelTransform != null && c.transform.IsChildOf(QrLabelTransform))
                {
                    continue;
                }

                if (c.transform.name == "QrLabel")
                {
                    continue;
                }

                return c;
            }

            return null;
        }

        /// <summary>집기 정렬 타깃( <see cref="PickAlignWorldPosition"/> 와 동일).</summary>
        public Vector3 PickAlignPoint => PickAlignWorldPosition;

        /// <summary>
        /// Visual 본체 Collider bounds (없으면 null).
        /// </summary>
        public Collider BodyCollider => GetBodyCollider();

        /// <summary>
        /// 스테이션 판정·파이프라인 상태를 설정합니다.
        /// </summary>
        public void SetSortOutcome(ItemSortOutcome outcome, bool pipelineActive)
        {
            SortOutcome = outcome;
            IsRobotPipelineActive = pipelineActive;
        }

        /// <summary>
        /// 로봇 파이프라인 종료(배치 완료 또는 실패).
        /// </summary>
        public void ClearRobotPipeline()
        {
            IsRobotPipelineActive = false;
            IsPickBeltStabilized = false;
        }

        /// <summary>
        /// 집기 구간에서 벨트가 물류 X 속도를 덮어쓰지 않도록 합니다.
        /// </summary>
        public void SetPickBeltStabilized(bool active)
        {
            IsPickBeltStabilized = active;
        }

        /// <summary>
        /// 물품을 초기화합니다.
        /// </summary>
        public void Initialize(
            int itemId,
            ShapeType meshShape,
            ShapeType qrShape,
            Transform qrLabel,
            ErrorLogisticsKind errorLogisticsKind = ErrorLogisticsKind.None)
        {
            ItemId = itemId;
            MeshShape = meshShape;
            QrShape = qrShape;
            QrLabelTransform = qrLabel;
            m_errorLogisticsKind = errorLogisticsKind;
            m_rigidbody = GetComponent<Rigidbody>();
            if (m_rigidbody == null)
            {
                m_rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            m_rigidbody.isKinematic = false;
            m_rigidbody.useGravity = true;
            m_rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            m_rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            m_rigidbody.linearVelocity = Vector3.zero;
            m_rigidbody.angularVelocity = Vector3.zero;
            IsQueued = false;
            AssignedArmIndex = -1;
            IsHeldByRobot = false;
            IsRobotPipelineActive = false;
            IsPickBeltStabilized = false;
            SortOutcome = ItemSortOutcome.Error;
            CancelZoneDespawnSchedule();
            ApplyQrLabelWorldUp();
            m_destinationRegion = DestinationRegion.A;
            m_priority = PackagePriority.Normal;
            m_lifecycleState = PackageLifecycleState.Waiting;
            m_lastRecognitionError = RecognitionErrorCode.None;
            m_acceptSimTime = 0f;
            m_deadlineSimTime = 0f;
            m_retryCount = 0;
            m_routedToBranch = false;
            m_scanComplete = false;
        }

        /// <summary>스폰 시 택배 메타데이터를 설정합니다.</summary>
        public void ApplyLogisticsMetadata(
            DestinationRegion destination,
            PackagePriority priority,
            float acceptSimTime,
            float deadlineSimTime)
        {
            m_destinationRegion = destination;
            m_priority = priority;
            m_acceptSimTime = acceptSimTime;
            m_deadlineSimTime = deadlineSimTime;
            m_lifecycleState = PackageLifecycleState.OnMainLine;
        }

        /// <summary>처리 단계를 변경합니다.</summary>
        public void SetLifecycleState(PackageLifecycleState state)
        {
            m_lifecycleState = state;
        }

        /// <summary>지선 이송 완료 표시.</summary>
        public void MarkRoutedToBranch()
        {
            m_routedToBranch = true;
        }

        /// <summary>ChangeZone 스캔·판정 완료 표시.</summary>
        public void MarkScanComplete()
        {
            m_scanComplete = true;
        }

        /// <summary>인식 재시도 카운터 증가.</summary>
        public void IncrementRetryCount()
        {
            m_retryCount++;
        }

        /// <summary>인식 오류 코드 기록.</summary>
        public void SetRecognitionError(RecognitionErrorCode code)
        {
            m_lastRecognitionError = code;
        }

        /// <summary>마감까지 남은 분(없으면 큰 음수).</summary>
        public float MinutesToDeadline(float nowSimTime)
        {
            if (m_deadlineSimTime <= 0f)
            {
                return -1000f;
            }

            return (m_deadlineSimTime - nowSimTime) / 60f;
        }

        void LateUpdate()
        {
            ApplyQrLabelWorldUp();
        }

        void OnCollisionEnter(Collision collision)
        {
            ForwardGanchoContactToProbe(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            ForwardGanchoContactToProbe(collision);
        }

        /// <summary>
        /// 물류 Rigidbody 쪽 물리 콜백을 Gancho_end 프로브에 전달합니다(AND 접촉 누적).
        /// </summary>
        void ForwardGanchoContactToProbe(Collision collision)
        {
            if (!IsPickBeltStabilized || collision == null)
            {
                return;
            }

            var probe = collision.collider.GetComponent<RobotArmGanchoContactProbe>();
            probe?.RegisterBodyContactFromItem(this, collision);
        }

        /// <summary>
        /// QR 라벨 법선을 항상 월드 +Y로 유지합니다(상부 스캐너·ZXing용).
        /// </summary>
        void ApplyQrLabelWorldUp()
        {
            if (QrLabelTransform == null)
            {
                return;
            }

            QrLabelTransform.position = transform.TransformPoint(QrLabelLocalOffset);
            QrLabelTransform.rotation = QrLabelWorldRotation;
        }

        /// <summary>
        /// 흡착 해제·Zone 내려놓기 후 트리거 Zone과 충돌하도록 Visual Collider·Rigidbody를 복원합니다.
        /// </summary>
        public void RestoreWorldPhysics()
        {
            var body = BodyCollider;
            if (body != null)
            {
                body.isTrigger = false;
            }

            if (m_rigidbody == null)
            {
                m_rigidbody = GetComponent<Rigidbody>();
            }

            if (m_rigidbody != null)
            {
                m_rigidbody.isKinematic = false;
            }
        }

        /// <summary>
        /// 분류 Zone에 놓인 뒤 일정 시간 후 <see cref="ItemSpawner.Despawn"/>을 예약합니다.
        /// </summary>
        /// <param name="spawner">풀 반환 대상.</param>
        /// <param name="delaySeconds">대기 시간(초). 0 이하면 <see cref="DefaultZoneDespawnDelaySeconds"/>.</param>
        public void ScheduleZoneDespawn(ItemSpawner spawner, float delaySeconds = -1f)
        {
            CancelZoneDespawnSchedule();
            if (spawner == null)
            {
                return;
            }

            var delay = delaySeconds > 0f ? delaySeconds : DefaultZoneDespawnDelaySeconds;
            IsZoneDespawnScheduled = true;
            HoldItemOnZoneUntilDespawn();
            m_zoneDespawnRoutine = StartCoroutine(ZoneDespawnRoutine(spawner, delay));
        }

        /// <summary>Zone에 놓인 동안 미끄러지지 않도록 kinematic으로 고정합니다.</summary>
        void HoldItemOnZoneUntilDespawn()
        {
            if (m_rigidbody == null)
            {
                m_rigidbody = GetComponent<Rigidbody>();
            }

            if (m_rigidbody == null)
            {
                return;
            }

            m_rigidbody.isKinematic = true;
            m_rigidbody.linearVelocity = Vector3.zero;
            m_rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>예약된 Zone Despawn 코루틴을 취소합니다.</summary>
        public void CancelZoneDespawnSchedule()
        {
            if (m_zoneDespawnRoutine != null)
            {
                StopCoroutine(m_zoneDespawnRoutine);
                m_zoneDespawnRoutine = null;
            }

            IsZoneDespawnScheduled = false;
        }

        IEnumerator ZoneDespawnRoutine(ItemSpawner spawner, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            m_zoneDespawnRoutine = null;
            IsZoneDespawnScheduled = false;
            if (gameObject.activeInHierarchy && spawner != null)
            {
                spawner.Despawn(this);
            }
        }

        /// <summary>
        /// 물품을 풀에 반환하기 위해 비활성화합니다.
        /// </summary>
        public void Release()
        {
            CancelZoneDespawnSchedule();
            AssignedArmIndex = -1;
            IsHeldByRobot = false;
            IsRobotPipelineActive = false;
            IsPickBeltStabilized = false;
            IsQueued = false;
            SuppressUnprocessedStatIncrement = false;
            m_errorLogisticsKind = ErrorLogisticsKind.None;
            m_routedToBranch = false;
            m_scanComplete = false;
            m_retryCount = 0;
            m_lifecycleState = PackageLifecycleState.Waiting;
            m_lastRecognitionError = RecognitionErrorCode.None;
            transform.SetParent(null, true);
            if (m_rigidbody == null)
            {
                m_rigidbody = GetComponent<Rigidbody>();
            }

            if (m_rigidbody != null)
            {
                m_rigidbody.isKinematic = false;
                m_rigidbody.linearVelocity = Vector3.zero;
                m_rigidbody.angularVelocity = Vector3.zero;
            }

            gameObject.SetActive(false);
        }
    }
}
