# Zones 위치 보고서

- **작성일:** 2026-10-07  
- **재조사:** 2026-10-07 (프리팹·`Factory.unity` YAML 재추출)  
- **분석 대상:** `Assets/RobotArm/Prefabs/RobotArm.prefab` (`guid: d621a291b7d0c5e408dad10081158277`)  
- **기준 좌표:** `Robotic-Arm-Rig`와 동일한 **`RobotArm` 루트 로컬** (리그 `localPosition` = `(0, 0, 0)`)  
- **데이터 출처:** 프리팹·씬 직렬화 (Play 미실행)

---

## 1. 요약

| 구분 | 내용 |
|------|------|
| **프리팹 내 Zone** | `NormalZone`, `ErrorZone`, `UnprocessedZone` 3개 — **`Robotic-Arm-Rig`와 형제**, `RobotArm` 직하 |
| **공통 X** | 로컬 **+0.842 m** (리그 원점에서 +X) |
| **Z 배열 (2026-10-07 프리팹)** | **Unprocessed → Normal → Error** 순으로 **+Z** 방향으로 멀어짐 (과거 보고서의 −Z 배열과 **다름**) |
| **공장 런타임 Zone** | `Factory.unity` / `FactoryLayoutBuilder`의 **전역 3 Zone + BeltEnd** — 프리팹 좌표와 **무관**, `SortZoneMap`은 전역만 참조 |

---

## 2. 프리팹 계층 (Zone · Rig)

```
RobotArm                          ← RobotArmController, 피벗
├── NormalZone                    ← 1번째 자식
├── ErrorZone
├── UnprocessedZone               ← UnprocessedZone.cs + BoxCollider
└── Robotic-Arm-Rig               ← modelRoot (0,0,0)
    ├── Arm Rig                   ← local (−0.009, 0.148, −0.017), scale 100
    │   └── Base → Principal → Brazo1~3 → Gancho… → Piston_end
    └── (SkinnedMesh Cylinder.* …)
```

- Zone은 **관절 회전과 무관**한 고정 마커(로컬만 정의).
- **자식 순서:** Normal → Error → Unprocessed → Robotic-Arm-Rig.

---

## 3. 프리팹 Zone 위치·간격 (RobotArm / Rig 로컬)

| Zone | 코드상 역할 | Local Position (X, Y, Z) | Local Scale |
|------|-------------|---------------------------|-------------|
| **UnprocessedZone** | `SortZoneMap.unprocessedZone`, `UnprocessedZone` | **(0.842, 0, 0.032)** | (0.8, 0.05, 0.8) |
| **NormalZone** | `SortZoneMap.defaultNormalZone` | **(0.842, 0, 1.000)** | (0.8, 0.05, 0.8) |
| **ErrorZone** | `SortZoneMap.errorZone` | **(0.842, 0, 1.885)** | (0.8, 0.05, 0.8) |

### 3.1 Zone 간 ΔZ (동일 X)

| 구간 | ΔZ (m) |
|------|--------|
| Unprocessed ↔ Normal | **0.968** |
| Normal ↔ Error | **0.885** |
| Unprocessed ↔ Error | **1.853** |

### 3.2 Top-Down (+X 오른쪽, +Z 위)

```
                         +Z
                          │
                   ErrorZone (0.842, 1.885)
                          │
                  NormalZone (0.842, 1.0)
                          │
              UnprocessedZone (0.842, 0.032)
                          │
    ─── Rig / RobotArm 원점 (0,0) ───► +X
                          │
                         −Z
```

- 팔 피벗 기준 Zone은 **전부 +X 약 0.84 m**, **+Z 쪽으로** Unprocessed(가장 가까움) → Error(가장 멀음).

### 3.3 이전 보고 대비 변경 (프리팹 편집 반영)

| Zone | 이전 문서 Z | **현재 프리팹 Z** |
|------|-------------|-------------------|
| ErrorZone | 0 | **1.885** |
| NormalZone | −0.885 | **1.000** |
| UnprocessedZone | −1.853 | **0.032** |

---

## 4. 영역(크기) · 콜라이더

