using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Recognition;
using UnityEngine;

namespace SmartFactory.Conveyor
{
    /// <summary>
    /// QR 스캔·Mock 비전 분류만 수행합니다(로봇팔·분류 없음).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ConveyorQrScanZone : MonoBehaviour
    {
        [SerializeField] QrScanner qrScanner;
        [SerializeField] ShapeCamera shapeCamera;
        [SerializeField] ConveyorTestHarness resultDisplay;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            var item = other.GetComponentInParent<FactoryItem>();
            if (item == null || !item.gameObject.activeInHierarchy || item.IsQueued)
            {
                return;
            }

            ProcessItem(item);
        }

        void ProcessItem(FactoryItem item)
        {
            item.IsQueued = true;

            var qr = qrScanner != null
                ? qrScanner.Scan(item)
                : new QrScanResult { success = false };

            var mockClassified = false;
            ShapeType mockShape = default;
            var mockConfidence = 0f;
            if (shapeCamera != null)
            {
                shapeCamera.CaptureMasked(item);
                var classifier = new MockShapeClassifier(item);
                mockClassified = classifier.TryClassify(shapeCamera.LastMaskedTexture, out mockShape, out mockConfidence);
            }

            var qrVsMockMatch = qr.success && mockClassified && ShapeComparator.IsMatch(qr.shape, mockShape);
            var qrVsMeshMatch = qr.success && ShapeComparator.IsMatch(qr.shape, item.MeshShape);

            if (resultDisplay != null)
            {
                resultDisplay.ReportScan(new ConveyorScanReport
                {
                    itemId = item.ItemId,
                    expectedMeshShape = item.MeshShape,
                    expectedQrShape = item.QrShape,
                    qrScan = qr,
                    mockVisionShape = mockShape,
                    mockVisionConfidence = mockConfidence,
                    mockVisionClassified = mockClassified,
                    qrMatchesMockVision = qrVsMockMatch,
                    qrMatchesMeshShape = qrVsMeshMatch
                });
            }
        }

        /// <summary>
        /// 결과 UI 및 설정을 연결합니다.
        /// </summary>
        public void Configure(QrScanner scanner, ShapeCamera camera, ConveyorTestHarness harness)
        {
            qrScanner = scanner;
            shapeCamera = camera;
            resultDisplay = harness;
        }
    }
}
