using System.Threading;
using System.Threading.Tasks;
using SmartFactory.Core;
using SmartFactory.Logistics;

namespace SmartFactory.Brain
{
    /// <summary>
    /// 규칙 기반 모의 중앙뇌. LLM 업체 확정 전 교체 가능한 구현체입니다.
    /// </summary>
    public class MockCentralBrainClient : ICentralBrainClient
    {
        /// <inheritdoc />
        public Task<BrainDecision> RequestAsync(FactorySnapshot snapshot, CancellationToken cancellationToken)
        {
            var logisticsNote = snapshot.logistics != null && snapshot.logistics.ops != null
                ? $" {snapshot.logistics.ops.summaryText}"
                : string.Empty;
            var decision = new BrainDecision
            {
                brainState = "Analyzing",
                report =
                    $"불일치 {snapshot.sorting.mismatchCount}개, 인식실패 {snapshot.sorting.recognitionFailureCount}개, 미처리 {snapshot.sorting.unprocessedCount}개.{logisticsNote}"
            };

            if (snapshot.sorting.mismatchCount > 3)
            {
                decision.actions.Add(new BrainAction
                {
                    target = "Conveyor",
                    command = "setSpeed",
                    args = "0.8"
                });
            }

            foreach (var robot in snapshot.robots)
            {
                if (!robot.isNormal || robot.hasBug)
                {
                    decision.actions.Add(new BrainAction
                    {
                        target = robot.id,
                        command = "pause",
                        args = string.Empty
                    });
                    decision.escalations.Add(new BrainEscalation
                    {
                        to = "maintenance",
                        message = $"{robot.id} 오류: {robot.errorDetail}"
                    });
                }
            }

            if (snapshot.lineHealth != null && snapshot.lineHealth.emergencyActive)
            {
                decision.brainState = "Emergency";
                decision.report = snapshot.lineHealth.emergencyMessage ?? "벨트 정체 및 로봇 무움직임 감지.";
                var stalled = snapshot.lineHealth.stalledRobotIds;
                if (stalled != null && stalled.Count > 0)
                {
                    foreach (var robotId in stalled)
                    {
                        decision.actions.Add(new BrainAction
                        {
                            target = robotId,
                            command = "reset",
                            args = string.Empty
                        });
                    }

                    decision.escalations.Add(new BrainEscalation
                    {
                        to = "supervisor",
                        message = $"긴급 정체: 중앙뇌가 정체 로봇팔 초기화를 실행합니다. ({string.Join(", ", stalled)})"
                    });
                }
            }

            if (snapshot.conveyor != null && !string.IsNullOrEmpty(snapshot.conveyor.errorDetail))
            {
                decision.escalations.Add(new BrainEscalation
                {
                    to = "supervisor",
                    message = $"벨트 오류: {snapshot.conveyor.errorDetail}"
                });
            }

            decision.brainState = "Reported";
            return Task.FromResult(decision);
        }

        /// <summary>운영 보고서 요약 문장 다듬기(LLM 대체, 수치 변경 없음).</summary>
        public static string PolishReportSummary(OpsMetricsDto ops)
        {
            if (ops == null)
            {
                return string.Empty;
            }

            return ops.summaryText + " (규칙 기반 요약·수치는 시스템 집계값)";
        }
    }
}
