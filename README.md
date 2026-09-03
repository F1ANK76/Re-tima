# Re:tima

Action Idle RPG · Unity 6 (URP) · C#

메이플스토리 울티마 스쿼드를 참고하여 만든 방치형 성장과 직접 조작 전투(패링)를 결합한 방치형 RPG입니다.

이 저장소는 게임플레이 프로그래밍 스코프를 확인할 수 있도록 `Assets/Scripts` 소스코드만 포함합니다.

## 폴더 구조

- `Combat/` — 전투 루프, 패링, 궁극기, 몬스터
- `Progression/` — 드랍 타입(스탯/장비)과 그 조율
- `Stage/` — 스테이지 진행, 몬스터 스폰, 드랍 픽업
- `Data/` — 등급 롤, 등급별 색·크기 램프, 스테이지/몬스터 정의(ScriptableObject)
- `UI/` — HUD, 관리창, 배너/게이지 연출
- `Core/` — 이벤트 버스(`GameEvents`), 이징 곡선(`Easing`)
- `Debug/` — 개발용 디버그 도구

## 동작 과정

1. 플레이 버튼 클릭 시 TitleScreenView.HandlePlayPressed()가 실행됨
- 이후 내부의 onPlay?.Invoke()가 onPlay에 보관된 StageManager.BeginRun을 실행
2. 이후 StageManager.ShowBannerThenSpawn() 호출 후 banner.Show를 호출해서 스테이지 배너를 잠깐 보여줬다가 배너가 닫힌 후 몬스터를 SpawnForCurrentSubStage()로 소환 후 자동 이동
- SO 데이터 반환 → 스탯 계산 → 화면 밖 프리팹 생성 → 스탯 주입 → 이동 → 이벤트 발생
3. 플레이어는 씬 로드 시점부터 CombatLoop.Update() 내부 shouldBeMoving 값을 매 프레임 세팅하고 값이 true 라면 제자리 달리기 애니메이션을 계속 재생
- 배경과 바닥이 스크롤돼서 전진하는 것처럼 보이게 구현
4. 그 상태로 스폰된 몬스터가 근접 사거리에 도달했는지 체크 후 도달 시 DoTick() 내부 DealDamageAfterSwing 코루틴으로 플레이어가 자동 공격 시작
- 동시에 일반 몬스터의 공격 루프도 NormalAttackLoop 코루틴으로 시작

## 설계
