# 기능 ID 목록

- 문서 초기 생성 일시: 2026-10-20 17:12 (KST)
- 문서 수정 일시: 2026-10-20 17:45 (KST)
- 지시자: 이종인
- 작업자(문서): Composer — ForAgents v0.0.11 ID 부여·매핑

## 개요

SmartFactory Unity 기능과 문서·코드 경로를 연결한다. 기존 [기능_분류_요구사항](메인프로젝트/전체구조/001/기능_분류_요구사항.md)의 1~5장 번호와 병행한다.

## 중분류명 ↔ 영문 식별자

| 중분류명(한글) | 영문 식별자(ID 3번째 항목) |
| --- | --- |
| 벨트 구동 | ConveyorTransport |
| 물품 생성 | ItemSpawning |
| 물품 인식 | ShapeRecognition |
| 물품 집기·분류 | RobotPickPlace |
| 장비 상태 집계 | FactoryState |
| 중앙뇌 | CentralBrain |
| 제어판 | Dashboard |
| 정체·긴급 감시 | LineHealthMonitor |
| 구역 라우팅 | RegionRouting |
| 주문 우선순위 | PackageOrder |
| 구역 혼잡 | RegionCongestion |
| 인식 오류 코드 | RecognitionError |
| 장비 오류 안내 | EquipmentGuidance |
| 지연 예측 | DelayPrediction |
| 운영 보고서 | OpsReporting |
| 가상 에너지·온도 | VirtualPlant |

## 기능 ID 표

