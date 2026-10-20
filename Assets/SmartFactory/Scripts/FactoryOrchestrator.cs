using SmartFactory.Brain;
using SmartFactory.Core;
using SmartFactory.Dashboard;
using SmartFactory.Items;
using SmartFactory.Logistics;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory
{
    /// <summary>
    /// 공장 서비스(상태·중앙뇌·대시보드)를 연결합니다. 레이아웃은 씬의 FactorySceneLayout을 우선 사용합니다.
    /// </summary>
    public class FactoryOrchestrator : MonoBehaviour
    {
        [SerializeField] FactoryConfig config;
        [SerializeField] FactorySceneLayout sceneLayout;

        [Tooltip("켜면 Play 시마다 레이아웃을 새로 생성합니다. 끄면 씬에 배치·연결된 레이아웃을 사용합니다.")]
        [SerializeField] bool generateLayoutAtPlay;

        void Awake()
        {
            EnsureConfig();

            if (sceneLayout == null)
            {
                sceneLayout = GetComponentInChildren<FactorySceneLayout>(true);
            }

            if (!generateLayoutAtPlay && sceneLayout != null && sceneLayout.IsPlayable())
            {
                InitializeServicesFromScene(sceneLayout);
                return;
            }

            if (generateLayoutAtPlay)
            {
                if (sceneLayout != null)
                {
                    Destroy(sceneLayout.gameObject);
                    sceneLayout = null;
                }

                sceneLayout = FactoryLayoutBuilder.Build(transform, config);
                InitializeServicesFromScene(sceneLayout);
                return;
            }

            Debug.LogWarning(
                "SmartFactory: 씬 레이아웃이 없습니다. 메뉴 SmartFactory → Build Factory Layout In Scene 실행 후 씬을 저장하거나, Generate Layout At Play를 켜세요.");
        }

        void EnsureConfig()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<FactoryConfig>();
            }

            if (config.robotArmPrefab == null)
            {
                config.robotArmPrefab = Resources.Load<GameObject>("SmartFactory/RobotArm");
            }

            if (config.robotArmPrefab == null)
            {
                config.robotArmPrefab = Resources.Load<GameObject>("SmartFactory/RobotArm_Rig");
            }

            if (config.robotArmRigBinding == null)
            {
                config.robotArmRigBinding = Resources.Load<RobotArmRigBinding>("SmartFactory/RoboticArmRigBinding");
            }

            if (config.robotArmPoseSet == null)
            {
                config.robotArmPoseSet = Resources.Load<RobotArmPoseSet>("SmartFactory/RobotArmDefaultPoseSet");
            }
        }

        void InitializeServicesFromScene(FactorySceneLayout layout)
        {
            _ = MainThreadDispatcher.Instance;

            var registry = GetComponent<FactoryStateRegistry>();
            if (registry == null)
            {
                registry = gameObject.AddComponent<FactoryStateRegistry>();
            }

            var robotCount = layout.UseMultiRegion && layout.RobotArms != null
                ? layout.RobotArms.Length
                : config.robotArmCount;
            registry.InitializeRobots(robotCount);
            registry.InitializeConsecutiveErrorAlertThreshold(config.defaultConsecutiveErrorAlertThreshold);

            var zoneMap = layout.ResolveSortZoneMap();
            if (layout.ItemSpawner != null)
            {
                FactoryLayoutBuilder.SetPrivateField(layout.ItemSpawner, "config", config);
                if (layout.SpawnPoint != null)
                {
                    FactoryLayoutBuilder.SetPrivateField(layout.ItemSpawner, "spawnPoint", layout.SpawnPoint);
                }
            }

            if (layout.ConveyorBelt != null)
            {
                layout.ConveyorBelt.SetSpeed(config.beltSpeed);
            }

            var executor = GetComponent<BrainCommandExecutor>();
            if (executor == null)
            {
                executor = gameObject.AddComponent<BrainCommandExecutor>();
            }

            FactoryLayoutBuilder.SetPrivateField(executor, "conveyor", layout.ConveyorBelt);

            var scheduler = GetComponent<BrainScheduler>();
            if (scheduler == null)
            {
                scheduler = gameObject.AddComponent<BrainScheduler>();
            }

            FactoryLayoutBuilder.SetPrivateField(scheduler, "config", config);
            FactoryLayoutBuilder.SetPrivateField(scheduler, "executor", executor);

            var dashboard = GetComponent<DashboardServer>();
            if (dashboard == null)
            {
                dashboard = gameObject.AddComponent<DashboardServer>();
            }

            FactoryLayoutBuilder.SetPrivateField(dashboard, "config", config);
            FactoryLayoutBuilder.SetPrivateField(dashboard, "brainScheduler", scheduler);
            FactoryLayoutBuilder.SetPrivateField(dashboard, "commandExecutor", executor);

            if (layout.RobotStations != null)
            {
                foreach (var station in layout.RobotStations)
                {
                    if (station == null)
                    {
                        continue;
                    }

                    if (zoneMap != null)
                    {
                        FactoryLayoutBuilder.SetPrivateField(station, "zoneMap", zoneMap);
                    }

                    FactoryLayoutBuilder.SetPrivateField(station, "config", config);
                }
            }

            ConveyorPickDispatcher pickDispatcher;
            if (layout.UseMultiRegion)
            {
                pickDispatcher = WireMultiRegionPickDispatchers(layout);
            }
            else
            {
                pickDispatcher = WirePickDispatcher(layout);
            }

            if (layout.UseMultiRegion && layout.Regions != null)
            {
                for (var ri = 0; ri < layout.Regions.Length; ri++)
                {
                    var d = layout.Regions[ri]?.PickDispatcher;
                    if (d != null)
                    {
                        executor.RegisterPickDispatcher(d);
                    }
                }
            }

            if (pickDispatcher != null)
            {
                executor.SetPickDispatcher(pickDispatcher);
            }

            if (layout.RobotArms != null)
            {
                var armsPerRegion = Mathf.Max(1, config.robotArmCount);
                for (var i = 0; i < layout.RobotArms.Length; i++)
                {
                    var arm = layout.RobotArms[i];
                    if (arm == null)
                    {
                        continue;
                    }

                    if (config.robotArmPoseSet != null)
                    {
                        arm.SetPoseSet(config.robotArmPoseSet);
                    }

                    if (config.robotArmRigBinding != null)
                    {
                        arm.SetupFromBinding(config.robotArmRigBinding);
                    }

                    arm.Configure(i);
                    executor.RegisterRobot($"RobotArm_{i + 1}", arm);
                    if (layout.UseMultiRegion)
                    {
                        var regionIndex = i / armsPerRegion;
                        var armInRegion = i % armsPerRegion;
                        var regionId = regionIndex < layout.Regions.Length
                            ? layout.Regions[regionIndex].Region.ToString()
                            : "?";
                        registry.UpdateRobotRegionMeta(i, regionId, armInRegion);
                    }
                }
            }

            var lineMonitor = GetComponent<FactoryLineHealthMonitor>();
            if (lineMonitor == null)
            {
                lineMonitor = gameObject.AddComponent<FactoryLineHealthMonitor>();
            }

            lineMonitor.Configure(layout.ConveyorBelt, layout.RobotArms, scheduler);

            var logistics = GetComponent<FactoryLogisticsCenter>();
            if (logistics == null)
            {
                logistics = gameObject.AddComponent<FactoryLogisticsCenter>();
            }

            logistics.Configure(
                config,
                layout.UseMultiRegion ? layout.Regions : null,
                layout.ItemSpawner,
                layout.ConveyorBelt);

            if (layout.ItemSpawner != null)
            {
                if (layout.BeltEndUnprocessedZones != null)
                {
                    foreach (var zone in layout.BeltEndUnprocessedZones)
                    {
                        if (zone != null)
                        {
                            FactoryLayoutBuilder.SetPrivateField(zone, "spawner", layout.ItemSpawner);
                        }
                    }
                }

                if (layout.RobotArms != null)
                {
                    foreach (var arm in layout.RobotArms)
                    {
                        if (arm == null)
                        {
                            continue;
                        }

                        var armZones = arm.GetComponentsInChildren<UnprocessedZone>(true);
                        for (var z = 0; z < armZones.Length; z++)
                        {
                            if (armZones[z] != null)
                            {
                                FactoryLayoutBuilder.SetPrivateField(armZones[z], "spawner", layout.ItemSpawner);
                            }
                        }
                    }
                }
            }
        }

        ConveyorPickDispatcher WirePickDispatcher(FactorySceneLayout layout)
        {
            var dispatcher = layout.GetComponent<ConveyorPickDispatcher>();
            if (dispatcher == null)
            {
                dispatcher = layout.GetComponentInChildren<ConveyorPickDispatcher>(true);
            }

            if (dispatcher == null)
            {
                return null;
            }

            RobotArmPickSlot[] slots = null;
            if (layout.RobotArms != null && layout.RobotArms.Length > 0)
            {
                var slotList = new System.Collections.Generic.List<RobotArmPickSlot>();
                foreach (var arm in layout.RobotArms)
                {
                    if (arm == null)
                    {
                        continue;
                    }

                    var slot = arm.GetComponent<RobotArmPickSlot>();
                    if (slot != null)
                    {
                        slotList.Add(slot);
                    }
                }

                if (slotList.Count > 0)
                {
                    slots = slotList.ToArray();
                }
            }

            dispatcher.Configure(layout.ConveyorBelt, slots, layout.ItemSpawner);

            var sortMap = layout.ResolveSortZoneMap();
            if (layout.RobotArms != null && sortMap != null)
            {
                foreach (var arm in layout.RobotArms)
                {
                    if (arm == null)
                    {
                        continue;
                    }

                    var zoneTargets = arm.GetComponent<RobotArmZoneTargets>();
                    if (zoneTargets == null)
                    {
                        zoneTargets = arm.gameObject.AddComponent<RobotArmZoneTargets>();
                    }

                    zoneTargets.ApplyFallbackFromMap(sortMap);
                    arm.SetZoneTargets(zoneTargets);
                }
            }

            var pickLiftEvaluator = layout.GetComponent<PickLiftSuccessEvaluator>();
            if (pickLiftEvaluator == null)
            {
                pickLiftEvaluator = layout.gameObject.AddComponent<PickLiftSuccessEvaluator>();
            }

            if (layout.RobotArms != null)
            {
                foreach (var arm in layout.RobotArms)
                {
                    if (arm != null)
                    {
                        arm.SetPickLiftEvaluator(pickLiftEvaluator);
                    }
                }
            }

            if (layout.RobotStations != null)
            {
                foreach (var station in layout.RobotStations)
                {
                    if (station != null)
                    {
                        FactoryLayoutBuilder.SetPrivateField(station, "dispatcher", dispatcher);
                    }
                }
            }

            return dispatcher;
        }

        ConveyorPickDispatcher WireMultiRegionPickDispatchers(FactorySceneLayout layout)
        {
            ConveyorPickDispatcher first = null;
            if (layout.Regions == null)
            {
                return null;
            }

            var pickLiftEvaluator = layout.GetComponent<PickLiftSuccessEvaluator>();
            if (pickLiftEvaluator == null)
            {
                pickLiftEvaluator = layout.gameObject.AddComponent<PickLiftSuccessEvaluator>();
            }

            for (var r = 0; r < layout.Regions.Length; r++)
            {
                var region = layout.Regions[r];
                if (region == null)
                {
                    continue;
                }

                var dispatcher = region.PickDispatcher;
                if (dispatcher == null)
                {
                    continue;
                }

                if (first == null)
                {
                    first = dispatcher;
                }

                if (region.RobotArms != null)
                {
                    foreach (var arm in region.RobotArms)
                    {
                        if (arm == null)
                        {
                            continue;
                        }

                        var zoneTargets = arm.GetComponent<RobotArmZoneTargets>();
                        if (zoneTargets != null && region.SortZoneMap != null)
                        {
                            zoneTargets.ApplyFallbackFromMap(region.SortZoneMap);
                            arm.SetZoneTargets(zoneTargets);
                        }

                        arm.SetPickLiftEvaluator(pickLiftEvaluator);
                    }
                }

                if (region.Station != null)
                {
                    FactoryLayoutBuilder.SetPrivateField(region.Station, "dispatcher", dispatcher);
                    FactoryLayoutBuilder.SetPrivateField(region.Station, "config", config);
                    if (region.SortZoneMap != null)
                    {
                        FactoryLayoutBuilder.SetPrivateField(region.Station, "zoneMap", region.SortZoneMap);
                    }
                }
            }

            WireChangeZoneScanStations(layout);

            return first;
        }

        void WireChangeZoneScanStations(FactorySceneLayout layout)
        {
            if (layout.RobotStations == null || layout.Regions == null || layout.Regions.Length == 0)
            {
                return;
            }

            SortZoneMap fallbackZoneMap = null;
            for (var r = 0; r < layout.Regions.Length; r++)
            {
                if (layout.Regions[r]?.SortZoneMap != null)
                {
                    fallbackZoneMap = layout.Regions[r].SortZoneMap;
                    break;
                }
            }

            foreach (var station in layout.RobotStations)
            {
                if (station == null || !station.gameObject.activeInHierarchy)
                {
                    continue;
                }

                FactoryLayoutBuilder.SetPrivateField(station, "dispatcher", null);
                FactoryLayoutBuilder.SetPrivateField(station, "regionSetups", layout.Regions);
                if (layout.RegionRouter != null)
                {
                    FactoryLayoutBuilder.SetPrivateField(station, "regionRouter", layout.RegionRouter);
                }

                FactoryLayoutBuilder.SetPrivateField(station, "config", config);
                if (fallbackZoneMap != null)
                {
                    FactoryLayoutBuilder.SetPrivateField(station, "zoneMap", fallbackZoneMap);
                }
            }
        }
    }
}
