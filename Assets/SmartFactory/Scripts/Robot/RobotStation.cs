using System.Collections;
using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Logistics;
using SmartFactory.Recognition;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 작업 위치 도착 물품에 대해 QR→카메라→비교 후 집기 디스패처에 배정합니다.
    /// ChangeZone 단일 스테이션은 regionSetups로 목적지 구역 디스패처를 선택합니다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RobotStation : MonoBehaviour
    {
        [SerializeField] ConveyorPickDispatcher dispatcher;
        [SerializeField] FactoryRegionSetup[] regionSetups;
        [SerializeField] MainLineRegionRouter regionRouter;
        [SerializeField] QrScanner qrScanner;
        [SerializeField] ShapeCamera shapeCamera;
        [SerializeField] SortZoneMap zoneMap;
        [SerializeField] FactoryConfig config;
        [SerializeField] Object onnxModel;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerStay(Collider other)
        {
            var item = other.GetComponentInParent<FactoryItem>();
            if (item == null || !item.gameObject.activeInHierarchy || item.IsQueued)
            {
                return;
            }

            if (item.IsRobotPipelineActive)
            {
                return;
            }

            if (item.HasScannedSuccessfully)
            {
                return;
            }

            if (ResolvePickDispatcher(item) == null)
            {
                return;
            }

            item.IsQueued = true;
            item.SetLifecycleState(PackageLifecycleState.Scanning);
            StartCoroutine(ScanAndSchedulePick(item));
        }

        ConveyorPickDispatcher ResolvePickDispatcher(FactoryItem item)
        {
            if (dispatcher != null)
            {
                return dispatcher;
            }

            if (regionSetups == null || regionSetups.Length == 0 || item == null)
            {
                return null;
            }

            for (var i = 0; i < regionSetups.Length; i++)
            {
                var setup = regionSetups[i];
                if (setup != null && setup.Region == item.DestinationRegion)
                {
                    return setup.PickDispatcher;
                }
            }

            return null;
        }

        IEnumerator ScanAndSchedulePick(FactoryItem item)
        {
            var scanStartTime = Time.time;
            var scanStartX = item.transform.position.x;
            var outcome = ItemSortOutcome.Error;
            RecognitionErrorCode errorCode = RecognitionErrorCode.None;

            if (item.IsErrorLogistics)
            {
                FactoryStateRegistry.Instance?.RecordErrorLogisticsProcessed();
                item.SetSortOutcome(ItemSortOutcome.Error, pipelineActive: true);
                var elapsedError = Time.time - scanStartTime;
                FinishScanRouteAndPick(item, scanStartX, elapsedError, ItemSortOutcome.Error);
                yield break;
            }

            FactoryStateRegistry.Instance?.ResetConsecutiveErrorLogistics();

            var qr = qrScanner.Scan(item);
            if (!qr.success)
            {
                errorCode = RecognitionErrorCode.QrReadFail;
                FactoryStateRegistry.Instance?.IncrementRecognitionFailure();
            }
            else
            {
                shapeCamera.CaptureMasked(item);
                var classifier = new MockShapeClassifier(item);
                ShapeType modelShape;
                float confidence;
                var classified = classifier.TryClassify(shapeCamera.LastMaskedTexture, out modelShape, out confidence);
                if (onnxModel != null)
                {
                    var onnxClassifier = new OnnxShapeClassifier(onnxModel);
                    classified = onnxClassifier.TryClassify(shapeCamera.LastMaskedTexture, out modelShape, out confidence) ||
                                   classified;
                }

                if (!classified)
                {
                    errorCode = RecognitionErrorCode.UnknownType;
                    FactoryStateRegistry.Instance?.IncrementRecognitionFailure();
                }
                else if (confidence < config.classifierConfidenceThreshold)
                {
                    errorCode = RecognitionErrorCode.ClassifyLowConf;
                    FactoryStateRegistry.Instance?.IncrementRecognitionFailure();
                }
                else if (item.IsErrorLogistics)
                {
                    outcome = ItemSortOutcome.Error;
                }
                else if (!ShapeComparator.IsMatch(qr.shape, modelShape))
                {
                    errorCode = RecognitionErrorCode.LabelMismatch;
                    FactoryStateRegistry.Instance?.IncrementMismatch();
                    outcome = ItemSortOutcome.Fail;
                }
                else
                {
                    outcome = ItemSortOutcome.Pass;
                }
            }

            if (errorCode != RecognitionErrorCode.None &&
                config != null &&
                item.RetryCount < config.maxRecognitionRetries &&
                errorCode != RecognitionErrorCode.LabelMismatch)
            {
                item.IncrementRetryCount();
                item.IsQueued = false;
                item.SetRecognitionError(errorCode);
                FactoryLogisticsCenter.Instance?.RecordRecognitionError(item, errorCode, "재시도 예정");
                yield break;
            }

            if (errorCode != RecognitionErrorCode.None)
            {
                item.SetRecognitionError(errorCode);
                FactoryLogisticsCenter.Instance?.RecordRecognitionError(item, errorCode, errorCode.ToString());
            }

            if (item.IsErrorLogistics)
            {
                outcome = ItemSortOutcome.Error;
                FactoryStateRegistry.Instance?.RecordErrorLogisticsProcessed();
            }

            item.SetSortOutcome(outcome, pipelineActive: true);
            item.SetLifecycleState(PackageLifecycleState.Picking);
            var scanElapsed = Time.time - scanStartTime;
            FinishScanRouteAndPick(item, scanStartX, scanElapsed, outcome);
            yield break;
        }

        void FinishScanRouteAndPick(
            FactoryItem item,
            float scanStartX,
            float scanElapsed,
            ItemSortOutcome outcome)
        {
            item.MarkScanComplete();
            if (regionRouter != null)
            {
                regionRouter.TryRouteItem(item);
            }

            var pickX = item.transform.position.x;
            var pickDispatcher = ResolvePickDispatcher(item);
            if (pickDispatcher == null)
            {
                Debug.LogWarning("SmartFactory: 목적지 구역 PickDispatcher를 찾을 수 없습니다.");
                item.IsQueued = false;
                return;
            }

            pickDispatcher.SchedulePick(item, pickX, scanElapsed, outcome);
        }
    }
}
