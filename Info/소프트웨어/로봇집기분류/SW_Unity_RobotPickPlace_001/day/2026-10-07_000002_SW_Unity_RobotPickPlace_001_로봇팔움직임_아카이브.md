# 로봇팔 움직임 지시서

- 작성일: 2026-10-06
- 최종 갱신: 2026-10-08 (SmartFactory 코드 기준)
- 작성 기준: 사용자 지정 오브젝트명·회전축·가동 범위 + `RobotArmRotationDriver` / `RobotArmController` 구현

## 1. 움직임 구현 조건

- 로봇팔의 모든 움직임은 **Rotation(회전)으로만** 구현한다.
- 관절·집게 동작에 Position(위치 이동)·Scale(크기 변경)을 **코드에서 적용하지 않는다.**
- 지정된 오브젝트의 회전축과 가동 범위를 준수한다(`RobotArmRigBinding` clamp).
- Rig(`Arm Rig`)·`Base` Transform은 **회전 제어 대상이 아니다.** (프리팹 바인드 포즈만 저장)
- `Piston_end`·`CenterOfTheGripper`는 **회전하지 않으며**, 경로 해석·집기 기준점으로만 사용한다.

## 2. 오브젝트별 회전축 및 가동 범위

| 오브젝트명 | 회전축 | 가동 범위 | 코드 제어 |
| --- | --- | --- | --- |
| `Principal` | Y | −360° ~ +360° | ○ |
| `Principal` | Z | −90° ~ +90° | ○ |
| `Brazo1` | Z | −120° ~ +120° | ○ |
| `Brazo2` | Z | −120° ~ +120° | ○ |
| `Brazo3` | Z | −120° ~ +120° | ○ |
| `Gancho1` | Z | −50° ~ −15° | ○ |
| `Gancho2` | Z | +15° ~ +50° | ○ |
| `Base` | — | (미정의) | ✕ |
| `Arm Rig` | — | (미정의) | ✕ |
| `Piston` / `Piston_end` | — | — | ✕ (참조만) |
| `CenterOfTheGripper` | — | — | ✕ (집기 정렬 기준점) |

`Gancho1` 표기는 사용자 지정 “−15° ~ −50°”를 최소·최대 순으로 적은 것과 동일하다. 코드에서는 `gancho1ZMin = −50`, `gancho1ZMax = −15`이다.

## 3. 오브젝트별 상세 조건 (요구사항)

### 3.1 Principal

- Y축 360° 회전 가능.
- Z축 −90° ~ +90°.

### 3.2 Brazo1·Brazo2·Brazo3

- 각각 Z축 회전.
- −120° ~ +120°.

### 3.3 Gancho1

- Z축 회전.
- −50° ~ −15°.

### 3.4 Gancho2

- Z축 회전.
- +15° ~ +50°.

## 4. 코드 구현 규칙 (`RobotArmRotationDriver`)

### 4.1 각도 적용 방식

- 초기화 시 각 관절의 **바인드 로컬 회전**(`localRotation`)을 저장한다.
- 포즈·보간 결과는 **바인드 대비 추가 각도(도)** 로 해석한다.
- `Principal`: `bind * Euler(0, principalY, 0) * Euler(0, 0, principalZ)`.
- `Brazo1`~`3`, `Gancho1`·`2`: `bind * Euler(0, 0, zDegrees)`.
- 모든 목표 각도는 `RobotArmRigBinding`의 min/max로 **Clamp** 후 적용한다.

### 4.2 포즈 보간

- `RobotArmPoseSet`의 named pose(각 DOF 필드, 단위: 도)를 `RobotArmController.MoveToPose`가 `poseBlendDuration`(기본 1.2s) 동안 선형 보간한다.
- 집기 대기 중에는 `AlignGripCenterTowardItem`이 **PrincipalY/Z, Brazo1Z~3Z**만 ±2° 시험 각도로 미세 조정한다(집게 DOF는 포즈 전환 시에만).

### 4.3 집게 열림·닫힘 (포즈 관례)

| 상태 | gancho1Z | gancho2Z | 비고 |
| --- | --- | --- | --- |
| 열림(기본·Reach) | −15 | +15 | 범위 내 “벌린” 쪽 |
| 닫힘(GripClosed*) | −50 | +50 | 범위 내 “닫힌” 쪽 |

