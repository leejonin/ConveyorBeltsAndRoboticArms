using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartFactory.Editor
{
    /// <summary>
    /// 씬에 저장된 구 UGUI 관절 패널(RobotArmJointBarCanvas)을 삭제합니다.
    /// </summary>
    public static class RobotArmLegacyUiCleanupMenu
    {
        const string CanvasName = "RobotArmJointBarCanvas";

        [MenuItem("SmartFactory/Remove Legacy Joint Bar UI From Open Scene")]
        public static void RemoveFromOpenScene()
        {
            var removed = 0;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas == null || canvas.name != CanvasName)
                {
                    continue;
                }

                Object.DestroyImmediate(canvas.gameObject);
                removed++;
            }

            var legacy = GameObject.Find(CanvasName);
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
                removed++;
            }

            if (Object.FindFirstObjectByType<Canvas>() == null)
            {
                var eventSystem = Object.FindFirstObjectByType<EventSystem>();
                if (eventSystem != null)
                {
                    Object.DestroyImmediate(eventSystem.gameObject);
                }
            }

            var harness = Object.FindFirstObjectByType<Robot.RobotArmTestHarness>();
            if (harness != null)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(harness.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(
                "SmartFactory",
                removed > 0
                    ? $"구 UGUI 관절 UI {removed}개 삭제했습니다.\n씬 저장(Ctrl+S)하세요."
                    : "삭제할 RobotArmJointBarCanvas가 없습니다.",
                "확인");
        }

        /// <summary>
        /// 배치/CLI: RobotArmTest 씬에서 레거시 UI 제거 후 저장.
        /// </summary>
        public static void RemoveFromRobotArmTestSceneAndSave()
        {
            const string path = "Assets/SmartFactory/Scenes/RobotArmTest.unity";
            if (!System.IO.File.Exists(path))
            {
                return;
            }

            EditorSceneManager.OpenScene(path);
            RemoveFromOpenSceneSilent();
            EditorSceneManager.SaveOpenScenes();
        }

        static void RemoveFromOpenSceneSilent()
        {
            var legacy = GameObject.Find(CanvasName);
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
            }

            if (Object.FindFirstObjectByType<Canvas>() == null)
            {
                var eventSystem = Object.FindFirstObjectByType<EventSystem>();
                if (eventSystem != null)
                {
                    Object.DestroyImmediate(eventSystem.gameObject);
                }
            }

            var harness = Object.FindFirstObjectByType<Robot.RobotArmTestHarness>();
            if (harness != null)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(harness.gameObject);
            }
        }
    }
}
