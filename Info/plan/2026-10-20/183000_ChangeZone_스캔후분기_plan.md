# ChangeZone 스캔 후 분기 정합

- 문서 초기 생성 일시: 2026-10-20 18:30 (KST)
- 지시자: 이종인
- 작업자: Composer
- 기능 ID: SW_Unity_RegionRouting_001
- Cursor plan: ChangeZone 기준 Factory 물류 흐름 정합 (`.cursor/plans` — 실행 참조만, 수정 없음)

## 목표

ChangeZoneConveyorBelt 단일 스폰·스캔 → A/C는 ToA/ToC 지선, B는 +X 직진.

## 작업

- `MainLineRegionRouter`: 스캔 완료 게이트, B 무텔레포트
- `FactoryItem.HasScannedSuccessfully`
- `RobotStation`: regionSetups·스캔 후 라우팅·구역별 PickDispatcher
- `Factory.unity`: 벨트 이름·RobotStation_ChangeZone·RegionSetup 배선

## day

- [2026-10-20_183000_001_ChangeZone_스캔후분기.md](../../메인프로젝트/전체구조/001/day/2026-10-20_183000_001_ChangeZone_스캔후분기.md)
