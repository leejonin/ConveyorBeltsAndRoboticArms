using System;
using System.Collections.Generic;

namespace SmartFactory.Logistics
{
    [Serializable]
    public class PackageQueueEntryDto
    {
        public string packageId;
        public string destination;
        public string priority;
        public float minutesToDeadline;
        public string lifecycle;
        public string region;
        public string delayRisk;
        public int queuePositionHint;
    }

    [Serializable]
    public class RegionQueueStatusDto
    {
        public string region;
        public int queueLength;
        public string congestionLevel;
        public float throughputPerMinute;
        public bool isSimulationMetric;
    }

    [Serializable]
    public class PackageErrorRecordDto
    {
        public string packageId;
        public int itemId;
        public string errorCode;
        public string detail;
        public int retryCount;
        public string timestampUtc;
    }

    [Serializable]
    public class PriorityChangeLogDto
    {
        public string packageId;
        public string reason;
        public string timestampUtc;
    }

    [Serializable]
    public class OpsMetricsDto
    {
        public int totalSpawned;
        public int totalCompleted;
        public int normalSortApprox;
        public int errorCount;
        public float throughputPerHour;
        public float deadlineCompliancePercent;
        public string summaryText;
    }

    [Serializable]
    public class OpsReportDto
    {
        public string generatedUtc;
        public OpsMetricsDto metrics;
        public string summaryText;
    }

    [Serializable]
    public class LogisticsSnapshotDto
    {
        public int schemaVersion = 1;
        public List<PackageQueueEntryDto> packagesWaiting = new List<PackageQueueEntryDto>();
        public List<RegionQueueStatusDto> regions = new List<RegionQueueStatusDto>();
        public List<PackageErrorRecordDto> errors = new List<PackageErrorRecordDto>();
        public List<PriorityChangeLogDto> priorityChanges = new List<PriorityChangeLogDto>();
        public OpsMetricsDto ops;
        public float virtualEnergyKwh;
        public float virtualTemperatureC;
        public string virtualPlantMode;
        public bool isSimulationValue;
    }
}
