using SmartFactory.Core;

namespace SmartFactory.Recognition
{
    /// <summary>
    /// QR 스캔 결과.
    /// </summary>
    public struct QrScanResult
    {
        public bool success;
        public ShapeType shape;
        public int itemId;
        public string rawText;
        public DestinationRegion destination;
        public PackagePriority priority;
    }
}
