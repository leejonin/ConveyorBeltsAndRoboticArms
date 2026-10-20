# Git 원격 저장소·공동 작업

- 문서 초기 생성 일시: 2026-10-20 18:40 (KST)
- 지시자: 이종인
- 작업자(문서): Composer

## 원격

- URL: https://github.com/leejonin/ConveyorBeltsAndRoboticArms
- 기본 브랜치: `main`

## 공동 작업자

| GitHub 계정 | 역할 | 비고 |
| --- | --- | --- |
| leejonin | Owner | 저장소 생성·push 권한 |
| sysop9000 | Collaborator | push 권한 — GitHub Settings → Collaborators에서 초대 |

초대는 Owner가 GitHub 웹 또는 `gh api repos/leejonin/ConveyorBeltsAndRoboticArms/collaborators/sysop9000 -X PUT -f permission=push` 로 수행합니다.

## Git에 포함

- `Assets/`, `Packages/`, `ProjectSettings/` (`.meta` 포함)
- `Info/` (plan, day, problem, 기능 문서)
- `.github/`, 루트 `README.md`, `.gitignore`

## Git에 제외 (.gitignore)

- Unity 생성: `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `.vs/`, `*.csproj`, `*.sln`
- 로컬 벤더: `UniversalRobots.Unity-main/`, `_RobotArmProjectFiles/`, `_zxing*`, `_nuget_zxing/`
- `.cursor/`, `debug-*.log` 등 — 계획은 **`Info/plan/`** 만 공유

## 운영

- 커밋 메시지: 한국어, 변경 이유 중심
- ForAgents 4.8: 하루 `day` 5건 이상이면 미반영 변경을 작업 시각 오름차순으로 커밋 후 push
- 기능 브랜치: `feature/기능ID-요약`, `fix/...` (선택)

## plan

- [Info/plan/2026-10-20/184000_GitHub_공동작업_저장소_plan.md](../../../plan/2026-10-20/184000_GitHub_공동작업_저장소_plan.md)

## 확인

- 다른 PC clone 후 Unity Play: **미확인**