드라이버 초기 각도: Gancho1 = −15, Gancho2 = +15 (`CacheBindPose`).

### 4.4 리그 해석 (`RobotArmRigBinding`)

- 에셋: `Assets/Resources/SmartFactory/RoboticArmRigBinding.asset` (프리팹·빌더에서 동일 경로 사용).
- 기본 본 경로: `Principal`, `Brazo1`~`3`, `Gancho1`·`2`, `Piston_end`, `CenterOfTheGripper`.
- `visualRootPath`가 비어 있으면 모델 루트에서 **이름 깊이 검색**으로 Transform을 찾는다.

### 4.5 집기 기준점

- **`CenterOfTheGripper`**: `Brazo3` 자식. 프리팹 `RobotArm.prefab`에 배치.
- 벨트 집기 시 **물류 pivot과 grip center 월드 좌표**가 `gripAlignTolerance`(기본 0.12m) 이내일 때만 `Gancho` 닫기·`TryGrab` 진행.
- 물리 고정: `FixedJoint`는 **`Brazo3`** GameObject에 추가해 물류 Rigidbody에 연결.
- `Piston_end`는 `EndEffector` 참조·상태 보고용; 집기 거리 판정에는 사용하지 않는다.

## 5. 포즈 세트 (기본값)

에셋: `Assets/Resources/SmartFactory/RobotArmDefaultPoseSet.asset` (동일 내용: `Assets/RobotArm/RobotArmDefaultPoseSet.asset`).

| poseName | 용도 |
| --- | --- |
| `Idle` | 대기 |
| `ApproachPick` | 집기 접근 1단계 |
| `ReachPick` | 벨트 쪽 전방·높이 맞춤 2단계 |
| `GripClosed` / `GripClosedAtReach` | 집게 닫힘(Reach 자세 유지 버전) |
| `LiftHold` | 집은 뒤 들어 올림 유지 |
| `ApproachDrop` / `GripOpen` | (2단계 이후) 내려놓기·분류용 |

### 5.1 1단계 집기·들어 올리기 시퀀스 (`PickAndLiftRoutine`)

1. `ApproachPick` → `ReachPick`
2. Pick 창(`RobotArmPickSlot`) + grip center 정렬 대기 (`AlignGripCenterTowardItem`, 타깃 `PickAlignPoint`)
3. `GripClosedAtReach`(없으면 `GripClosed`, **보간 중에도 정렬 유지**) → Gancho 접촉 충족 시 `TryGrab`
4. `LiftHold` (`IsBusy` 유지)

관련 코드: `RobotArmController`, `ConveyorPickDispatcher`, `RobotArmPickSlot`.

## 6. 확인 기준

- 관절·집게가 **Rotation만**으로 움직이는지 (`RobotArmRotationDriver`).
- 제어 대상 이름이 §2 표와 `RobotArmRigBinding` 경로와 일치하는지.
- 각 DOF가 바인딩 clamp 범위를 넘지 않는지.
- 집기 시 `CenterOfTheGripper`와 **`FactoryItem.PickAlignPoint`**(Collider 중심) 정렬 → Gancho 접촉 → Brazo3 `FixedJoint` → LiftHold 동반 상승.
- Unity Play 검증: 프로젝트 루트 `pick-lift-trace.log`의 `EvaluatePass` 및 [1단계 집기 아카이브](2026-10-07_000000_SW_Unity_RobotPickPlace_001_집기들어올리기_아카이브.md) §7 (**2026-10-11 기준 Play 미검증**).

## 7. 원본 지시에서 코드가 채우지 않은 항목

다음은 여전히 씬·FBX·튜닝에 의존하며, 본 문서만으로 수치를 고정하지 않는다.

- 프리팹 FBX **초기 bind pose**와 포즈 각도의 기하학적 대응(ReachPick 각도는 레이아웃·벨트 높이에 맞게 에셋 튜닝).
- `Principal` Y 연속 회전·각도 래핑 정책(현재는 clamp ±360).
- 집게 Collider 크기·물리 마찰(프리팹 `Gancho1_end` / `Gancho2_end`).

---

§1~§3은 사용자 원본 요구사항이다. §4~§6은 2026-10-08 기준 SmartFactory 구현과의 대응 관계다.
