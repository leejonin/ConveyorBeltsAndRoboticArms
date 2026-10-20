using SmartFactory;
using SmartFactory.Conveyor;
using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Recognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SmartFactory.Editor
{
    /// <summary>
    /// 컨베이어·물류·QR 스캔 전용 테스트 씬을 생성합니다(로봇팔 없음).
    /// </summary>
    public static class ConveyorTestSceneMenu
    {
        const string ScenePath = "Assets/SmartFactory/Scenes/ConveyorTest.unity";
        const string ConfigPath = "Assets/FactoryConfig.asset";

        [MenuItem("SmartFactory/Create Conveyor Test Scene")]
        public static void CreateConveyorTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildConveyorTestScene();
            EditorUtility.DisplayDialog(
                "SmartFactory",
                $"컨베이어 테스트 씬 저장:\n{ScenePath}\n\nPlay → F1: 물류/QR 선택·생성, QrScanZone에서 QR·Mock 결과 확인.\n로봇팔·Orchestrator 없음.",
                "확인");
        }

        /// <summary>
        /// 배치 모드(-executeMethod)용. 다이얼로그·현재 씬 저장 확인 없이 씬만 생성합니다.
        /// </summary>
        public static void CreateConveyorTestSceneBatch()
        {
            BuildConveyorTestScene();
        }

        static void BuildConveyorTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = AssetDatabase.LoadAssetAtPath<FactoryConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FactoryConfig>();
            }

            CreateGround();
            CreateLighting();
            CreateCamera();

            var root = new GameObject("ConveyorTestRoot");

            var beltGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beltGo.name = "ConveyorBelt";
            beltGo.transform.SetParent(root.transform, false);
            beltGo.transform.localScale = new Vector3(config.beltLength, 0.2f, 1.2f);
            beltGo.transform.position = new Vector3(0f, 0.1f, 0f);
            var belt = beltGo.AddComponent<ConveyorBelt>();
            belt.SetSpeed(config.beltSpeed);

            var spawnPoint = new GameObject("SpawnPoint").transform;
            spawnPoint.SetParent(root.transform, false);
            spawnPoint.position = new Vector3(-config.beltLength * 0.45f, 0.35f, 0f);

            var spawnerGo = new GameObject("ItemSpawner");
            spawnerGo.transform.SetParent(root.transform, false);
            var spawner = spawnerGo.AddComponent<ItemSpawner>();
            FactoryLayoutBuilder.SetPrivateField(spawner, "config", config);
            FactoryLayoutBuilder.SetPrivateField(spawner, "spawnPoint", spawnPoint);
            FactoryLayoutBuilder.SetPrivateField(spawner, "autoSpawnEnabled", false);

            var harnessGo = new GameObject("ConveyorTestHarness");
            harnessGo.transform.SetParent(root.transform, false);
            var harness = harnessGo.AddComponent<ConveyorTestHarness>();
            harness.SetSpawner(spawner);

            var zoneGo = new GameObject("QrScanZone");
            zoneGo.transform.SetParent(root.transform, false);
            zoneGo.transform.position = new Vector3(config.beltLength * 0.15f, 0.5f, 0f);
            var zoneCol = zoneGo.AddComponent<BoxCollider>();
            zoneCol.isTrigger = true;
            zoneCol.size = new Vector3(1.4f, 1f, 1.4f);
            var scanZone = zoneGo.AddComponent<ConveyorQrScanZone>();

            var qrGo = new GameObject("QrScannerCamera");
            qrGo.transform.SetParent(zoneGo.transform, false);
            qrGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            qrGo.transform.localRotation = QrScanner.TopDownRotation;
            var qrCam = qrGo.AddComponent<Camera>();
            qrCam.enabled = false;
            var qrScanner = qrGo.AddComponent<QrScanner>();
            FactoryLayoutBuilder.SetPrivateField(qrScanner, "scanCamera", qrCam);

            var shapeGo = new GameObject("ShapeCamera");
            shapeGo.transform.SetParent(zoneGo.transform, false);
            shapeGo.transform.localPosition = new Vector3(0.3f, 1.2f, -0.6f);
            var shapeCam = shapeGo.AddComponent<Camera>();
            shapeCam.enabled = false;
            var shapeCamera = shapeGo.AddComponent<ShapeCamera>();
            FactoryLayoutBuilder.SetPrivateField(shapeCamera, "captureCamera", shapeCam);

            scanZone.Configure(qrScanner, shapeCamera, harness);

            EnsureSceneFolder();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
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
            camGo.transform.position = new Vector3(-2f, 2.5f, -4f);
            camGo.transform.rotation = Quaternion.Euler(22f, 25f, 0f);
        }
    }
}
