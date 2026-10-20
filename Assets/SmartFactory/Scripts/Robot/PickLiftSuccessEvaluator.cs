using System.Collections;
using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// LiftHold 이후 동반 상승·유지·벨트 이탈 등 성공 조건을 샘플링해 판정합니다.
    /// </summary>
    public class PickLiftSuccessEvaluator : MonoBehaviour
    {
        // 기본값 출처: 계획 §2 초안 + 벨트 표면·LiftHold 포즈 ΔY 추정. Unity Play 1회 측정 전까지 Serialize 유지.
        [Header("성공 기준 (씬 측정 후 튜닝, 임의 완화 금지)")]
        [SerializeField] float minContactSeconds = 0.05f;
        [SerializeField] float minJointHoldSeconds = 0.2f;
        [SerializeField] float minLiftDeltaY = 0.06f;
        [SerializeField] float minOffBeltSeconds = 0.25f;
        [SerializeField] float maxRelativeDriftMeters = 0.12f;
        [SerializeField] float beltSpeedEpsilon = 0.35f;
        [SerializeField] float beltSurfaceY = 0.35f;
        [SerializeField] float offBeltHeightMargin = 0.08f;
        [SerializeField] float sampleDurationSeconds = 1.5f;
        [SerializeField] float sampleIntervalSeconds = 0.05f;

        /// <summary>직전 run evaluator PASS 여부.</summary>
        public bool LastRunPassed { get; private set; }

        /// <summary>
        /// LiftHold 진입 후 평가 코루틴을 시작합니다.
        /// </summary>
        public void BeginEvaluation(
            int runId,
            int armIndex,
            FactoryItem item,
            RobotArmController arm,
            RobotArmGanchoContactProbe probe1,
            RobotArmGanchoContactProbe probe2,
            float contactSecondsAtGrab,
            float beltSpeed)
        {
            StartCoroutine(EvaluateRoutine(
                runId, armIndex, item, arm, probe1, probe2, contactSecondsAtGrab, beltSpeed));
        }

        IEnumerator EvaluateRoutine(
            int runId,
            int armIndex,
            FactoryItem item,
            RobotArmController arm,
            RobotArmGanchoContactProbe probe1,
            RobotArmGanchoContactProbe probe2,
            float contactSecondsAtGrab,
            float beltSpeed)
        {
            if (item == null || arm == null)
            {
                LastRunPassed = false;
                PickLiftTrace.LogPhase(runId, armIndex, -1, "EvaluateAbort", "{\"cause\":\"null refs\"}");
                yield break;
            }

            var itemId = item.ItemId;
            var grip = arm.GripCenter;
            var startItemY = item.transform.position.y;
            var startRel = grip != null ? grip.position - item.PickAlignWorldPosition : Vector3.zero;
            var maxDrift = 0f;
            var offBeltAccum = 0f;
            var jointHoldAccum = 0f;
            var beltViolations = 0;
            var beltHeldSkipSamples = 0;
            var samples = 0;
            var elapsed = 0f;

            while (elapsed < sampleDurationSeconds && item != null && item.gameObject.activeInHierarchy)
            {
                yield return new WaitForSeconds(sampleIntervalSeconds);
                elapsed += sampleIntervalSeconds;
                samples++;

                if (item.IsHeldByRobot && arm.HasActiveGrabJoint(item))
                {
                    jointHoldAccum += sampleIntervalSeconds;
                }

                var itemY = item.transform.position.y;
                if (itemY >= beltSurfaceY + offBeltHeightMargin)
                {
                    offBeltAccum += sampleIntervalSeconds;
                }

                if (grip != null)
                {
                    var rel = grip.position - item.PickAlignWorldPosition;
                    var drift = Vector3.Distance(rel, startRel);
                    if (drift > maxDrift)
                    {
                        maxDrift = drift;
                    }
                }

                var rb = item.ItemRigidbody;
                if (rb != null && item.IsHeldByRobot &&
                    Mathf.Abs(rb.linearVelocity.x - beltSpeed) > beltSpeedEpsilon)
                {
                    beltViolations++;
                }

                if (item.IsHeldByRobot && item.BeltHeldSkipLastStep)
                {
                    beltHeldSkipSamples++;
                }
            }

            var liftDeltaY = item != null ? item.transform.position.y - startItemY : 0f;
            var contactOk = contactSecondsAtGrab >= minContactSeconds;
            var jointOk = jointHoldAccum >= minJointHoldSeconds;
            var liftOk = liftDeltaY >= minLiftDeltaY;
            var offBeltOk = offBeltAccum >= minOffBeltSeconds;
            var driftOk = maxDrift <= maxRelativeDriftMeters;
            var beltOk = beltViolations == 0;

            LastRunPassed = contactOk && jointOk && liftOk && offBeltOk && driftOk && beltOk;

            PickLiftTrace.LogPhase(
                runId,
                armIndex,
                itemId,
                LastRunPassed ? "EvaluatePass" : "LiftHoldFailedCriteria",
                "{\"contactOk\":" + Bool(contactOk) + ",\"jointOk\":" + Bool(jointOk) + ",\"liftOk\":" + Bool(liftOk) +
                ",\"offBeltOk\":" + Bool(offBeltOk) + ",\"driftOk\":" + Bool(driftOk) + ",\"beltOk\":" + Bool(beltOk) +
                ",\"contactSec\":" + contactSecondsAtGrab + ",\"jointHoldSec\":" + jointHoldAccum +
                ",\"liftDeltaY\":" + liftDeltaY + ",\"offBeltSec\":" + offBeltAccum + ",\"maxDrift\":" + maxDrift +
                ",\"beltViolations\":" + beltViolations + ",\"beltHeldSkipSamples\":" + beltHeldSkipSamples +
                ",\"samples\":" + samples + "}");

            if (LastRunPassed)
            {
                PickLiftTrace.EndRun("RunSuccess");
            }
            else
            {
                PickLiftTrace.EndRun("RunFailedCriteria");
            }
        }

        static string Bool(bool v) => v ? "true" : "false";
    }
}
