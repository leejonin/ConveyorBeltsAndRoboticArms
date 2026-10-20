using SmartFactory.Conveyor;
using SmartFactory.Core;

namespace SmartFactory.Logistics
{
    /// <summary>혼잡·지연·가상 설비·장비 안내 규칙.</summary>
    public static class RegionCongestionMonitor
    {
        public static RegionCongestionLevel EvaluateLevel(
            int queueLength,
            int caution,
            int warning,
            int severe)
        {
            if (queueLength >= severe)
            {
                return RegionCongestionLevel.Severe;
            }

            if (queueLength >= warning)
            {
                return RegionCongestionLevel.Congested;
            }

            if (queueLength >= caution)
            {
                return RegionCongestionLevel.Caution;
            }

            return RegionCongestionLevel.Normal;
        }
    }

    public static class DelayRiskEvaluator
    {
        public static DelayRiskLevel Evaluate(float minutesToDeadline, int queuePosition)
        {
            if (minutesToDeadline < -999f)
            {
                return DelayRiskLevel.Unknown;
            }

            var effective = minutesToDeadline - queuePosition * 0.5f;
            if (effective < 0f)
            {
                return DelayRiskLevel.AtRisk;
            }

            if (effective < 5f)
            {
                return DelayRiskLevel.Caution;
            }

            return DelayRiskLevel.Normal;
        }
    }

    public static class VirtualPlantSimulator
    {
        public static float EstimateEnergyUse(
            VirtualPlantMode mode,
            FactoryRegionSetup[] regions,
            ConveyorBelt main,
            float sessionSeconds)
        {
            var beltCount = 1 + (regions != null ? regions.Length : 0);
            var armCount = 0;
            if (regions != null)
            {
                for (var i = 0; i < regions.Length; i++)
                {
                    if (regions[i]?.RobotArms != null)
                    {
                        armCount += regions[i].RobotArms.Length;
                    }
                }
            }

            var factor = mode switch
            {
                VirtualPlantMode.Throughput => 1.4f,
                VirtualPlantMode.Eco => 0.65f,
                _ => 1f
            };

            return (beltCount * 0.08f + armCount * 0.05f) * (sessionSeconds / 3600f) * factor;
        }

        public static float EstimateTemperature(VirtualPlantMode mode, float energyKwh)
        {
            var baseTemp = 28f + energyKwh * 12f;
            return mode == VirtualPlantMode.Eco ? baseTemp - 3f : baseTemp;
        }
    }

    public static class EquipmentFaultCatalog
    {
        public static string GetGuidanceForRecognition(RecognitionErrorCode code)
        {
            return code switch
            {
                RecognitionErrorCode.QrReadFail =>
                    "QR 판독 실패: 라벨 부착·조명·스캐너 초점을 확인하세요.",
                RecognitionErrorCode.ClassifyLowConf =>
                    "분류 신뢰도 미달: 재촬영 또는 모델 입력을 확인하세요.",
                RecognitionErrorCode.LabelMismatch =>
                    "QR·카메라 불일치: 정상 구역 분류를 중단하고 예외 구역으로 이동합니다.",
                RecognitionErrorCode.DestinationInvalid =>
                    "목적지 코드 오류: 등록된 A/B/C 구역과 QR 목적지를 확인하세요.",
                _ => string.Empty
            };
        }

        public static string GetGuidance(EquipmentFaultKind kind)
        {
            return kind switch
            {
                EquipmentFaultKind.PickFailure =>
                    "집기 실패: 택배 위치·그리퍼 정렬·흡착 상태를 확인하세요.",
                EquipmentFaultKind.ConveyorStall =>
                    "벨트 정지: 전원·모터·장애물 여부를 확인하세요.",
                EquipmentFaultKind.CameraTimeout =>
                    "카메라 응답 없음: 연결과 인식 프로세스를 확인하세요.",
                EquipmentFaultKind.CommunicationLoss =>
                    "통신 끊김: 케이블·포트·제어기 전원을 확인하세요.",
                EquipmentFaultKind.VirtualOverheat =>
                    "가상 온도 경고(시뮬): 부하를 줄이거나 가동을 일시 중지하세요.",
                EquipmentFaultKind.RepeatedFailure =>
                    "반복 오류: 원인 확인 전 반복 실행하지 마세요.",
                _ => "확인 필요: 로그와 장비 상태를 점검하세요."
            };
        }
    }
}
