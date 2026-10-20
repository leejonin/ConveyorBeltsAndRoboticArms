using SmartFactory.Core;
using SmartFactory.Items;
using UnityEngine;
using ZXing;
using ZXing.Common;

namespace SmartFactory.Recognition
{
    /// <summary>
    /// 카메라 RenderTexture를 ZXing으로 판독합니다.
    /// </summary>
    public class QrScanner : MonoBehaviour
    {
        [SerializeField] Camera scanCamera;
        [SerializeField] int textureSize = 256;
        [SerializeField] float focusHeightAboveItem = 0.2f;
        [SerializeField] float cameraHeightAboveFocus = 0.75f;

        /// <summary>카메라 forward가 월드 −Y(아래)를 향하는 상부 스캔 자세.</summary>
        public static readonly Quaternion TopDownRotation =
            Quaternion.LookRotation(Vector3.down, Vector3.forward);

        RenderTexture m_rt;
        Texture2D m_readTexture;
        readonly BarcodeReaderGeneric m_reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions { TryHarder = true }
        };

        void Awake()
        {
            if (scanCamera == null)
            {
                scanCamera = GetComponent<Camera>();
            }

            m_rt = new RenderTexture(textureSize, textureSize, 16);
            m_readTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            scanCamera.targetTexture = m_rt;
        }

        void OnDestroy()
        {
            if (m_rt != null)
            {
                m_rt.Release();
            }
        }

        /// <summary>
        /// 현재 카메라 프레임에서 QR을 판독합니다.
        /// </summary>
        public QrScanResult Scan(FactoryItem item)
        {
            if (item == null)
            {
                return new QrScanResult { success = false };
            }

            AimCameraTopDown(item);
            scanCamera.Render();
            RenderTexture.active = m_rt;
            m_readTexture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            m_readTexture.Apply();
            RenderTexture.active = null;

            var result = m_reader.Decode(BuildLuminanceSource(m_readTexture.GetPixels32(), textureSize, textureSize));
            if (result == null || string.IsNullOrEmpty(result.Text))
            {
                return new QrScanResult { success = false };
            }

            return ParsePayload(result.Text);
        }

        /// <summary>
        /// 스캔 카메라를 물품 위 +Y에 두고 시선을 월드 −Y로 고정합니다(+Z 기본 자세 보정).
        /// </summary>
        void AimCameraTopDown(FactoryItem item)
        {
            var focus = item.transform.position + Vector3.up * focusHeightAboveItem;
            scanCamera.transform.SetPositionAndRotation(
                focus + Vector3.up * cameraHeightAboveFocus,
                TopDownRotation);
        }

        static RGBLuminanceSource BuildLuminanceSource(Color32[] pixels, int width, int height)
        {
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                var offset = i * 4;
                bytes[offset] = pixels[i].r;
                bytes[offset + 1] = pixels[i].g;
                bytes[offset + 2] = pixels[i].b;
                bytes[offset + 3] = pixels[i].a;
            }

            return new RGBLuminanceSource(bytes, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);
        }

        static QrScanResult ParsePayload(string text)
        {
            var scan = new QrScanResult { success = false, rawText = text };
            var shapeIndex = text.IndexOf("\"shape\":\"", System.StringComparison.Ordinal);
            if (shapeIndex < 0)
            {
                return scan;
            }

            shapeIndex += "\"shape\":\"".Length;
            var shapeEnd = text.IndexOf('"', shapeIndex);
            if (shapeEnd < 0)
            {
                return scan;
            }

            var shapeToken = text.Substring(shapeIndex, shapeEnd - shapeIndex);
            if (!System.Enum.TryParse(shapeToken, out ShapeType shape))
            {
                return scan;
            }

            scan.shape = shape;
            scan.success = true;
            var idToken = "\"id\":";
            var idIndex = text.IndexOf(idToken, System.StringComparison.Ordinal);
            if (idIndex >= 0)
            {
                idIndex += idToken.Length;
                var idEnd = text.IndexOfAny(new[] { ',', '}' }, idIndex);
                if (idEnd > idIndex && int.TryParse(text.Substring(idIndex, idEnd - idIndex), out var id))
                {
                    scan.itemId = id;
                }
            }

            var destToken = "\"dest\":\"";
            var destIndex = text.IndexOf(destToken, System.StringComparison.Ordinal);
            if (destIndex >= 0)
            {
                destIndex += destToken.Length;
                var destEnd = text.IndexOf('"', destIndex);
                if (destEnd > destIndex &&
                    System.Enum.TryParse(text.Substring(destIndex, destEnd - destIndex), out DestinationRegion dest))
                {
                    scan.destination = dest;
                }
            }

            var priToken = "\"pri\":\"";
            var priIndex = text.IndexOf(priToken, System.StringComparison.Ordinal);
            if (priIndex >= 0)
            {
                priIndex += priToken.Length;
                var priEnd = text.IndexOf('"', priIndex);
                if (priEnd > priIndex &&
                    System.Enum.TryParse(text.Substring(priIndex, priEnd - priIndex), out PackagePriority pri))
                {
                    scan.priority = pri;
                }
            }

            return scan;
        }
    }
}
