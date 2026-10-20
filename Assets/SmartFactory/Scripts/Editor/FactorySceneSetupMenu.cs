using SmartFactory;
using SmartFactory.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartFactory.Editor
{
    /// <summary>
    /// Factory 씬에 SmartFactory 루트·레이아웃·오케스트레이터를 배치합니다.
    /// </summary>
    public static class FactorySceneSetupMenu
    {
        const string OrchestratorMenu = "SmartFactory/Setup Factory Scene Orchestrator";
        const string BuildLayoutMenu = "SmartFactory/Build Factory Layout In Scene";
        const string RebuildFactoryMenu = "SmartFactory/Rebuild Factory Scene (RobotArm Prefab)";
        const string FactoryScenePath = "Assets/SmartFactory/Scenes/Factory.unity";
        const string FactoryConfigPath = "Assets/FactoryConfig.asset";
        const string RobotArmPrefabPath = "Assets/RobotArm/Prefabs/RobotArm.prefab";

        [MenuItem(OrchestratorMenu)]
        static void SetupOrchestrator()
        {
            var existing = Object.FindFirstObjectByType<FactoryOrchestrator>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("SmartFactory", "FactoryOrchestrator가 이미 있습니다.", "확인");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var go = new GameObject("SmartFactory");
            go.AddComponent<FactoryOrchestrator>();
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }

        [MenuItem(BuildLayoutMenu)]
        static void BuildLayoutInScene()
        {
            BuildLayoutInSceneInternal(showDialogs: true);
        }

        [MenuItem(RebuildFactoryMenu)]
        static void RebuildFactorySceneWithRobotArmPrefab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(FactoryScenePath, OpenSceneMode.Single);
            RemoveOrphanedFactoryRootObjects();
            BuildLayoutInSceneInternal(showDialogs: true);
        }

        /// <summary>
        /// 배치 모드: Factory 씬을 RobotArm.prefab 레이아웃으로 재생성·저장.
        /// </summary>
        public static void RebuildFactorySceneBatch()
        {
            EditorSceneManager.OpenScene(FactoryScenePath, OpenSceneMode.Single);
            RemoveOrphanedFactoryRootObjects();
            BuildLayoutInSceneInternal(showDialogs: false);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), FactoryScenePath);
            AssetDatabase.SaveAssets();
        }

        static void BuildLayoutInSceneInternal(bool showDialogs)
        {
            var orchestrator = Object.FindFirstObjectByType<FactoryOrchestrator>();
            if (orchestrator == null)
            {
                var go = new GameObject("SmartFactory");
                orchestrator = go.AddComponent<FactoryOrchestrator>();
            }

            var so = new SerializedObject(orchestrator);
            var config = so.FindProperty("config").objectReferenceValue as FactoryConfig;
            if (config == null)
            {
                config = AssetDatabase.LoadAssetAtPath<FactoryConfig>(FactoryConfigPath);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<FactoryConfig>();
                    var path = EditorUtility.SaveFilePanelInProject(
                        "Factory Config 저장",
                        "FactoryConfig",
                        "asset",
                        "공장 설정 ScriptableObject 경로");
                    if (string.IsNullOrEmpty(path))
                    {
                        Object.DestroyImmediate(config);
                        return;
                    }

                    AssetDatabase.CreateAsset(config, path);
                }

                so.FindProperty("config").objectReferenceValue = config;
            }

            AssignRobotArmPrefabToConfig(config);

            var existingLayout = orchestrator.GetComponentInChildren<FactorySceneLayout>(true);
            if (existingLayout != null)
            {
                if (showDialogs && !EditorUtility.DisplayDialog(
                        "SmartFactory",
                        "기존 FactoryLayout을 삭제하고 RobotArm.prefab 기준으로 새로 만듭니다. 계속할까요?",
                        "예",
                        "아니오"))
                {
                    return;
                }

                Object.DestroyImmediate(existingLayout.gameObject);
            }

            var layout = FactoryLayoutBuilder.Build(orchestrator.transform, config);
            so.FindProperty("sceneLayout").objectReferenceValue = layout;
            so.FindProperty("generateLayoutAtPlay").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(orchestrator.gameObject.scene);
            Selection.activeGameObject = layout.gameObject;
            if (showDialogs)
            {
                EditorUtility.DisplayDialog(
                    "SmartFactory",
                    $"FactoryLayout 생성 완료.\n로봇팔: {RobotArmPrefabPath}\n씬 저장(Ctrl+S) 후 Play.",
                    "확인");
            }
        }

        static void AssignRobotArmPrefabToConfig(FactoryConfig config)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotArmPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"SmartFactory: RobotArm 프리팹을 찾을 수 없습니다. {RobotArmPrefabPath}");
                return;
            }

            config.robotArmPrefab = prefab;
            EditorUtility.SetDirty(config);
        }

        /// <summary>
        /// FactoryLayout 밖에 남은 구 Zone·로봇팔 루트 오브젝트 제거.
        /// </summary>
        static void RemoveOrphanedFactoryRootObjects()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name is "NormalZone" or "ErrorZone" or "UnprocessedZone" or "RobotArm")
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}
