using UnityEngine;

[CreateAssetMenu(fileName = "StageConfig", menuName = "Retima/Stage Config")]
public class StageConfigSO : ScriptableObject
{
    [Header("Combat Timing")]
    public float tickInterval = 0.5f;
    public float normalMonsterAttackInterval = 0.4f;

    [Header("Monster Scaling")]
    public float hpGrowthPerStage = 0.15f;
    public float atkGrowthPerStage = 0.10f;

    [Header("Stat Drop Amounts - ATK")]
    public float atkAmountNormal = 0.1f;
    public float atkAmountRare = 0.3f;
    public float atkAmountEpic = 0.5f;
    public float atkAmountUnique = 1f;
    public float atkAmountLegendary = 2f;

    [Header("Stat Drop Amounts - HP")]
    public float hpAmountNormal = 5f;
    public float hpAmountRare = 10f;
    public float hpAmountEpic = 30f;
    public float hpAmountUnique = 50f;
    public float hpAmountLegendary = 100f;


    [Header("Monster Movement")]
    // 월드의 이동 속도이며 의도적으로 공유된다: 스탯 포션/장비 픽업의 유인 속도, GroundScroller/
    // BackdropScroller의 스크롤 속도가 모두 여기서 파생되어 플레이어가 걷는 것처럼 보이는 바로 그
    // 속도로 지면이 흐른다. 바꾸면 이 모든 게 함께 재조정되는 것이 의도이므로, 뒤집어 말하면
    // "몬스터가 더 빨리 도착하게" 하려고 만지는 값은 아니다.
    public float monsterMoveSpeed = 2f;
    // 몬스터가 걸어 들어오는 속도에만 적용되는, 위 속도에 대한 배율. 몬스터 접근은 죽은 시간
    // (플레이어는 전투 시작을 기다릴 뿐)이지만 픽업 유인과 지면 스크롤은 "앞으로 걷고 있다"는 착각을
    // 만드므로 분리했다 - 몬스터를 빠르게 한다고 포션이 두 배로 딸려오거나 지면이 걷는 속도와
    // 어긋나서는 안 된다.
    public float monsterApproachMultiplier = 2f;
    public float meleeRange = 1.3f;

    // MonsterSpawner가 실제로 몬스터에게 전달하는 값. 세 곳의 개별 스폰 호출 지점에서 두 필드가
    // 서로 어긋나는 일이 없도록 프로퍼티로 유지한다.
    public float MonsterApproachSpeed => monsterMoveSpeed * monsterApproachMultiplier;

}
