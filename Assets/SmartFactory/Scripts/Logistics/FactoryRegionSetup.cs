using SmartFactory.Core;
using SmartFactory.Conveyor;
using SmartFactory.Items;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory.Logistics
{
    /// <summary>
    /// A/B/C 한 구역: 지선 벨트·스테이션·로봇 3·디스패처·분류 Zone.
    /// </summary>
    public class FactoryRegionSetup : MonoBehaviour
    {
        [SerializeField] DestinationRegion region = DestinationRegion.A;
        [SerializeField] ConveyorBelt branchBelt;
        [SerializeField] Transform branchEntryPoint;
        [SerializeField] RobotStation station;
        [SerializeField] RobotArmController[] robotArms;
        [SerializeField] ConveyorPickDispatcher pickDispatcher;
        [SerializeField] SortZoneMap sortZoneMap;
        [SerializeField] UnprocessedZone[] beltEndUnprocessedZones;

        public DestinationRegion Region => region;
        public ConveyorBelt BranchBelt => branchBelt;
        public Transform BranchEntryPoint => branchEntryPoint;
        public RobotStation Station => station;
        public RobotArmController[] RobotArms => robotArms;
        public ConveyorPickDispatcher PickDispatcher => pickDispatcher;
        public SortZoneMap SortZoneMap => sortZoneMap;
        public UnprocessedZone[] BeltEndUnprocessedZones => beltEndUnprocessedZones;

        /// <summary>에디터·빌더가 참조를 채웁니다.</summary>
        public void SetBuilt(
            DestinationRegion regionId,
            ConveyorBelt belt,
            Transform entry,
            RobotStation robotStation,
            RobotArmController[] arms,
            ConveyorPickDispatcher dispatcher,
            SortZoneMap zones,
            UnprocessedZone[] endZones)
        {
            region = regionId;
            branchBelt = belt;
            branchEntryPoint = entry;
            station = robotStation;
            robotArms = arms;
            pickDispatcher = dispatcher;
            sortZoneMap = zones;
            beltEndUnprocessedZones = endZones;
        }
    }
}
