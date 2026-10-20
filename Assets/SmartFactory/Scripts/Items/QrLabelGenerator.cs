using SmartFactory.Core;
using UnityEngine;
using ZXing;
using ZXing.QrCode;

namespace SmartFactory.Items
{
    /// <summary>
    /// ZXing으로 도형 정보 QR 텍스처를 생성·캐싱합니다.
    /// </summary>
    public static class QrLabelGenerator
    {
        /// <summary>
        /// QR 페이로드 문자열을 생성합니다.
        /// </summary>
        public static string BuildPayload(int itemId, ShapeType shape)
        {
            return BuildPayload(itemId, shape, DestinationRegion.A, PackagePriority.Normal, 0f);
        }

        /// <summary>목적지·우선순위·마감 시각을 포함한 QR 페이로드.</summary>
        public static string BuildPayload(
            int itemId,
            ShapeType shape,
            DestinationRegion destination,
            PackagePriority priority,
            float deadlineSimTime)
        {
            return
                "{\"shape\":\"" + shape +
                "\",\"id\":" + itemId +
                ",\"dest\":\"" + destination +
                "\",\"pri\":\"" + priority +
                "\",\"ddl\":" + deadlineSimTime.ToString("F1") + "}";
        }

        /// <summary>
        /// 페이로드로 QR 텍스처를 생성합니다.
        /// </summary>
        public static Texture2D CreateQrTexture(string payload)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Height = 256,
                    Width = 256,
                    Margin = 1
                }
            };

            var pixelData = writer.Write(payload);
            var texture = new Texture2D(pixelData.Width, pixelData.Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(ToColor32(pixelData));
            texture.Apply();
            return texture;
        }

        static Color32[] ToColor32(ZXing.Rendering.PixelData pixelData)
        {
            var colors = new Color32[pixelData.Width * pixelData.Height];
            var raw = pixelData.Pixels;
            for (var i = 0; i < colors.Length; i++)
            {
                var offset = i * 4;
                colors[i] = new Color32(raw[offset], raw[offset + 1], raw[offset + 2], raw[offset + 3]);
            }

            return colors;
        }
    }
}
