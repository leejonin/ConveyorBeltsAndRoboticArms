using System.Collections.Generic;
using SmartFactory.Conveyor;
using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Logistics;
using SmartFactory.Recognition;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory
{
    /// <summary>
    /// 메인 벨트 + A/B/C 지선·스테이션·로봇 3 구성 레이아웃 생성.
    /// </summary>
    public static class FactoryThreeRegionLayoutBuilder
    {
        const float MainBeltLength = 8f;
        const float BranchBeltLength = 10f;

        public static FactorySceneLayout Build(Transform parent, FactoryConfig config)
        {
            if (config.robotArmPrefab == null)
            {
                config.robotArmPrefab = Resources.Load<GameObject>("SmartFactory/RobotArm");
            }

            if (config.robotArmPrefab == null)
            {
                config.robotArmPrefab = Resources.Load<GameObject>("SmartFactory/RobotArm_Rig");
            }

            var layoutGo = new GameObject("FactoryLayout_ThreeRegion");
            layoutGo.transform.SetParent(parent, false);
            var layout = layoutGo.AddComponent<FactorySceneLayout>();

            var root = new GameObject("FactoryRoot").transform;
            root.SetParent(layoutGo.transform, false);

            var mainBeltGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mainBeltGo.name = "MainConveyorBelt";
            mainBeltGo.transform.SetParent(root, false);
            mainBeltGo.transform.localScale = new Vector3(MainBeltLength, 0.2f, 1.2f);
            mainBeltGo.transform.position = new Vector3(0f, 0.1f, 0f);
            var mainBelt = mainBeltGo.AddComponent<ConveyorBelt>();
            mainBelt.SetSpeed(config.beltSpeed);

            var spawnPoint = new GameObject("SpawnPoint").transform;
            spawnPoint.SetParent(root, false);
            spawnPoint.position = new Vector3(-MainBeltLength * 0.45f, 0.35f, 0f);

            var spawnerGo = new GameObject("ItemSpawner");
            spawnerGo.transform.SetParent(root, false);
            var spawner = spawnerGo.AddComponent<ItemSpawner>();
            FactoryLayoutBuilder.SetPrivateField(spawner, "config", config);
            FactoryLayoutBuilder.SetPrivateField(spawner, "spawnPoint", spawnPoint);

            var routerGo = new GameObject("MainLineRegionRouter");
            routerGo.transform.SetParent(root, false);
            routerGo.transform.position = new Vector3(MainBeltLength * 0.35f, 0.5f, 0f);
            var routerCol = routerGo.AddComponent<BoxCollider>();
            routerCol.isTrigger = true;
            routerCol.size = new Vector3(1.2f, 1f, 2f);
            var router = routerGo.AddComponent<MainLineRegionRouter>();

            var regionSetups = new List<FactoryRegionSetup>();
            var allArms = new List<RobotArmController>();
            var allStations = new List<RobotStation>();
            var endZones = new List<UnprocessedZone>();

            var zPositions = new[] { -5f, 0f, 5f };
            var regions = new[] { DestinationRegion.A, DestinationRegion.B, DestinationRegion.C };
            var armsPerRegion = Mathf.Max(1, config.robotArmCount);

            for (var r = 0; r < regions.Length; r++)
            {
                var regionRoot = new GameObject($"Region_{regions[r]}").transform;
                regionRoot.SetParent(root, false);

                var branchGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                branchGo.name = $"BranchBelt_{regions[r]}";
                branchGo.transform.SetParent(regionRoot, false);
                branchGo.transform.localScale = new Vector3(BranchBeltLength, 0.2f, 1f);
                branchGo.transform.position = new Vector3(MainBeltLength * 0.35f + 1f, 0.1f, zPositions[r]);
                var branchBelt = branchGo.AddComponent<ConveyorBelt>();
                branchBelt.SetSpeed(config.beltSpeed);

                var entry = new GameObject("BranchEntry").transform;
                entry.SetParent(regionRoot, false);
                entry.position = branchGo.transform.position +
                                 new Vector3(-BranchBeltLength * 0.45f, 0.25f, 0f);

                var zoneMap = ScriptableObject.CreateInstance<SortZoneMap>();
                var normal = CreateZone(regionRoot, $"Normal_{regions[r]}", new Vector3(6f, 0f, 1.5f), Color.green);
                var error = CreateZone(regionRoot, $"Error_{regions[r]}", new Vector3(6f, 0f, -1.5f), Color.red);
                var unprocessed = CreateZone(regionRoot, $"Unprocessed_{regions[r]}", new Vector3(7.5f, 0f, 0f), Color.yellow);
                zoneMap.defaultNormalZone = normal;
                zoneMap.errorZone = error;
                zoneMap.unprocessedZone = unprocessed;

                var unprocessedTrigger = unprocessed.gameObject.AddComponent<UnprocessedZone>();
                FactoryLayoutBuilder.SetPrivateField(unprocessedTrigger, "spawner", spawner);
                endZones.Add(unprocessedTrigger);

                var branchEndGo = new GameObject($"BranchEnd_{regions[r]}");
                branchEndGo.transform.SetParent(regionRoot, false);
                branchEndGo.transform.position = entry.position + new Vector3(BranchBeltLength * 0.9f, 0.4f, 0f);
                var branchEndCol = branchEndGo.AddComponent<BoxCollider>();
                branchEndCol.isTrigger = true;
                branchEndCol.size = new Vector3(1f, 1f, 1.5f);
                var branchEndZone = branchEndGo.AddComponent<UnprocessedZone>();
                FactoryLayoutBuilder.SetPrivateField(branchEndZone, "spawner", spawner);
                endZones.Add(branchEndZone);

                var regionArms = new List<RobotArmController>();
                var pickSlots = new List<RobotArmPickSlot>();
                var baseX = branchGo.transform.position.x - 1f;
                for (var a = 0; a < armsPerRegion; a++)
                {
                    var x = baseX + a * 2f;
                    RobotArmController arm;
                    if (config.robotArmPrefab != null)
                    {
                        arm = RobotArmController.CreateFromPrefab(
                            regionRoot,
                            config.robotArmPrefab,
                            new Vector3(x, 0f, zPositions[r] + 1.5f),
                            allArms.Count,
                            config.robotArmRigBinding,
                            config.robotArmPoseSet);
                    }
                    else
                    {
                        arm = RobotArmController.CreateRuntimeArm(
                            regionRoot,
                            new Vector3(x, 0f, zPositions[r] + 1.5f),
                            allArms.Count,
                            config.robotArmPoseSet);
                    }

                    regionArms.Add(arm);
                    allArms.Add(arm);

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
                }

                var stationGo = new GameObject($"RobotStation_{regions[r]}");
                stationGo.transform.SetParent(regionRoot, false);
                stationGo.transform.position = entry.position + new Vector3(1.2f, 0.5f, 0f);
                var stationBox = stationGo.AddComponent<BoxCollider>();
                stationBox.isTrigger = true;
                stationBox.size = new Vector3(1.2f, 1f, 1.2f);

                var qrGo = new GameObject("QrScannerCamera");
                qrGo.transform.SetParent(stationGo.transform, false);
                qrGo.transform.localPosition = new Vector3(0f, 1f, 0f);
                qrGo.transform.localRotation = QrScanner.TopDownRotation;
                var qrCam = qrGo.AddComponent<Camera>();
                qrCam.enabled = false;
                var qrScanner = qrGo.AddComponent<QrScanner>();
                FactoryLayoutBuilder.SetPrivateField(qrScanner, "scanCamera", qrCam);

                var shapeGo = new GameObject("ShapeCamera");
                shapeGo.transform.SetParent(stationGo.transform, false);
                shapeGo.transform.localPosition = new Vector3(0.3f, 1.2f, -0.6f);
                var shapeCam = shapeGo.AddComponent<Camera>();
                shapeCam.enabled = false;
                var shapeCamera = shapeGo.AddComponent<ShapeCamera>();
                FactoryLayoutBuilder.SetPrivateField(shapeCamera, "captureCamera", shapeCam);

                var dispatcherGo = new GameObject($"PickDispatcher_{regions[r]}");
                dispatcherGo.transform.SetParent(regionRoot, false);
                var dispatcher = dispatcherGo.AddComponent<ConveyorPickDispatcher>();
                dispatcher.Configure(branchBelt, pickSlots.ToArray(), spawner);

                var station = stationGo.AddComponent<RobotStation>();
                FactoryLayoutBuilder.SetPrivateField(station, "dispatcher", dispatcher);
                FactoryLayoutBuilder.SetPrivateField(station, "qrScanner", qrScanner);
                FactoryLayoutBuilder.SetPrivateField(station, "shapeCamera", shapeCamera);
                FactoryLayoutBuilder.SetPrivateField(station, "zoneMap", zoneMap);
                FactoryLayoutBuilder.SetPrivateField(station, "config", config);
                allStations.Add(station);

                var regionSetup = regionRoot.gameObject.AddComponent<FactoryRegionSetup>();
                regionSetup.SetBuilt(
                    regions[r],
                    branchBelt,
                    entry,
                    station,
                    regionArms.ToArray(),
                    dispatcher,
                    zoneMap,
                    new[] { unprocessedTrigger, branchEndZone });
                regionSetups.Add(regionSetup);
            }

            if (layoutGo.GetComponent<PickLiftSuccessEvaluator>() == null)
            {
                layoutGo.AddComponent<PickLiftSuccessEvaluator>();
            }

            router.Configure(mainBelt, regionSetups.ToArray());

            layout.SetMultiRegionReferences(
                mainBelt,
                spawner,
                spawnPoint,
                allStations.ToArray(),
                allArms.ToArray(),
                regionSetups.ToArray(),
                router,
                endZones.ToArray());

            return layout;
        }

        static Transform CreateZone(Transform parent, string name, Vector3 localPos, Color color)
        {
            var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = name;
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = localPos;
            zone.transform.localScale = new Vector3(0.8f, SortZoneColliderUtility.ZoneVisualHeight, 0.8f);
            var renderer = zone.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            renderer.sharedMaterial = mat;
            SortZoneColliderUtility.EnsureBoxCollider(zone, isTrigger: false);
            return zone.transform;
        }
    }
}
