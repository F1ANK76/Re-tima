
// 플레이어 캐릭터(SwordAndShieldStance.controller)의 상태 이름.
public static class PlayerAnimStates
{
    public const string Idle = "Idle_Battle_SwordAndShield";
    public const string Running = "MoveFWD_Normal_InPlace_SwordAndShield";
    public const string Victory = "Victory_Battle_SwordAndShield";
    public const string Die = "Die01_SwordAndShield";
    public const string Defend = "Defend_SwordAndShield";
    public const string Riposte = "Attack04_SwordAndShiled";
    public const string Ultimate = "JumpFull_Spin_RM_SwordAndShield";
}

public static class AnimParams
{
    public const string IsMoving = "IsMoving";
    public const string Attack = "Attack";
    public const string Victory = "Victory";
    public const string Die = "Die";
}
