using SmartFactory.Core;
using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Recognition
{
    /// <summary>
    /// Teachable Machine 모델 준비 전, 물품 메시 도형을 그대로 반환하는 분류기.
    /// </summary>
    public class MockShapeClassifier : IShapeClassifier
    {
        readonly FactoryItem m_item;

        public MockShapeClassifier(FactoryItem item)
        {
            m_item = item;
        }

        /// <inheritdoc />
        public bool TryClassify(Texture2D maskedTexture, out ShapeType shape, out float confidence)
        {
            if (m_item == null)
            {
                shape = ShapeType.Cube;
                confidence = 0f;
                return false;
            }

            shape = m_item.MeshShape;
            confidence = 0.99f;
            return true;
        }
    }
}
