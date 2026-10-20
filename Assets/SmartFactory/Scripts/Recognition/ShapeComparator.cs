using SmartFactory.Core;

namespace SmartFactory.Recognition
{
    /// <summary>
    /// QR 도형 정보와 모델 인식 결과를 비교합니다.
    /// </summary>
    public static class ShapeComparator
    {
        /// <summary>
        /// 두 도형 정보가 일치하는지 판단합니다.
        /// </summary>
        public static bool IsMatch(ShapeType qrShape, ShapeType modelShape)
        {
            return qrShape == modelShape;
        }
    }
}
