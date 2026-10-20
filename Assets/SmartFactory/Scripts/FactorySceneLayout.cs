using SmartFactory.Conveyor;
using SmartFactory.Items;
using SmartFactory.Logistics;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory
{
    /// <summary>
    /// 에디터에서 배치한 공장 오브젝트 참조. Play 시 FactoryOrchestrator가 이 구성을 사용합니다.
    /// </summary>
    public class FactorySceneLayout : MonoBehaviour
    {
        [Tooltip("벨트·스테이션 등이 들어갈 부모. 비어 있으면 이 컴포넌트 Transform 사용")]
        [SerializeField] Transform layoutRoot;

        [Header("컨베이어·물품")]
        [SerializeField] ConveyorBelt conveyorBelt;
        [SerializeField] ItemSpawner itemSpawner;
        [SerializeField] Transform spawnPoint;

        [Header("로봇 스테이션")]
        [SerializeField] RobotStation[] robotStations;

        [Header("로봇팔")]
        [SerializeField] RobotArmController[] robotArms;

        [Header("분류 구역")]
        [SerializeField] SortZoneMap sortZoneMap;
        [SerializeField] Transform normalZone;
        [SerializeField] Transform errorZone;
        [SerializeField] Transform unprocessedZone;

        [Header("미처리 트리거")]
        [SerializeField] UnprocessedZone[] beltEndUnprocessedZones;

        [Header("3구역 물류")]
        [SerializeField] FactoryRegionSetup[] regions;
        [SerializeField] MainLineRegionRouter regionRouter;

        /// <summary>3구역(A/B/C) 레이아웃 사용 여부.</summary>
        public bool UseMultiRegion => regions != null && regions.Length > 0;

        public FactoryRegionSetup[] Regions => regions;
        public MainLineRegionRouter RegionRouter => regionRouter;

        /// <summary>레이아웃 루트 Transform.</summary>
        public Transform LayoutRoot => layoutRoot != null ? layoutRoot : transform;

        public ConveyorBelt ConveyorBelt => conveyorBelt;
        public ItemSpawner ItemSpawner => itemSpawner;
        public Transform SpawnPoint => spawnPoint;
        public RobotStation[] RobotStations => robotStations;
        public RobotArmController[] RobotArms => robotArms;
        public SortZoneMap SortZoneMap => sortZoneMap;
        public UnprocessedZone[] BeltEndUnprocessedZones => beltEndUnprocessedZones;

        /// <summary>
        /// Play에 필요한 최소 참조가 채워졌는지 확인합니다.
        /// </summary>
        public bool IsPlayable()
        {
            return conveyorBelt != null && itemSpawner != null && spawnPoint != null &&
                   robotStations != null && robotStations.Length >= 1 &&
                   robotArms != null && robotArms.Length >= 1;
        }

        /// <summary>
        /// sortZoneMap이 없고 구역 Transform만 있으면 런타임용 맵을 만듭니다.
        /// </summary>
        public SortZoneMap ResolveSortZoneMap()
        {
            if (sortZoneMap != null)
            {
                return sortZoneMap;
            }

            if (normalZone == null && errorZone == null && unprocessedZone == null)
            {
                return null;
            }

            var map = ScriptableObject.CreateInstance<SortZoneMap>();
            map.defaultNormalZone = normalZone;
            map.errorZone = errorZone;
            map.unprocessedZone = unprocessedZone;
            return map;
        }

        /// <summary>
        /// 빌더/에디터가 참조를 채웁니다.
        /// </summary>
        internal void SetBuiltReferences(
            ConveyorBelt belt,
            ItemSpawner spawner,
            Transform spawn,
            RobotStation[] stations,
            RobotArmController[] arms,
            SortZoneMap zones,
            Transform normal,
            Transform error,
            Transform unprocessed,
            UnprocessedZone[] endZones)
        {
            conveyorBelt = belt;
            itemSpawner = spawner;
            spawnPoint = spawn;
            robotStations = stations;
            robotArms = arms;
            sortZoneMap = zones;
            normalZone = normal;
            errorZone = error;
            unprocessedZone = unprocessed;
            beltEndUnprocessedZones = endZones;
        }

        /// <summary>3구역 레이아웃 참조를 채웁니다.</summary>
        internal void SetMultiRegionReferences(
            ConveyorBelt mainBelt,
            ItemSpawner spawnerRef,
            Transform spawn,
            RobotStation[] stations,
            RobotArmController[] arms,
            FactoryRegionSetup[] regionSetups,
            MainLineRegionRouter router,
            UnprocessedZone[] endZones)
        {
            conveyorBelt = mainBelt;
            itemSpawner = spawnerRef;
            spawnPoint = spawn;
            robotStations = stations;
            robotArms = arms;
            regions = regionSetups;
            regionRouter = router;
            beltEndUnprocessedZones = endZones;
            sortZoneMap = null;
            normalZone = null;
            errorZone = null;
            unprocessedZone = null;
        }
    }
}