- 메시: Unity **Cylinder** (높이 2, 반경 0.5 로컬).
- `localScale (0.8, 0.05, 0.8)` → 지면 투영 **반경 ≈ 0.40 m**, 두께 **≈ 0.10 m**.

| Zone | CapsuleCollider | 기타 |
|------|-----------------|------|
| Normal / Error | r=0.5, h=2, Y축, **非 trigger** | — |
| Unprocessed | 동일 Capsule | **BoxCollider** (1,2,1) + `UnprocessedZone` → **Awake에서 Box만 trigger** |

`FactoryLayoutBuilder.CreateZone`은 원통 **Collider 제거**; 프리팹 Zone은 Capsule(± Box) **유지**.

---

## 5. 로봇 배치 시 프리팹 Zone 월드 좌표

`CreateFromPrefab` / `FactoryLayoutBuilder`: 팔 **`(x, 0, 1.5)`**, 회전 0 가정.

**월드 (FactoryRoot 기준):** `(x + 0.842, 0, 1.5 + z_zone)`

| Zone | z_zone | x = −2 | x = 0 | x = 2 |
|------|--------|--------|-------|-------|
| Unprocessed | 0.032 | (−1.158, 0, **1.532**) | (0.842, 0, **1.532**) | (2.842, 0, **1.532**) |
| Normal | 1.000 | (−1.158, 0, **2.500**) | (0.842, 0, **2.500**) | (2.842, 0, **2.500**) |
| Error | 1.885 | (−1.158, 0, **3.385**) | (0.842, 0, **3.385**) | (2.842, 0, **3.385**) |

### 5.1 컨베이어와의 상대 위치 (`FactoryConfig` 기본: `beltLength=12`)

| 오브젝트 | 대표 위치 (FactoryRoot) |
|----------|-------------------------|
| ConveyorBelt 중심 | (0, **0.1**, 0), scale (12, 0.2, **1.2**) → X 약 ±6, **Z 약 ±0.6** |
| SpawnPoint | (−5.4, 0.35, 0) |
| RobotArm (코드 기본) | **(−2 / 0 / 2, 0, 1.5)** — 벨트 **+Z 측** (Z=1.5) |
| 프리팹 Zone (팔 1대) | X ≈ 팔 X + 0.84, **Z ≈ 1.5 ~ 3.4** — 벨트 **바깥 +Z** |

→ 프리팹 Zone은 **벨트 라인(Z≈0) 위가 아니라**, 팔 옆 **+Z 열**에 모여 있음.

### 5.2 프리팹 에셋 루트

- `RobotArm` 프리팹 파일 루트: **localPosition (−2, 0, 1.5)** (에셋 기본값). 인스턴스는 배치 코드로 **덮어씀**.

---

## 6. `Factory.unity` — 실제 연결 Zone (전역)

`FactorySceneLayout` / `SortZoneMap`이 가리키는 Transform (YAML 기준):

| 오브젝트 | Position (월드≈로컬, FactoryLayout 부모가 원점) | Scale | Hierarchy 비고 |
|----------|--------------------------------------------------|-------|----------------|
| **NormalZone** | **(4, 0, 2)** | (0.8, 0.05, 0.8) | **씬 루트** (`m_Father: 0`) |
| **ErrorZone** | **(4, 0, −2)** | 동일 | 씬 루트 |
| **UnprocessedZone** | **(6, 0, 0)** | 동일 + Box + `UnprocessedZone` | 씬 루트 |
| **BeltEndUnprocessedTrigger** | **(5.4, 0.5, 0)** | — | **FactoryRoot** 자식, Box trigger |

- `FactoryRoot` 자식 목록에는 **위 3 Sort Zone 없음** (벨트·스폰·스테이션·팔·BeltEnd만 포함).
- `beltEndUnprocessedZones`: **BeltEnd** + **전역 UnprocessedZone** 컴포넌트 둘 다 등록됨.

### 6.1 로봇·스테이션 (FactoryRoot, 재조사)

