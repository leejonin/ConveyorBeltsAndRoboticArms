using System.Collections.Generic;
using SmartFactory.Conveyor;
using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Recognition;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory
{
    /// <summary>
    /// 공장 기본 레이아웃 생성(에디터 씬 저장 / Play 시 런타임 생성).
    /// </summary>
    public static class FactoryLayoutBuilder
    {
        /// <summary>
        /// FactoryConfig 값으로 기본 공장 계층을 만듭니다.
        /// </summary>
        /// <param name="parent">FactoryRoot 부모.</param>
        /// <param name="config">설정.</param>
        /// <returns>생성된 레이아웃 참조.</returns>
        public static FactorySceneLayout Build(Transform parent, FactoryConfig config)
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FactoryConfig>();
            }

            if (config.useThreeRegionLogistics)
            {
                return FactoryThreeRegionLayoutBuilder.Build(parent, config);
            }

            if (config.robotArmPrefab == null)
            {
                config.robotArmPrefab = Resources.Load<GameObject>("SmartFactory/RobotArm");
            }

            if (config.robotArmPrefab == null)
            {
                config.robotArmPrefab = Resources.Load<GameObject>("SmartFactory/RobotArm_Rig");
            }

            var layoutGo = new GameObject("FactoryLayout");
            layoutGo.transform.SetParent(parent, false);
            var layout = layoutGo.AddComponent<FactorySceneLayout>();

            var root = new GameObject("FactoryRoot").transform;
            root.SetParent(layoutGo.transform, false);

            var beltGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beltGo.name = "ConveyorBelt";
            beltGo.transform.SetParent(root, false);
            beltGo.transform.localScale = new Vector3(config.beltLength, 0.2f, 1.2f);
            beltGo.transform.position = new Vector3(0f, 0.1f, 0f);
            var belt = beltGo.AddComponent<ConveyorBelt>();
            belt.SetSpeed(config.beltSpeed);

            var spawnPoint = new GameObject("SpawnPoint").transform;
            spawnPoint.SetParent(root, false);
            spawnPoint.position = new Vector3(-config.beltLength * 0.45f, 0.35f, 0f);

            var spawnerGo = new GameObject("ItemSpawner");
            spawnerGo.transform.SetParent(root, false);
            var spawner = spawnerGo.AddComponent<ItemSpawner>();
            SetPrivateField(spawner, "config", config);
            SetPrivateField(spawner, "spawnPoint", spawnPoint);

            var zoneMap = ScriptableObject.CreateInstance<SortZoneMap>();
            var normal = CreateZone(root, "NormalZone", new Vector3(4f, 0f, 2f), Color.green);
            var error = CreateZone(root, "ErrorZone", new Vector3(4f, 0f, -2f), Color.red);
            var unprocessed = CreateZone(root, "UnprocessedZone", new Vector3(6f, 0f, 0f), Color.yellow);
            zoneMap.defaultNormalZone = normal;
            zoneMap.errorZone = error;
            zoneMap.unprocessedZone = unprocessed;

            var unprocessedTrigger = unprocessed.gameObject.AddComponent<UnprocessedZone>();
            SetPrivateField(unprocessedTrigger, "spawner", spawner);

            var arms = new List<RobotArmController>();
            var pickSlots = new List<RobotArmPickSlot>();
            for (var i = 0; i < config.robotArmCount; i++)
            {
                var x = -2f + i * 2f;
                RobotArmController arm;
                if (config.robotArmPrefab != null)
                {
                    arm = RobotArmController.CreateFromPrefab(
                        root,
                        config.robotArmPrefab,
                        new Vector3(x, 0f, 1.5f),
                        i,
                        config.robotArmRigBinding,
                        config.robotArmPoseSet);
                }
                else
                {
                    arm = RobotArmController.CreateRuntimeArm(root, new Vector3(x, 0f, 1.5f), i, config.robotArmPoseSet);
                }

                arms.Add(arm);

                var pickAnchorGo = new GameObject("PickAnchor");
                pickAnchorGo.transform.SetParent(arm.transform, false);
                pickAnchorGo.transform.localPosition = new Vector3(0f, 0.7f, -1.5f);

                var pickSlot = arm.gameObject.AddComponent<RobotArmPickSlot>();
                pickSlot.Configure(arm, pickAnchorGo.transform);
                pickSlots.Add(pickSlot);

                var zoneTargets = arm.GetComponent<RobotArmZoneTargets>();
                if (zoneTargets == null)
                {
                    zoneTargets = arm.gameObject.AddComponent<RobotArmZoneTargets>();
                }

                zoneTargets.ApplyFallbackFromMap(zoneMap);
                arm.SetZoneTargets(zoneTargets);
                if (!zoneTargets.HasCompleteSet())
                {
                    Debug.LogWarning(
                        $"{arm.name}: RobotArmZoneTargets incomplete — SortZoneMap 폴백을 확인하세요.");
                }
            }

            var stationGo = new GameObject("RobotStation_1");
            stationGo.transform.SetParent(root, false);
            const float beltMargin = 1.2f;
            stationGo.transform.position = new Vector3(spawnPoint.position.x + beltMargin, 0.5f, 0f);
            var box = stationGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.2f, 1f, 1.2f);

            var qrGo = new GameObject("QrScannerCamera");
            qrGo.transform.SetParent(stationGo.transform, false);
            qrGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            qrGo.transform.localRotation = QrScanner.TopDownRotation;
            var qrCam = qrGo.AddComponent<Camera>();
            qrCam.enabled = false;
            var qrScanner = qrGo.AddComponent<QrScanner>();
            SetPrivateField(qrScanner, "scanCamera", qrCam);

            var shapeGo = new GameObject("ShapeCamera");
            shapeGo.transform.SetParent(stationGo.transform, false);
            shapeGo.transform.localPosition = new Vector3(0.3f, 1.2f, -0.6f);
            var shapeCam = shapeGo.AddComponent<Camera>();
            shapeCam.enabled = false;
            var shapeCamera = shapeGo.AddComponent<ShapeCamera>();
            SetPrivateField(shapeCamera, "captureCamera", shapeCam);

            if (layoutGo.GetComponent<PickLiftSuccessEvaluator>() == null)
            {
                layoutGo.AddComponent<PickLiftSuccessEvaluator>();
            }

            var dispatcher = layoutGo.AddComponent<ConveyorPickDispatcher>();
            dispatcher.Configure(belt, pickSlots.ToArray(), spawner);

            var station = stationGo.AddComponent<RobotStation>();
            SetPrivateField(station, "dispatcher", dispatcher);
            SetPrivateField(station, "qrScanner", qrScanner);
            SetPrivateField(station, "shapeCamera", shapeCamera);
            SetPrivateField(station, "zoneMap", zoneMap);
            SetPrivateField(station, "config", config);
            var stations = new[] { station };

            var endZoneGo = new GameObject("BeltEndUnprocessedTrigger");
            endZoneGo.transform.SetParent(root, false);
            endZoneGo.transform.position = new Vector3(config.beltLength * 0.45f, 0.5f, 0f);
            var endCol = endZoneGo.AddComponent<BoxCollider>();
            endCol.isTrigger = true;
            endCol.size = new Vector3(1f, 1f, 2f);
            var endZone = endZoneGo.AddComponent<UnprocessedZone>();
            SetPrivateField(endZone, "spawner", spawner);

            layout.SetBuiltReferences(
                belt,
                spawner,
                spawnPoint,
                stations,
                arms.ToArray(),
                zoneMap,
                normal,
                error,
                unprocessed,
                new[] { endZone, unprocessedTrigger });

            return layout;
        }

        static Transform CreateZone(Transform parent, string name, Vector3 position, Color color)
        {
            var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = name;
            zone.transform.SetParent(parent, false);
            zone.transform.position = position;
            zone.transform.localScale = new Vector3(0.8f, SortZoneColliderUtility.ZoneVisualHeight, 0.8f);
            var renderer = zone.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.sharedMaterial = mat;
            SortZoneColliderUtility.EnsureBoxCollider(zone, isTrigger: false);
            return zone.transform;
        }

        /// <summary>
        /// 에디터 레이아웃 빌드(Play 아님)에서는 DestroyImmediate, Play 중에는 Destroy.
        /// </summary>
        static void DestroyColliderForLayout(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        internal static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
