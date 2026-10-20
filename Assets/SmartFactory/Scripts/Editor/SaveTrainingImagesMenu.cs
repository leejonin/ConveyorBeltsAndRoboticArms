using System.IO;
using SmartFactory.Recognition;
using UnityEditor;
using UnityEngine;

namespace SmartFactory.Editor
{
    /// <summary>
    /// ShapeCamera 마스킹 이미지를 Teachable Machine 학습용으로 저장합니다.
    /// </summary>
    public static class SaveTrainingImagesMenu
    {
        const string MenuPath = "SmartFactory/Save Masked Capture From Selected ShapeCamera";

        [MenuItem(MenuPath)]
        static void SaveFromSelected()
        {
            var camera = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<ShapeCamera>()
                : null;
            if (camera == null || camera.LastMaskedTexture == null)
            {
                EditorUtility.DisplayDialog("SmartFactory", "ShapeCamera와 LastMaskedTexture가 필요합니다.", "확인");
                return;
            }

            var folder = EditorUtility.SaveFolderPanel("학습 이미지 저장 폴더", Application.dataPath, "ShapeTraining");
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            var bytes = camera.LastMaskedTexture.EncodeToPNG();
            var file = Path.Combine(folder, $"masked_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(file, bytes);
            EditorUtility.DisplayDialog("SmartFactory", $"저장 완료: {file}", "확인");
        }
    }
}
