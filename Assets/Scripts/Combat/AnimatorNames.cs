// Animator에 문자열로 접근하는 이름들을 한곳에 모아둔다. 컴파일러가 검증해주지 않는
// 값이므로, 같은 리터럴이 여러 파일에 흩어져 있으면 한쪽만 고쳤을 때 조용히 어긋난다 -
// 실제로 러닝/Idle 상태 이름과 IsMoving/Die 파라미터가 파일 두세 곳에 각각 박혀 있었다.
//
// 여기 있는 값은 임포트한 캐릭터 애셋 팩의 클립/상태 이름 그대로이며(오타 포함), 코드가
// 아니라 Animator Controller 쪽이 원본이다. 이름을 바꾸려면 Animator에서 먼저 바꿔야 한다.

// 플레이어 캐릭터(SwordAndShieldStance.controller)의 상태 이름.
public static class PlayerAnimStates
{
    public const string Idle = "Idle_Battle_SwordAndShield";
    public const string Running = "MoveFWD_Normal_InPlace_SwordAndShield";
    public const string Victory = "Victory_Battle_SwordAndShield";
    public const string Die = "Die01_SwordAndShield";
    public const string Defend = "Defend_SwordAndShield";
    // 패링 반격. 애셋 팩 원본의 오타("Shiled")를 그대로 둔다 - Animator 상태 이름과
    // 정확히 일치해야 하므로 여기서 고치면 매칭이 깨진다.
    public const string Riposte = "Attack04_SwordAndShiled";
    public const string Ultimate = "JumpFull_Spin_RM_SwordAndShield";
}

// 플레이어와 몬스터 컨트롤러가 이름 규약을 공유하는 파라미터. 몬스터 전용 파라미터
// (UseTelegraph, AttackPattern2 등)는 Monster에서만 쓰이므로 그쪽에 그대로 둔다.
public static class AnimParams
{
    public const string IsMoving = "IsMoving";
    public const string Attack = "Attack";
    public const string Victory = "Victory";
    public const string Die = "Die";
}
