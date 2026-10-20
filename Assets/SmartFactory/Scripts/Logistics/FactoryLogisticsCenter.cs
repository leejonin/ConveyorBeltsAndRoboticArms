using System;
using System.Collections.Generic;
using SmartFactory.Brain;
using SmartFactory.Conveyor;
using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory.Logistics
{
    /// <summary>
    /// AI 물류센터 확장: 택배 상태·혼잡·지연·운영 지표·가상 설비·장비 안내 집계.
    /// </summary>
    public class FactoryLogisticsCenter : MonoBehaviour
    {
        public static FactoryLogisticsCenter Instance { get; private set; }

        [SerializeField] FactoryConfig config;
        [SerializeField] FactoryRegionSetup[] regions;
        [SerializeField] ItemSpawner itemSpawner;
        [SerializeField] ConveyorBelt mainConveyor;

        readonly List<PackageQueueEntryDto> m_queueEntries = new List<PackageQueueEntryDto>();
        readonly List<RegionQueueStatusDto> m_regionStatus = new List<RegionQueueStatusDto>();
        readonly List<PackageErrorRecordDto> m_errorRecords = new List<PackageErrorRecordDto>();
        readonly List<PriorityChangeLogDto> m_priorityLogs = new List<PriorityChangeLogDto>();
        readonly Dictionary<int, float> m_completedAtSimTime = new Dictionary<int, float>();
        readonly Dictionary<int, float> m_acceptSimTime = new Dictionary<int, float>();

        float m_sessionStartTime;
        int m_totalSpawned;
        int m_totalCompleted;
        int m_deadlineMet;
        int m_deadlineTarget;
        string m_lastOpsSummary = string.Empty;
        VirtualPlantMode m_plantMode = VirtualPlantMode.Balanced;

        public LogisticsSnapshotDto Logistics { get; private set; } = new LogisticsSnapshotDto();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            m_sessionStartTime = Time.time;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>런타임 참조를 주입합니다.</summary>
        public void Configure(
            FactoryConfig factoryConfig,
            FactoryRegionSetup[] regionSetups,
            ItemSpawner spawner,
            ConveyorBelt mainBelt)
        {
            config = factoryConfig;
            regions = regionSetups;
            itemSpawner = spawner;
            mainConveyor = mainBelt;
        }

        void Update()
        {
            TickRegionCongestion();
            TickDelayRisk();
            TickVirtualPlant();
            PublishSnapshot();
        }

        /// <summary>스폰 시 택배 메타데이터를 등록합니다.</summary>
        public void RegisterSpawnedPackage(FactoryItem item)
        {
            if (item == null)
            {
                return;
            }

            m_totalSpawned++;
            m_deadlineTarget++;
            m_acceptSimTime[item.ItemId] = Time.time;
            item.SetLifecycleState(PackageLifecycleState.OnMainLine);
            RebuildQueueFromDispatchers();
        }

        /// <summary>지선 이송 완료.</summary>
        public void NotifyPackageRouted(FactoryItem item, DestinationRegion region)
        {
            if (item == null)
            {
                return;
            }

            item.SetLifecycleState(PackageLifecycleState.OnBranchLine);
            RebuildQueueFromDispatchers();
        }

        /// <summary>목적지 무효.</summary>
        public void RecordDestinationInvalid(FactoryItem item)
        {
            RecordError(item, RecognitionErrorCode.DestinationInvalid, "목적지 구역을 찾을 수 없음");
        }

        /// <summary>인식 오류 기록.</summary>
        public void RecordRecognitionError(FactoryItem item, RecognitionErrorCode code, string detail)
        {
            RecordError(item, code, detail);
            if (item != null)
            {
                item.SetLifecycleState(PackageLifecycleState.Error);
            }
        }

        void RecordError(FactoryItem item, RecognitionErrorCode code, string detail)
        {
            if (item == null)
            {
                return;
            }

            m_errorRecords.Insert(0, new PackageErrorRecordDto
            {
                packageId = item.PackageDisplayId,
                itemId = item.ItemId,
                errorCode = code.ToString(),
                detail = detail,
                retryCount = item.RetryCount,
                timestampUtc = DateTime.UtcNow.ToString("o")
            });

            if (m_errorRecords.Count > 100)
            {
                m_errorRecords.RemoveAt(m_errorRecords.Count - 1);
            }

            var guidance = EquipmentFaultCatalog.GetGuidanceForRecognition(code);
            if (!string.IsNullOrEmpty(guidance))
            {
                FactoryStateRegistry.Instance?.AddNotification("recognition", guidance, "normal");
            }
        }

        /// <summary>분류 완료(Zone 배치) 시 호출.</summary>
        public void NotifySortComplete(FactoryItem item)
        {
            if (item == null)
            {
                return;
            }

            item.SetLifecycleState(PackageLifecycleState.SortComplete);
            m_totalCompleted++;
            m_completedAtSimTime[item.ItemId] = Time.time;
            if (Time.time <= item.DeadlineSimTime)
            {
                m_deadlineMet++;
            }

            RebuildQueueFromDispatchers();
        }

        /// <summary>우선순위 변경 로그.</summary>
        public void LogPriorityChange(string packageId, string reason)
        {
            m_priorityLogs.Insert(0, new PriorityChangeLogDto
            {
                packageId = packageId,
                reason = reason,
                timestampUtc = DateTime.UtcNow.ToString("o")
            });

            if (m_priorityLogs.Count > 50)
            {
                m_priorityLogs.RemoveAt(m_priorityLogs.Count - 1);
            }
        }

        void RebuildQueueFromDispatchers()
        {
            m_queueEntries.Clear();
            if (regions == null)
            {
                return;
            }

            for (var r = 0; r < regions.Length; r++)
            {
                var region = regions[r];
                if (region == null || region.PickDispatcher == null)
                {
                    continue;
                }

                var waiting = region.PickDispatcher.GetWaitingPackages();
                for (var i = 0; i < waiting.Count; i++)
                {
                    var item = waiting[i];
                    if (item == null)
                    {
                        continue;
                    }

                    m_queueEntries.Add(new PackageQueueEntryDto
                    {
                        packageId = item.PackageDisplayId,
                        destination = item.DestinationRegion.ToString(),
                        priority = item.Priority.ToString(),
                        minutesToDeadline = item.MinutesToDeadline(Time.time),
                        lifecycle = item.LifecycleState.ToString(),
                        region = region.Region.ToString()
                    });
                }
            }
        }

        void TickRegionCongestion()
        {
            m_regionStatus.Clear();
            if (regions == null || config == null)
            {
                return;
            }

            for (var i = 0; i < regions.Length; i++)
            {
                var region = regions[i];
                if (region == null)
                {
                    continue;
                }

                var queueLen = region.PickDispatcher != null ? region.PickDispatcher.WaitQueueCount : 0;
                var level = RegionCongestionMonitor.EvaluateLevel(
                    queueLen,
                    config.congestionCautionQueue,
                    config.congestionWarningQueue,
                    config.congestionSevereQueue);
                var throughput = region.PickDispatcher != null
                    ? region.PickDispatcher.RecentThroughputPerMinute
                    : 0f;

                m_regionStatus.Add(new RegionQueueStatusDto
                {
                    region = region.Region.ToString(),
                    queueLength = queueLen,
                    congestionLevel = level.ToString(),
                    throughputPerMinute = throughput,
                    isSimulationMetric = true
                });

                if (level >= RegionCongestionLevel.Congested)
                {
                    FactoryStateRegistry.Instance?.AddNotification(
                        "congestion",
                        $"구역 {region.Region} 혼잡({level}): 대기열 {queueLen} (시뮬레이션)",
                        level == RegionCongestionLevel.Severe ? "urgent" : "normal");
                }
            }
        }

        void TickDelayRisk()
        {
            for (var i = 0; i < m_queueEntries.Count; i++)
            {
                var entry = m_queueEntries[i];
                entry.delayRisk = DelayRiskEvaluator.Evaluate(
                    entry.minutesToDeadline,
                    entry.queuePositionHint).ToString();
                m_queueEntries[i] = entry;
            }
        }

        void TickVirtualPlant()
        {
            var energy = VirtualPlantSimulator.EstimateEnergyUse(
                m_plantMode,
                regions,
                mainConveyor,
                Time.time - m_sessionStartTime);
            Logistics.virtualEnergyKwh = energy;
            Logistics.virtualTemperatureC = VirtualPlantSimulator.EstimateTemperature(m_plantMode, energy);
            Logistics.virtualPlantMode = m_plantMode.ToString();
            Logistics.isSimulationValue = true;
        }

        void PublishSnapshot()
        {
            Logistics.schemaVersion = 2;
            Logistics.packagesWaiting = new List<PackageQueueEntryDto>(m_queueEntries);
            Logistics.regions = new List<RegionQueueStatusDto>(m_regionStatus);
            Logistics.errors = new List<PackageErrorRecordDto>(m_errorRecords);
            Logistics.priorityChanges = new List<PriorityChangeLogDto>(m_priorityLogs);
            Logistics.ops = BuildOpsMetrics();
            FactoryStateRegistry.Instance?.UpdateLogistics(Logistics);
        }

        OpsMetricsDto BuildOpsMetrics()
        {
            var elapsedMin = Mathf.Max(0.01f, (Time.time - m_sessionStartTime) / 60f);
            var sorting = FactoryStateRegistry.Instance != null
                ? FactoryStateRegistry.Instance.Sorting
                : new SortingStats();
            var passApprox = m_totalCompleted - sorting.mismatchCount - sorting.recognitionFailureCount;
            if (passApprox < 0)
            {
                passApprox = 0;
            }

            var deadlineRate = m_deadlineTarget > 0 ? m_deadlineMet / (float)m_deadlineTarget * 100f : 0f;
            m_lastOpsSummary =
                $"총 {m_totalSpawned}개 투입, {m_totalCompleted}개 처리 완료. " +
                $"인식·불일치·에러 물류는 sorting 통계 참조. 마감 준수율 {deadlineRate:F0}% (시뮬).";

            return new OpsMetricsDto
            {
                totalSpawned = m_totalSpawned,
                totalCompleted = m_totalCompleted,
                normalSortApprox = passApprox,
                errorCount = m_errorRecords.Count,
                throughputPerHour = m_totalCompleted / elapsedMin * 60f,
                deadlineCompliancePercent = deadlineRate,
                summaryText = m_lastOpsSummary
            };
        }

        /// <summary>운영 보고서 JSON 생성.</summary>
        public OpsReportDto BuildOpsReport(bool useLlmPolish = false)
        {
            var ops = BuildOpsMetrics();
            var report = new OpsReportDto
            {
                generatedUtc = DateTime.UtcNow.ToString("o"),
                metrics = ops,
                summaryText = ops.summaryText
            };

            if (useLlmPolish && FactoryStateRegistry.Instance != null)
            {
                report.summaryText = MockCentralBrainClient.PolishReportSummary(ops);
            }

            return report;
        }

        /// <summary>혼잡 완화: 시뮬레이션 스폰 간격 확대.</summary>
        public void ApplyCongestionMitigationSuggestion()
        {
            if (itemSpawner == null || config == null)
            {
                return;
            }

            itemSpawner.ApplySpawnRateScale(config.congestionSpawnScaleFactor);
            FactoryStateRegistry.Instance?.AddNotification(
                "congestion",
                $"혼잡 완화: 스폰 간격 ×{config.congestionSpawnScaleFactor:F1} (시뮬레이션)",
                "normal");
        }
    }
}
