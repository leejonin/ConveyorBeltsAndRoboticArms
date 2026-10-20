using SmartFactory.Core;
using UnityEngine;

namespace SmartFactory.Recognition
{
    /// <summary>
    /// ONNX(Teachable Machine 변환) 모델로 도형을 분류합니다.
    /// Unity Inference Engine 패키지가 준비되면 ModelAsset을 연결해 확장합니다.
    /// </summary>
    public class OnnxShapeClassifier : IShapeClassifier
    {
        readonly Object m_modelAsset;

        /// <summary>
        /// ONNX 모델 에셋 참조로 분류기를 생성합니다.
        /// </summary>
        public OnnxShapeClassifier(Object modelAsset)
        {
            m_modelAsset = modelAsset;
        }

        /// <inheritdoc />
        public bool TryClassify(Texture2D maskedTexture, out ShapeType shape, out float confidence)
        {
            shape = ShapeType.Cube;
            confidence = 0f;
            if (m_modelAsset == null || maskedTexture == null)
            {
                return false;
            }

            // Inference Engine API 연동은 모델 에셋(.onnx) Import 후 확장합니다.
            return false;
        }
    }
}
