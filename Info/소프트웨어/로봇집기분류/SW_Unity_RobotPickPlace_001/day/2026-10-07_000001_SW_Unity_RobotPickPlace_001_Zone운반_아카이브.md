# 로봇팔 2단계: 판정별 Zone 운반·내려놓기

- 작성일: 2026-10-11
- 선행: [집기·들어올리기 아카이브](2026-10-07_000000_SW_Unity_RobotPickPlace_001_집기들어올리기_아카이브.md)

## 재사용한 기존 기능

- `PickAndLiftRoutine` (ApproachPick → ReachPick → 정렬 → TryGrab → LiftHold)
- `MoveToPose` / `RobotArmRotationDriver` 관절 보간
- `AlignGripCenterTowardItem` hill-climb 패턴 → `ReachGripWorldTarget` / `AlignGripCenterTowardWorldPoint`
- `TryGrab` / FixedJoint / `HasActiveGrabJoint`
- `GripOpen` 포즈, `PickLiftTrace` 실패 로그

## 추가·수정

| 구분 | 내용 |
|------|------|
| 판정 | `ItemSortOutcome` (Pass/Fail/Error), `RobotStation`에서 산출 → `SchedulePick(..., outcome)` |
| 파이프라인 | `FactoryItem.IsRobotPipelineActive`, `SuppressUnprocessedStatIncrement` |
| Zone | `RobotArmZoneTargets` (Inspector 명시 Transform), `RobotArmPlacementGeometry` |
| 운반 | `RunPickLiftAndPlaceTask` → LiftHold 후 travel/place reach → GripOpen → `ReleaseHeldItemAtZone` → Idle |
| 디스패처 | `RunPickLiftAndPlaceTask` 호출, `ItemSpawner` 주입 |
| Unprocessed | held/pipeline 중 `OnTriggerEnter` 무시; 로봇 FAIL 배치 Despawn 시 통계 중복 방지 |

## Zone 연결

| 대상 | 연결 |
|------|------|
| `RobotArm.prefab` | `RobotArmZoneTargets`: NormalZone / UnprocessedZone / ErrorZone Transform (프리팹 자식, fileID 연결) |
| Rebuild 레이아웃 | 프리팹 refs 우선; 비어 있으면 `SortZoneMap` 전역 Zone 폴백 (`FactoryLayoutBuilder`, `FactoryOrchestrator`) |
| 판정 매핑 | Pass→Normal, Fail→Unprocessed, Error→Error |

## 검증

| 항목 | 결과 |
|------|------|
| `dotnet build SmartFactory.csproj` | **성공** (2026-10-11) |
| Unity Play PASS/FAIL/ERROR 배치 | **미검증** |
| 순간이동 없음·집기 회귀·Unprocessed 조기 제거 | **미검증** |

Play 시 `pick-lift-trace.log` 및 Console `[RobotArm] Place complete` / `PlaceFailed` 확인.
