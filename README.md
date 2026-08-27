# Re:tima

Action Idle RPG · Unity 6 (URP) · C#

메이플스토리 울티마 스쿼드를 참고하여 만든 방치형 성장과 직접 조작 전투(패링)를 결합한 액션 RPG입니다.

이 저장소는 게임플레이 프로그래밍 스코프를 확인할 수 있도록 `Assets/Scripts` 소스코드만 포함합니다.

## 폴더 구조

- `Combat/` — 전투 루프, 패링, 궁극기
- `Progression/` — 스탯/장비/스톤 드랍 매니저
- `Stage/` — 스테이지 진행, 몬스터 스폰, 픽업
- `Data/` — 등급/확률/드랍 테이블 (ScriptableObject 기반)
- `UI/` — HUD, 관리창, 배너/게이지 연출
- `Core/` — 이벤트 버스 (`GameEvents`)
- `Debug/` — 개발용 디버그 도구

## 설계

### 이벤트 버스로 시스템 간 결합 최소화

`Core/GameEvents.cs`는 9개의 `static event`를 제공하는 이벤트 버스입니다.
몬스터 사망 하나에 스테이지 진행·드랍 판정·UI 갱신 등 여러 시스템이 반응해야 하는데,
발행하는 쪽(`Monster`)이 구독자를 전부 직접 참조하면 시스템이 늘어날 때마다
발행 코드를 고쳐야 합니다. 발행은 `GameEvents.RaiseMonsterDied(this)` 한 줄로 끝내고,
반응이 필요한 쪽이 각자 구독하는 구조로 분리했습니다.

`event` 키워드로 외부에서의 `Invoke`와 대입을 막고 발행 창구를 `Raise...` 메서드로 한정해,
다른 시스템이 임의로 이벤트를 발행하거나 남의 구독을 초기화하는 것을 차단했습니다.
`static` 이벤트는 오브젝트 수명과 무관하게 유지되므로, 구독하는 22개 파일 전부
`OnEnable`/`OnDisable` 짝으로 해제를 보장했습니다.

### 카운터 기반 상태 관리

전투 중단은 `bool` 플래그가 아니라 참조 카운터로 관리합니다
(`CombatLoop.PushSuspend()` / `PopSuspend()`, 이동 정지는 `PushIdleHold()` / `PopIdleHold()`).
패링·궁극기·드랍 픽업 등 여러 연출이 동시에 겹칠 수 있어, `bool`로 관리하면
먼저 끝난 연출이 아직 진행 중인 다른 연출의 정지 상태까지 해제해버립니다.
사망·디버그 점프처럼 중단 이력을 전부 무효화해야 하는 경로에는 `ClearSuspend()`를 둬서
카운터가 0으로 돌아오지 못하고 고착되는 상황을 막았습니다.

### 밸런스 수치의 코드 분리

틱 간격, 몬스터 성장률, 드랍 확률 등은 `Data/StageConfigSO`·`MonsterDefinitionSO`
(ScriptableObject)로 분리해 재컴파일 없이 조정할 수 있게 했습니다.
서브스테이지별 HP 곡선은 `MonsterSpawner.GetNormalHp()` 하나만 참조하도록 하고,
엘리트·보스 수치는 그 반환값에 배수를 적용해 계산합니다.

### 연출 타이밍을 상수 대신 실제 지속 시간으로 동기화

보스 게이지 연출 → 등장 배너 → 엘리트 스폰 순서를 매직 넘버로 맞추면
어느 한쪽 연출 길이를 바꿀 때 타이밍이 어긋납니다.
`BossGaugeView.ReadyFlourishDuration`, `ClearBannerView.TotalPlayDuration`처럼
각 연출이 자기 총 재생 시간을 노출하고, `StageManager`가 그 값을 읽어 대기하도록 했습니다.
