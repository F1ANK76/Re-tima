# Re:tima

Action Idle RPG · Unity 6 (URP) · C#

방치형 성장과 직접 조작 전투(패링)를 결합한 액션 RPG입니다.

이 저장소는 게임플레이 프로그래밍 스코프를 확인할 수 있도록 `Assets/Scripts` 소스코드만 포함합니다.

## 폴더 구조

- `Combat/` — 전투 루프, 패링, 궁극기
- `Progression/` — 스탯/장비/스톤 드랍 매니저
- `Stage/` — 스테이지 진행, 몬스터 스폰, 픽업
- `Data/` — 등급/확률/드랍 테이블 (ScriptableObject 기반)
- `UI/` — HUD, 관리창, 배너/게이지 연출
- `Core/` — 이벤트 버스 (`GameEvents`)
- `Debug/` — 개발용 디버그 도구
