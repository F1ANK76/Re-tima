## 렌더링

### 그림자 캐스케이드 4 → 1으로 변경

**문제** — 화면 오브젝트가 295개인데 그리는 횟수가 570번. 절반이 그림자 패스

**원인** — URP 템플릿의 PC 품질 프리셋이 캐스케이드 값이 4

### Cast Shadows: TwoSided → On으로 변경

**문제** — 배경 오브젝트 110개(나무 45 · 바위 55 · 울타리 10)의 `MeshRenderer` → `Cast Shadows`가 전부 기본값인 `TwoSided`

**원인** — `TwoSided`는 그림자 패스에서 뒷면 컬링을 끄는 설정이라 삼각형을 두 배로 래스터화

## 코드 구조

### 드롭 아이템 설정을 SO로 분리

**문제** — `DropPickup`에 직렬화 필드가 15개 있었고, 같은 값이 프리팹 2개(`StatPotionPickup`, `EquipmentDropPickup`)에 중복 저장

**조치** — 프로젝트에 이미 있는 `StatDropTableSO` 패턴에 맞춰 `DropPickupConfigSO`로 분리. 종류별 애셋(프리팹·재질·회전·크기)을 `Entry`로 묶고 전역 튜닝값 15개를 함께 담음

### 스크롤러 공통 로직 추출

**문제** — `BackdropScroller`와 `GroundScroller`가 속도 이징·정지 스냅·페이스 노이즈를 약 20줄씩 중복

**조치** — `DropSource`와 같은 방식으로 `ScrollSource` 추상 베이스에 공통부를 두고 `Scroll(float step)`만 하위 클래스가 각자 구현

## 버그

### 지면 텍스처 오프셋 무한 누적

**문제** — `GroundScroller`가 `offset.x`를 계속 빼서 float 변수 몫의 값이 무한으로 커짐

**조치** — `offset.x %= 1f`로 몫을 없앰으로써 지속적으로 소수점 계산이 가능하도록 수정