| 기능 ID | 영역 / 실행환경 / 영문중분류 | 기능명 | 대분류 / 중분류 | 기능 설명 | 관련 기능 ID | 코드 경로 | 문서 경로 | 상태 | 이전 ID |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| (메인) 001 | — | 전체 구조·요구사항 | 메인프로젝트 / 전체구조 | 개요·기능분류·구현 현황 | 아래 SW_* 전체 | `Assets/SmartFactory/` | [001/](메인프로젝트/전체구조/001/) | 구현 중 | 해당 없음 |
| SW_Unity_ConveyorTransport_001 | SW / Unity / ConveyorTransport | 컨베이어 벨트 운반 | 소프트웨어 / 벨트구동 | Rigidbody +X 운반, 속도·상태 | SW_Unity_FactoryState_001 | `Assets/SmartFactory/Scripts/Conveyor/ConveyorBelt.cs` | [벨트구동](소프트웨어/벨트구동/SW_Unity_ConveyorTransport_001/컨베이어_벨트_운반.md) | 구현 완료 | 해당 없음 |
| SW_Unity_ItemSpawning_001 | SW / Unity / ItemSpawning | 물품·에러 물류 스폰 | 소프트웨어 / 물품생성 | 풀링, QR, 에러 물류 주입 | SW_Unity_ShapeRecognition_001 | `Assets/SmartFactory/Scripts/Items/ItemSpawner.cs` | [물품생성](소프트웨어/물품생성/SW_Unity_ItemSpawning_001/물품_생성.md) | 구현 완료 | 해당 없음 |
| SW_Unity_ShapeRecognition_001 | SW / Unity / ShapeRecognition | QR·도형 인식 | 소프트웨어 / 물품인식 | 스테이션 스캔·판정 | SW_Unity_RobotPickPlace_001 | `RobotStation.cs`, `QrScanner.cs`, `ShapeCamera.cs` | [물품인식](소프트웨어/물품인식/SW_Unity_ShapeRecognition_001/물품_인식.md) | 구현 완료 | 해당 없음 |
| SW_Unity_RobotPickPlace_001 | SW / Unity / RobotPickPlace | 집기·Zone 배치 | 소프트웨어 / 로봇집기분류 | 흡착·디스패처·Zone | SW_Unity_ShapeRecognition_001, SW_Unity_FactoryState_001 | `RobotArmController.cs`, `ConveyorPickDispatcher.cs` | [로봇집기분류](소프트웨어/로봇집기분류/SW_Unity_RobotPickPlace_001/로봇_집기_분류.md) | 구현 중 | 해당 없음 |
| SW_Unity_FactoryState_001 | SW / Unity / FactoryState | 공장 상태 집계 | 소프트웨어 / 공장상태 | 스냅샷·알림·통계 | SW_Unity_Dashboard_001, SW_Unity_CentralBrain_001 | `FactoryStateRegistry.cs`, `FactoryStatusDtos.cs` | [공장상태](소프트웨어/공장상태/SW_Unity_FactoryState_001/공장_상태_집계.md) | 구현 완료 | 해당 없음 |
| SW_Unity_CentralBrain_001 | SW / Unity / CentralBrain | 중앙뇌 Mock·실행 | 소프트웨어 / 중앙뇌 | 주기 호출·명령 실행 | SW_Unity_FactoryState_001 | `BrainScheduler.cs`, `BrainCommandExecutor.cs`, `MockCentralBrainClient.cs` | [중앙뇌](소프트웨어/중앙뇌/SW_Unity_CentralBrain_001/중앙뇌.md) | 구현 중 | 해당 없음 |
| SW_Unity_Dashboard_001 | SW / Unity / Dashboard | 웹 제어판 | 소프트웨어 / 대시보드 | HttpListener·SSE·API | SW_Unity_FactoryState_001 | `DashboardServer.cs`, `StreamingAssets/Dashboard/` | [대시보드](소프트웨어/대시보드/SW_Unity_Dashboard_001/웹_제어판.md) | 구현 완료 | 해당 없음 |
| SW_Unity_LineHealthMonitor_001 | SW / Unity / LineHealthMonitor | 라인 정체 감시 | 소프트웨어 / 라인감시 | 정체·무움직임 긴급 알림 | SW_Unity_Dashboard_001, SW_Unity_CentralBrain_001 | `FactoryLineHealthMonitor.cs` | [라인감시](소프트웨어/라인감시/SW_Unity_LineHealthMonitor_001/라인_정체_감시.md) | 구현 완료 | 해당 없음 |
| SW_Unity_RegionRouting_001 | SW / Unity / RegionRouting | 3구역 라우팅 | 소프트웨어 / 구역라우팅 | 메인→A/B/C 지선·스테이션·로봇3 | SW_Unity_ConveyorTransport_001, SW_Unity_RobotPickPlace_001 | `FactoryThreeRegionLayoutBuilder.cs`, `MainLineRegionRouter.cs`, `FactoryRegionSetup.cs` | [구역라우팅](소프트웨어/구역라우팅/SW_Unity_RegionRouting_001/구역_라우팅.md) | 구현 중 | 해당 없음 |
| SW_Unity_PackageOrder_001 | SW / Unity / PackageOrder | 택배·우선순위 | 소프트웨어 / 주문우선순위 | QR 메타·대기열 정렬 | SW_Unity_ItemSpawning_001, SW_Unity_RegionRouting_001 | `FactoryItem.cs`, `ConveyorPickDispatcher.cs` | [주문우선순위](소프트웨어/주문우선순위/SW_Unity_PackageOrder_001/주문_우선순위.md) | 구현 중 | 해당 없음 |
| SW_Unity_RegionCongestion_001 | SW / Unity / RegionCongestion | 구역 혼잡 | 소프트웨어 / 구역혼잡 | 대기열·임계·스폰 완화 | SW_Unity_FactoryState_001 | `FactoryLogisticsCenter.cs`, `LogisticsServices.cs` | [구역혼잡](소프트웨어/구역혼잡/SW_Unity_RegionCongestion_001/구역_혼잡.md) | 구현 중 | 해당 없음 |
| SW_Unity_RecognitionError_001 | SW / Unity / RecognitionError | 인식 오류 | 소프트웨어 / 인식오류코드 | 오류 코드·재시도·기록 | SW_Unity_ShapeRecognition_001 | `RobotStation.cs`, `PackageLogisticsTypes.cs` | [인식오류](소프트웨어/인식오류코드/SW_Unity_RecognitionError_001/인식_오류_코드.md) | 구현 중 | 해당 없음 |
| SW_Unity_EquipmentGuidance_001 | SW / Unity / EquipmentGuidance | 장비 안내 | 소프트웨어 / 장비오류안내 | 규칙 기반 안내 문구 | SW_Unity_CentralBrain_001 | `LogisticsServices.cs` (`EquipmentFaultCatalog`) | [장비안내](소프트웨어/장비오류안내/SW_Unity_EquipmentGuidance_001/장비_오류_안내.md) | 구현 중 | 해당 없음 |
| SW_Unity_DelayPrediction_001 | SW / Unity / DelayPrediction | 지연 예측 | 소프트웨어 / 지연예측 | 마감·대기열 위험 단계 | SW_Unity_PackageOrder_001 | `DelayRiskEvaluator` in `LogisticsServices.cs` | [지연예측](소프트웨어/지연예측/SW_Unity_DelayPrediction_001/지연_위험_예측.md) | 구현 중 | 해당 없음 |
| SW_Unity_OpsReporting_001 | SW / Unity / OpsReporting | 운영 보고서 | 소프트웨어 / 운영보고서 | 세션 지표·`/api/ops/report` | SW_Unity_FactoryState_001, SW_Unity_Dashboard_001 | `FactoryLogisticsCenter.cs` | [운영보고서](소프트웨어/운영보고서/SW_Unity_OpsReporting_001/운영_보고서.md) | 구현 중 | 해당 없음 |
| SW_Unity_VirtualPlant_001 | SW / Unity / VirtualPlant | 가상 설비 | 소프트웨어 / 가상에너지온도 | 가상 전력·온도(표기) | SW_Unity_RegionCongestion_001 | `VirtualPlantSimulator` in `LogisticsServices.cs` | [가상설비](소프트웨어/가상에너지온도/SW_Unity_VirtualPlant_001/가상_에너지_온도.md) | 구현 중 | 해당 없음 |

## 기존 기능분류 번호 매핑(요약)

| 기능분류 장 | 대응 기능 ID |
| --- | --- |
| 1 Unity 환경 | 001, SW_Unity_RobotPickPlace_001 |
| 2 컨베이어·물품 | SW_Unity_ConveyorTransport_001, SW_Unity_ItemSpawning_001 |
| 3 로봇팔 | SW_Unity_ShapeRecognition_001, SW_Unity_RobotPickPlace_001 |
| 4 중앙뇌 | SW_Unity_CentralBrain_001 |
| 5 제어판 | SW_Unity_Dashboard_001 |

## 기술 환경

- [Info/기술_환경.md](기술_환경.md)
