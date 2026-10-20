using SmartFactory.Core;
using SmartFactory.Recognition;

namespace SmartFactory.Conveyor
{
    /// <summary>
    /// 컨베이어 테스트 씬 QR·Mock 비전 스캔 결과.
    /// </summary>
    public struct ConveyorScanReport
    {
        public int itemId;
        public ShapeType expectedMeshShape;
        public ShapeType expectedQrShape;
        public QrScanResult qrScan;
        public ShapeType mockVisionShape;
        public float mockVisionConfidence;
        public bool mockVisionClassified;
        public bool qrMatchesMockVision;
        public bool qrMatchesMeshShape;
    }
}
