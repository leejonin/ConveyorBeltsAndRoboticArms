using SmartFactory.Core;
using SmartFactory.Robot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SmartFactory.Editor
{
    /// <summary>
    /// 로봇팔 단독 테스트 씬과 제어 패널을 생성합니다.
    /// </summary>
    public static class RobotArmTestSceneMenu
    {
        const string ScenePath = "Assets/SmartFactory/Scenes/RobotArmTest.unity";
        const string ConfigPath = "Assets/FactoryConfig.asset";

        [MenuItem("SmartFactory/Create Robot Arm Test Scene")]
        static void CreateRobotArmTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var config = AssetDatabase.LoadAssetAtPath<FactoryConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FactoryConfig>();
            }

            if (config.robotArmPrefab == null)
            {
                config.robotArmPrefab = Resources.Load<GameObject>("SmartFactory/RobotArm_Rig");
            }

            if (config.robotArmRigBinding == null)
            {
                config.robotArmRigBinding =
                    AssetDatabase.LoadAssetAtPath<RobotArmRigBinding>("Assets/RobotArm/RoboticArmRigBinding.asset");
            }

            if (config.robotArmPoseSet == null)
            {
                config.robotArmPoseSet =
                    AssetDatabase.LoadAssetAtPath<RobotArmPoseSet>("Assets/RobotArm/RobotArmDefaultPoseSet.asset");
            }

            CreateGround();
            CreateLighting();
            CreateCamera();

            var root = new GameObject("RobotArmTestRoot");
            RobotArmController arm;
            if (config.robotArmPrefab != null)
            {
                arm = RobotArmController.CreateFromPrefab(
                    root.transform,
                    config.robotArmPrefab,
                    Vector3.zero,
                    0,
                    config.robotArmRigBinding,
                    config.robotArmPoseSet);
            }
            else
            {
                arm = RobotArmController.CreateRuntimeArm(root.transform, Vector3.zero, 0, config.robotArmPoseSet);
                if (config.robotArmRigBinding != null)
                {
                    arm.SetupFromBinding(config.robotArmRigBinding);
                }
            }

            arm.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var harnessGo = new GameObject("RobotArmTestHarness");
            var harness = harnessGo.AddComponent<RobotArmTestHarness>();
            harness.SetTarget(arm);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "DropTargetMarker";
            marker.transform.position = new Vector3(0.6f, 0.15f, 0.4f);
            marker.transform.localScale = Vector3.one * 0.12f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.2f, 0.85f, 0.35f);
                markerRenderer.sharedMaterial = mat;
            }

            EnsureSceneFolder();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "SmartFactory",
                $"로봇팔 테스트 씬 저장:\n{ScenePath}\n\nPlay 후 F1: 포즈·관절 슬라이더 패널.",
                "확인");
        }

        [MenuItem("SmartFactory/Add Test Harness To Selected Arm")]
        static void AddHarnessToSelection()
        {
            var arm = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<RobotArmController>()
                : null;
            if (arm == null)
            {
                EditorUtility.DisplayDialog("SmartFactory", "RobotArmController가 있는 오브젝트를 선택하세요.", "확인");
                return;
            }

            var existing = Object.FindFirstObjectByType<RobotArmTestHarness>();
            if (existing != null)
            {
                existing.SetTarget(arm);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var harnessGo = new GameObject("RobotArmTestHarness");
            var harness = harnessGo.AddComponent<RobotArmTestHarness>();
            harness.SetTarget(arm);
            EditorSceneManager.MarkSceneDirty(arm.gameObject.scene);
            Selection.activeGameObject = harnessGo;
        }

        static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SmartFactory/Scenes"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/SmartFactory"))
                {
                    AssetDatabase.CreateFolder("Assets", "SmartFactory");
                }

                AssetDatabase.CreateFolder("Assets/SmartFactory", "Scenes");
            }
        }

        static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
        }

        static void CreateLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void CreateCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0.8f, 1.4f, -1.6f);
            camGo.transform.rotation = Quaternion.Euler(18f, 35f, 0f);
            cam.clearFlags = CameraClearFlags.Skybox;
        }
    }
}
