# 로봇팔 1단계: 집기·들어 올리기 구현 보고서

- 작성일: 2026-10-11 (검증·trace 갱신)
- 선행 문서: [1단계 구현보고서 아카이브](../../../../메인프로젝트/전체구조/001/day/2026-10-06_000000_001_스마트팩토리_1단계_구현보고서_아카이브.md), [로봇팔 움직임 아카이브](2026-10-07_000002_SW_Unity_RobotPickPlace_001_로봇팔움직임_아카이브.md)
- 범위: **도착 예측 → ApproachPick → ReachPick → grip center 정렬 → 집게 닫힘 → TryGrab → LiftHold** (Zone·Despawn·분류 시퀀스 없음)

## 1. 확정 구성

| 항목 | 값 |
| --- | --- |
| RobotStation | 1 (`RobotStation_1`, 스폰 상류) |
| 로봇팔 | 3 (`X` = −2, 0, 2 / `Z` = 1.5) |
| 집기 앵커 | 각 팔 `PickAnchor` local **`(0, 0.7, −1.5)`** (Rebuild 시 `FactoryLayoutBuilder`) |
| 집기 정렬 | `CenterOfTheGripper` ↔ `FactoryItem.PickAlignPoint`(Collider bounds 중심), `gripAlignTolerance` **0.12 m** |
| 집기 대상 | 스캔·인식 절차를 거친 **모든** 물류 (`SchedulePick`) |

## 2. 수정·추가 파일

| 구분 | 경로 |
| --- | --- |
| 신규 | `ConveyorPickDispatcher.cs`, `RobotArmPickSlot.cs` |
| 신규 (검증) | `PickLiftTrace.cs`, `PickLiftSuccessEvaluator.cs`, `RobotArmGanchoContactProbe.cs`, `RobotArmGrabJointHost.cs` |
| 수정 | `RobotStation.cs`, `RobotArmController.cs`, `FactoryLayoutBuilder.cs`, `FactorySceneLayout.cs`, `FactoryOrchestrator.cs` |
| 수정 | `FactoryItem.cs` (`PickAlignPoint`, Visual Collider 기준), `ItemSpawner.cs` (Visual primitive Collider), `ConveyorBelt.cs` |
| 수정 | `RobotArmDefaultPoseSet.asset`, `RobotArm.prefab` (Gancho Collider, `CenterOfTheGripper`) |

## 3. 동작 요약

- **RobotStation**: QR·캡처·비교 후 `dispatcher.SchedulePick`.
- **ConveyorPickDispatcher**: pick X 순 팔 배정, `ETA ≤ lead` 시 `PickLiftTrace.BeginRun` 후 `RunPickAndLiftTask`.  
  `lead = PoseBlendDuration × 3 + alignBudgetSeconds(1.5) + approachLeadMargin`.
- **RobotArmController**: ApproachPick → ReachPick → 정렬 대기(`AlignGripCenterTowardItem`, ±2°/DOF) → **GripClose 보간 중에도 정렬 유지** → `TryGrab`(Gancho 접촉 누적 + 거리 + FixedJoint on Brazo3) → LiftHold → `PickLiftSuccessEvaluator`.
- **ConveyorBelt**: `IsHeldByRobot` 물류는 +X 속도 미적용(`BeltHeldSkipLastStep` trace용).

## 4. 실행 추적·성공 판정

- 로그 파일: 프로젝트 루트 **`pick-lift-trace.log`** (NDJSON, runId 단위).
- **PickLiftSuccessEvaluator** (레이아웃 루트, Rebuild/Orchestrator 자동 부착) — LiftHold 후 **1.5 s** 샘플:

| # | 조건 | Serialize 기본값 | Play 측정 |
| --- | --- | --- | --- |
| 1 | Gancho 접촉 (TryGrab 시점 누적) | ≥ **0.05 s** | **미측정** |
| 2 | Joint connectedBody 유지 | ≥ **0.2 s** | **미측정** |
| 3 | LiftHold 대비 item Y 증가 | ≥ **0.06 m** | **미측정** |
| 4 | 벨트 표면+여유 이상 유지 | ≥ **0.25 s** (`beltSurfaceY=0.35`, margin 0.08) | **미측정** |
| 5 | grip–item 상대 drift | ≤ **0.12 m** | **미측정** |
| 6 | held 중 벨트 속도 강제 | \|velX−beltSpeed\| ≤ **0.35** 위반 0회 | **미측정** |

실패 시 trace `phase=LiftHoldFailedCriteria`. Evaluator **PASS** 없이는 1단계 완료로 보지 않음.

## 5. 과거 실패 가설·대응 (로그 근거)

| 가설 | 근거 | 코드 대응 |
| --- | --- | --- |
| **H-T** lead 과소 | Reach+정렬+Grip 시간 | `leadPoseBlendCount=3`, `alignBudgetSeconds=1.5` |
| **H-G** GripClose 중 정렬 중단 | 0.45 s 보간 동안 물류 이동 | `MoveToPose(..., alignItemDuringBlend)` |
| **H-R** pivot vs Collider | bounds 불일치 | `PickAlignPoint` |
| **H-J** 무접촉 joint | 거리만 맞을 때 연결 | `minGanchoContactSecondsForGrab`, Gancho probe |
| **H-A / H-P** alignDist ~1.7, tolerance 0.12 | 과거 `debug-a62e45.log` | **ReachPick·정렬 DOF 튜닝은 Play trace로 재확인 필요** (임의 tolerance 완화 없음) |

### 5.1 물류 콜라이더 (2026-10-11)

| 항목 | 내용 |
| --- | --- |
| 원인 | `ItemSpawner`가 루트에 **1×1 BoxCollider**를 두고, 스케일된 `Visual` primitive 콜라이더는 제거함 → `PickAlignPoint`·벨트·Gancho 접촉이 **보이는 크기와 불일치** |
| 수정 | 루트 Collider 제거, **Visual 자식 primitive Collider 유지**; 풀 재스폰 시 `StripLegacyRootCollider`; `FactoryItem` 정렬 기준 = Visual bounds |
| Play 검증 | **미실행** — Scene Gizmo·`pick-lift-trace.log`의 `contactSec`·`alignDist` 재확인 |

## 6. 씬 설정

1. `Factory.unity` → **SmartFactory → Rebuild Factory Scene (RobotArm Prefab)** → 저장  
2. Play 2~3분, 종료 후 **`pick-lift-trace.log`** 확인 (`EvaluatePass` / `LiftHoldFailedCriteria`)

## 7. 검증

| 항목 | 결과 |
| --- | --- |
| `dotnet build SmartFactory.csproj` | **성공** (2026-10-11, 물류 콜라이더 정합 포함) |
| Unity Play + evaluator PASS | **미검증** (에이전트 환경에서 Play 불가) |
| Scene 눈으로 집기·상승 | **미검증** |

### Play 체크리스트

- [ ] runId별 itemId·armIndex 일치 (`PickTaskStart`)
- [ ] ApproachPick → ReachPick → AlignSample에서 alignDist 추세
- [ ] GripClose 중 alignDist 유지
- [ ] TryGrab: contactSec > 0, jointConnectedOk
- [ ] LiftHoldEntered 후 `EvaluatePass`
- [ ] Held 후 벨트 +X 미적용 (`beltHeldSkipSamples` > 0)
