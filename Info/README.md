# Info — SmartFactory 개발 문서

- 문서 수정 일시: 2026-10-20 (KST)
- 지침: ForAgents v0.0.11 (팀원 PC `Desktop/ForAgents/` — 저장소 미포함)
- Git·협업: [Git_원격_저장소.md](메인프로젝트/전체구조/001/Git_원격_저장소.md) — 공동 작업자 **sysop9000**

## 빠른 링크

| 문서 | 경로 |
| --- | --- |
| Git·공동 작업 | [Git_원격_저장소.md](메인프로젝트/전체구조/001/Git_원격_저장소.md) |
| 기능 ID 목록 | [기능_ID_목록.md](기능_ID_목록.md) |
| 기술 환경 | [기술_환경.md](기술_환경.md) |
| 프로젝트 개요·요구·현황 | [메인프로젝트/전체구조/001/](메인프로젝트/전체구조/001/) |
| 작업 계획 | [plan/](plan/) |
| 에셋 다운로드 지침 | [001/에셋_다운로드_지침.md](메인프로젝트/전체구조/001/에셋_다운로드_지침.md) |
| AI 물류센터 구현 계획 | [plan/2026-10-20/173000_AI물류센터_핵심기능_구현_plan.md](plan/2026-10-20/173000_AI물류센터_핵심기능_구현_plan.md) |
| AI 물류센터 상세 설계(Desktop) | Desktop `AI_물류센터_핵심기능_상세설계서.md` (저장소 외부) |
| 3구역 라우팅 | [SW_Unity_RegionRouting_001](소프트웨어/구역라우팅/SW_Unity_RegionRouting_001/구역_라우팅.md) |

## 대시보드

- Play Mode 중: **http://127.0.0.1:8080/** (포트: `FactoryConfig.dashboardPort`, 기본 8080)
- Unity Play 종료 시 서버 중지

## 소프트웨어 기능 문서

| 기능 ID | 문서 |
| --- | --- |
| SW_Unity_ConveyorTransport_001 | [벨트구동](소프트웨어/벨트구동/SW_Unity_ConveyorTransport_001/컨베이어_벨트_운반.md) |
| SW_Unity_ItemSpawning_001 | [물품생성](소프트웨어/물품생성/SW_Unity_ItemSpawning_001/물품_생성.md) |
| SW_Unity_ShapeRecognition_001 | [물품인식](소프트웨어/물품인식/SW_Unity_ShapeRecognition_001/물품_인식.md) |
| SW_Unity_RobotPickPlace_001 | [로봇집기분류](소프트웨어/로봇집기분류/SW_Unity_RobotPickPlace_001/로봇_집기_분류.md) |
| SW_Unity_FactoryState_001 | [공장상태](소프트웨어/공장상태/SW_Unity_FactoryState_001/공장_상태_집계.md) |
| SW_Unity_CentralBrain_001 | [중앙뇌](소프트웨어/중앙뇌/SW_Unity_CentralBrain_001/중앙뇌.md) |
| SW_Unity_Dashboard_001 | [대시보드](소프트웨어/대시보드/SW_Unity_Dashboard_001/웹_제어판.md) |
| SW_Unity_LineHealthMonitor_001 | [라인감시](소프트웨어/라인감시/SW_Unity_LineHealthMonitor_001/라인_정체_감시.md) |

## 외부 설계(Desktop)

- AI 물류센터 핵심기능 상세설계서 — Desktop 로컬(Info 외부)

## README.ko

- 영문 README는 저장소 루트에 없을 수 있음. 한국어 프로젝트 소개: [README.ko.md](README.ko.md) (점진적으로 본 README와 통합)
