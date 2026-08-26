using UnityEngine;

public class PlayerStats
{
    public float MaxHp { get; private set; }
    public float CurrentHp { get; private set; }
    public float AttackPower { get; private set; }

    public bool IsDead => CurrentHp <= 0f;

    public PlayerStats(float maxHp, float attackPower)
    {
        MaxHp = maxHp;
        CurrentHp = maxHp;
        AttackPower = attackPower;
    }

    public void IncreaseAttack(float amount) => AttackPower += amount;

    public void IncreaseMaxHp(float amount)
    {
        MaxHp += amount;
        CurrentHp += amount;
    }

    public void ApplyDamage(float amount) => CurrentHp = Mathf.Max(0f, CurrentHp - amount);

    public void ResetToFull() => CurrentHp = MaxHp;
}