**2026-10-07 `FactoryLayoutBuilder` (Rebuild 후)**  
- `RobotStation_1` 1개: `(spawnPoint.x + 1.2, 0.5, 0)`  
- `RobotArm_1`~`3`: `(−2/0/2, 0, 1.5)`, `PickAnchor` local `(0, 0.35, −1.5)` — 집기는 **Zone 좌표 미사용**  
- 상세: [1단계 집기 아카이브](2026-10-07_000000_SW_Unity_RobotPickPlace_001_집기들어올리기_아카이브.md)

아래 표는 **Rebuild 이전 `Factory.unity` 스냅샷**입니다.

|对象 | Position | 비고 |
|------|----------|------|
| RobotStation_1 | (−2, 0.5, 0) | QrScanner / ShapeCamera 자식 |
| RobotStation_2 | (0, 0.5, 0) | |
| RobotStation_3 | (2, 0.5, 0) | |
| RobotArm_2 | (0, 0, **1.5**) | Rig만 자식, **프리팹 Zone 자식 없음** |
| RobotArm_3 | (2, 0, **1.5**) | 동일 |
| **RobotArm** (PrefabInstance) | **(−2, 0, 1.5)** | **씬 루트**에 프리팹 인스턴스 1개 (`RobotArm.prefab` 전체, **Zone 포함 가능**) |

- `RobotArm_1` 이름 오브젝트는 없음. Station 1 쪽은 **「Robotic Arm」** FBX 계층 + **씬 루트 PrefabInstance `RobotArm`** 조합으로 보임.
- **런타임 분류 목표(`SortZoneMap`)** → **(4,±2), (6,0)** 전역 Zone. 프리팹 내 Zone은 **맵에 연결되지 않음**.

---

## 7. 코드상 Zone 용도

| API | 용도 |
|-----|------|
| `SortZoneMap` | 2단계 예정 (`RunSortTask`); 1단계 런타임 미사용 |
| `ConveyorPickDispatcher` | `SchedulePick` → `RunPickAndLiftTask` |
| `UnprocessedZone` | 트리거 → `ItemSpawner.Despawn`, 미처리 카운트 |
| `RobotArmController.SortRoutine` | **`dropTarget.position` 미사용**, 포즈 시퀀스만 — Zone은 **의미·null 체크** 위주 |

---

## 8. Robotic-Arm-Rig 기준 해석

| 기준 | RobotArm 로컬 |
|------|----------------|
| Rig 원점 | (0, 0, 0) |
| Arm Rig 래퍼 | (−0.009, 0.148, −0.017), scale 100 |
| Zone Y | **0** (루트 지면) |
| Zone X | **+0.842** |
| Zone Z | **0.032 / 1.0 / 1.885** |

---

## 9. 정리 · 권장

1. **현재 프리팹:** +X 0.842 m 선상, **+Z**로 Unprocessed → Normal → Error (**최대 Z 1.885**).  
2. **공장 시뮬:** `SortZoneMap` = **전역 (4,±2), (6,0)** + BeltEnd **(5.4, 0.5, 0)** — 프리팹 Zone과 **좌표·계층 모두 불일치**.  
3. **씬 정리:** 전역 Zone·`RobotArm` PrefabInstance가 **씬 루트**에 있어 Hierarchy가 분산됨 — `FactoryRoot` 하위로 옮기거나 `SortZoneMap`을 **프리팹 Zone Transform**에 연결하는 편이 튜닝에 유리.  
4. **포즈·내려놓기:** Zone 위치 변경만으로는 팔 동작이 따라가지 않음 — `RobotArmPoseSet` / `ApproachDrop` 별도 정렬 필요.

---

## 10. 참고 파일

- `Assets/RobotArm/Prefabs/RobotArm.prefab`  
- `Assets/SmartFactory/Scenes/Factory.unity`  
- `Assets/SmartFactory/Scripts/FactoryLayoutBuilder.cs`  
- `Assets/SmartFactory/Scripts/FactorySceneLayout.cs`  
- `Assets/SmartFactory/Scripts/Robot/SortZoneMap.cs`  
- `Assets/SmartFactory/Scripts/Robot/RobotStation.cs`  
- `Assets/SmartFactory/Scripts/Items/UnprocessedZone.cs`
