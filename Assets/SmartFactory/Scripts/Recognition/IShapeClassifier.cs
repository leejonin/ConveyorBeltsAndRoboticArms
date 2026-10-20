using SmartFactory.Core;
using UnityEngine;

namespace SmartFactory.Recognition
{
    /// <summary>
    /// QR 마스킹된 이미지에서 도형을 분류합니다.
    /// </summary>
    public interface IShapeClassifier
    {
        /// <summary>
        /// 마스킹된 텍스처에서 도형을 추론합니다.
        /// </summary>
        /// <param name="maskedTexture">QR 영역이 가려진 입력 이미지.</param>
        /// <param name="shape">인식된 도형.</param>
        /// <param name="confidence">신뢰도(0~1).</param>
        /// <returns>인식 성공 여부.</returns>
        bool TryClassify(Texture2D maskedTexture, out ShapeType shape, out float confidence);
    }
}
