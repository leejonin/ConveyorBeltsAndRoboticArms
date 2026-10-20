using SmartFactory.Conveyor;
using SmartFactory.Items;
using SmartFactory.Core;
using UnityEngine;

namespace SmartFactory.Logistics
{
    /// <summary>
    /// ChangeZone에서 스캔이 끝난 물류를 목적지 구역 지선(A/C)으로 이송합니다. 구역 B는 +X 직진(텔레포트 없음).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class MainLineRegionRouter : MonoBehaviour
    {
        [SerializeField] FactoryRegionSetup[] regions;
        [SerializeField] ConveyorBelt mainBelt;
        [SerializeField] bool requireScanComplete = true;

        void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        /// <summary>구역·메인 벨트 참조를 주입합니다.</summary>
        public void Configure(ConveyorBelt main, FactoryRegionSetup[] regionSetups)
        {
            mainBelt = main;
            regions = regionSetups;
        }

        void OnTriggerEnter(Collider other)
        {
            var item = other.GetComponentInParent<FactoryItem>();
            if (item == null)
            {
                return;
            }

            TryRouteItem(item);
        }

        /// <summary>스캔 완료 후 목적지에 맞게 이송합니다. 이미 지선 이송된 A/C는 건너뜁니다.</summary>
        public bool TryRouteItem(FactoryItem item)
        {
            if (item == null || !item.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (requireScanComplete && !item.HasScannedSuccessfully)
            {
                return false;
            }

            if (item.IsRobotPipelineActive || item.IsHeldByRobot)
            {
                return false;
            }

            if (item.HasRoutedToBranch)
            {
                return false;
            }

            var setup = FindRegion(item.DestinationRegion);
            if (setup == null)
            {
                FactoryLogisticsCenter.Instance?.RecordDestinationInvalid(item);
                return false;
            }

            if (item.DestinationRegion == DestinationRegion.B)
            {
                FactoryLogisticsCenter.Instance?.NotifyPackageRouted(item, DestinationRegion.B);
                return true;
            }

            if (setup.BranchEntryPoint == null)
            {
                FactoryLogisticsCenter.Instance?.RecordDestinationInvalid(item);
                return false;
            }

            item.MarkRoutedToBranch();
            item.SetLifecycleState(PackageLifecycleState.OnBranchLine);
            var entry = setup.BranchEntryPoint.position;
            var rb = item.ItemRigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = entry + Vector3.up * 0.05f;
            }
            else
            {
                item.transform.position = entry;
            }

            FactoryLogisticsCenter.Instance?.NotifyPackageRouted(item, item.DestinationRegion);
            return true;
        }

        FactoryRegionSetup FindRegion(DestinationRegion destination)
        {
            if (regions == null)
            {
                return null;
            }

            for (var i = 0; i < regions.Length; i++)
            {
                if (regions[i] != null && regions[i].Region == destination)
                {
                    return regions[i];
                }
            }

            return null;
        }
    }
}
