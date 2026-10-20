using System;
using System.Collections.Generic;
using SmartFactory.Logistics;

namespace SmartFactory.Core
{
    /// <summary>
    /// 로봇팔 상태 DTO.
    /// </summary>
    [Serializable]
    public class RobotArmStatus
    {
        public string id;
        public string currentState;
        public string currentTask;
        public float taskProgress;
        public string completionState;
        public float batteryPercent;
        public bool isNormal;
        public bool hasBug;
        public string errorDetail;
        public float[] jointAnglesDeg;
        public float[] endEffectorPosition;
        public float[] endEffectorRotationEuler;
        /// <summary>엔드이펙터가 임계 시간 이상 거의 움직이지 않음(정체 감시).</summary>
        public bool isMovementStalled;
        /// <summary>마지막 의미 있는 엔드이펙터 이동 이후 경과(초).</summary>
        public float secondsSinceEndEffectorMove;
        /// <summary>목적지 구역(A/B/C).</summary>
        public string regionId;
        /// <summary>구역 내 로봇 인덱스(0~2).</summary>
        public int armIndexInRegion;
    }

    /// <summary>
    /// 컨베이어 벨트 상태 DTO.
    /// </summary>
    [Serializable]
    public class ConveyorStatus
    {
        public bool isRunning;
        public float speed;
        public string errorDetail;
    }

    /// <summary>
    /// 중앙뇌 상태 DTO.
    /// </summary>
    [Serializable]
    public class BrainStatus
    {
        public string state;
        public string lastReport;
        public string nextCallUtc;
        public int intervalMinutes;
    }

    /// <summary>
    /// 분류 통계 DTO.
    /// </summary>
    [Serializable]
    public class SortingStats
    {
        public int mismatchCount;
        public int recognitionFailureCount;
        public int unprocessedCount;
        /// <summary>에러 물류(ErrorZone 배정) 누적 처리 수.</summary>
        public int errorLogisticsCount;
        /// <summary>연속 에러 물류 처리 수(일반 물류 처리 시 0으로 리셋).</summary>
        public int consecutiveErrorLogisticsCount;
        /// <summary>연속 에러 물류 알림 임계값.</summary>
        public int consecutiveErrorAlertThreshold;
        /// <summary>에러 물류만 스폰하는 버스트 구간 활성 여부.</summary>
        public bool errorLogisticsBurstActive;
    }

    /// <summary>
    /// 알림 로그 항목.
    /// </summary>
    [Serializable]
    public class NotificationEntry
    {
        public string timestampUtc;
        public string target;
        public string message;
        /// <summary>normal | urgent</summary>
        public string severity;
    }

    /// <summary>
    /// 벨트·로봇팔 정체 감시 결과.
    /// </summary>
    [Serializable]
    public class LineHealthStatus
    {
        public bool logisticsStagnation;
        public float logisticsStagnationSeconds;
        public bool emergencyActive;
        public string emergencyMessage;
        public List<string> stalledRobotIds = new List<string>();
    }

    /// <summary>
    /// 제어판에 전달하는 전체 스냅샷.
    /// </summary>
    [Serializable]
    public class FactorySnapshot
    {
        public string timestampUtc;
        public ConveyorStatus conveyor;
        public List<RobotArmStatus> robots = new List<RobotArmStatus>();
        public BrainStatus brain;
        public SortingStats sorting;
        public List<NotificationEntry> notifications = new List<NotificationEntry>();
        public LineHealthStatus lineHealth;
        public LogisticsSnapshotDto logistics;
    }
}
