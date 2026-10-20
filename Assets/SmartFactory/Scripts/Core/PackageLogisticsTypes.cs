using System;

namespace SmartFactory.Core
{
    /// <summary>택배 목적지 구역(A/B/C).</summary>
    public enum DestinationRegion
    {
        A = 0,
        B = 1,
        C = 2
    }

    /// <summary>주문 우선순위 등급.</summary>
    public enum PackagePriority
    {
        Normal = 0,
        High = 1,
        Urgent = 2
    }

    /// <summary>택배 처리 진행 단계.</summary>
    public enum PackageLifecycleState
    {
        Waiting,
        OnMainLine,
        Routing,
        OnBranchLine,
        Scanning,
        Picking,
        SortComplete,
        Error
    }

    /// <summary>설계서 §5.2 인식·분류 오류 코드.</summary>
    public enum RecognitionErrorCode
    {
        None,
        QrReadFail,
        ClassifyLowConf,
        LabelMismatch,
        UnknownType,
        DestinationInvalid
    }

    /// <summary>혼잡 단계(시뮬레이션 기준).</summary>
    public enum RegionCongestionLevel
    {
        Normal,
        Caution,
        Congested,
        Severe
    }

    /// <summary>지연 위험 단계.</summary>
    public enum DelayRiskLevel
    {
        Normal,
        Caution,
        AtRisk,
        Unknown
    }

    /// <summary>장비 오류 안내 유형(규칙 기반).</summary>
    public enum EquipmentFaultKind
    {
        PickFailure,
        ConveyorStall,
        CameraTimeout,
        CommunicationLoss,
        VirtualOverheat,
        RepeatedFailure
    }

    /// <summary>가상 운영 모드.</summary>
    public enum VirtualPlantMode
    {
        Balanced,
        Throughput,
        Eco
    }
}
