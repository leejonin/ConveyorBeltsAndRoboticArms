using SmartFactory.Robot;
using UnityEngine;

namespace SmartFactory.Core
{
    /// <summary>
    /// 스마트팩토리 시뮬레이션 전역 설정.
    /// </summary>
    [CreateAssetMenu(fileName = "FactoryConfig", menuName = "SmartFactory/Factory Config")]
    public class FactoryConfig : ScriptableObject
    {
        [Header("장비")]
        [Tooltip("구역당 로봇팔 수(3구역×3대=9). 단일 벨트 레이아웃에서도 동일.")]
        public int robotArmCount = 3;
        [Tooltip("3구역(A/B/C) 메인·지선 물류 레이아웃 사용")]
        public bool useThreeRegionLogistics = true;
        public int regionCount = 3;
        [Tooltip("Robotic-Arm-Rig 프리팹. 비어 있으면 런타임 더미 팔 사용")]
        public GameObject robotArmPrefab;
        public RobotArmRigBinding robotArmRigBinding;
        public RobotArmPoseSet robotArmPoseSet;
        public float beltSpeed = 1.2f;
        public float beltLength = 12f;

        [Header("물품 생성")]
        public float spawnIntervalMin = 1f;
        public float spawnIntervalMax = 5f;
        [Range(0f, 1f)]
        public float mismatchInjectionRate = 0.25f;
        [Tooltip("에러 물류(이상 QR·QR 없음·이상 형태) 단일 스폰 확률")]
        [Range(0f, 1f)]
        public float errorLogisticsSpawnRate = 0.005f;
        [Tooltip("10초간 에러 물류만 연속 스폰되는 버스트 시작 확률")]
        [Range(0f, 1f)]
        public float errorLogisticsBurstRate = 0.001f;
        [Tooltip("에러 물류 버스트 지속 시간(초)")]
        public float errorLogisticsBurstDurationSeconds = 10f;
        [Tooltip("연속 에러 물류 알림 기본 임계값(대시보드에서 변경 가능)")]
        public int defaultConsecutiveErrorAlertThreshold = 5;

        [Header("인식")]
        public float classifierConfidenceThreshold = 0.55f;
        public int maxRecognitionRetries = 2;

        [Header("택배·물류(시뮬레이션)")]
        [Range(0f, 1f)] public float urgentPackageRate = 0.12f;
        [Range(0f, 1f)] public float highPriorityPackageRate = 0.28f;
        public float deadlineMinMinutes = 8f;
        public float deadlineMaxMinutes = 22f;

        [Header("구역 혼잡(시뮬레이션 임계)")]
        public int congestionCautionQueue = 3;
        public int congestionWarningQueue = 5;
        public int congestionSevereQueue = 7;
        public float congestionSpawnScaleFactor = 1.35f;

        [Header("중앙뇌")]
        public int defaultBrainIntervalMinutes = 30;
        public bool triggerBrainOnError;

        [Header("대시보드")]
        public int dashboardPort = 8080;
        public float dashboardSseIntervalSeconds = 1f;
    }
}
