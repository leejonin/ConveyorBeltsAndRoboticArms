using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Recognition
{
    /// <summary>
    /// 물품 이미지를 캡처하고 QR 영역을 마스킹합니다.
    /// </summary>
    public class ShapeCamera : MonoBehaviour
    {
        [SerializeField] Camera captureCamera;
        [SerializeField] int captureSize = 224;
        [SerializeField] Material maskMaterial;

        RenderTexture m_rt;

        public Texture2D LastMaskedTexture { get; private set; }

        void Awake()
        {
            if (captureCamera == null)
            {
                captureCamera = GetComponent<Camera>();
            }

            m_rt = new RenderTexture(captureSize, captureSize, 16);
            captureCamera.targetTexture = m_rt;
            if (maskMaterial == null)
            {
                maskMaterial = new Material(Shader.Find("SmartFactory/QrMaskBlit"));
            }
        }

        void OnDestroy()
        {
            if (m_rt != null)
            {
                m_rt.Release();
            }
        }

        /// <summary>
        /// 물품을 촬영하고 QR 영역을 마스킹한 텍스처를 반환합니다.
        /// </summary>
        public Texture2D CaptureMasked(FactoryItem item)
        {
            if (item == null)
            {
                return null;
            }

            var focus = item.transform.position + Vector3.up * 0.2f;
            captureCamera.transform.SetPositionAndRotation(
                focus + Vector3.up * 0.75f,
                QrScanner.TopDownRotation);
            captureCamera.Render();

            var raw = new Texture2D(captureSize, captureSize, TextureFormat.RGBA32, false);
            RenderTexture.active = m_rt;
            raw.ReadPixels(new Rect(0, 0, captureSize, captureSize), 0, 0);
            raw.Apply();
            RenderTexture.active = null;

            if (item.QrLabelTransform != null)
            {
                var vp = captureCamera.WorldToViewportPoint(item.QrLabelTransform.position);
                maskMaterial.SetVector("_QrMask", new Vector4(vp.x, vp.y, 0.08f, 0.08f));
            }

            var maskedRt = RenderTexture.GetTemporary(captureSize, captureSize, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(raw, maskedRt, maskMaterial);
            LastMaskedTexture = new Texture2D(captureSize, captureSize, TextureFormat.RGBA32, false);
            RenderTexture.active = maskedRt;
            LastMaskedTexture.ReadPixels(new Rect(0, 0, captureSize, captureSize), 0, 0);
            LastMaskedTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(maskedRt);
            return LastMaskedTexture;
        }
    }
}
