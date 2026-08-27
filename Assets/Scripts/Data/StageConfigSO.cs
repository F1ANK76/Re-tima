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

    [Header("Stat Drop Progression")]
    // 처치 시 랜덤 스탯 상승 아이템이 드롭될 확률. 서브 스테이지가 아니라 메인 스테이지에 따라
    // 상승한다: 스테이지 1 = 50%, 스테이지 2 = 60%, 스테이지 3 = 70%, ...
    [Range(0f, 1f)] public float statDropBaseChance = 0.5f;
    [Range(0f, 1f)] public float statDropChancePerStage = 0.1f;

    [Header("Stat Drop Amounts - ATK")]
    public float atkAmountNormal = 0.1f;
    public float atkAmountRare = 0.3f;
    public float atkAmountEpic = 0.5f;
    public float atkAmountUnique = 1f;
    public float atkAmountLegendary = 2f;

    [Header("Stat Drop Amounts - HP")]
    public float hpAmountNormal = 1f;
    public float hpAmountRare = 3f;
    public float hpAmountEpic = 5f;
    public float hpAmountUnique = 10f;
    public float hpAmountLegendary = 20f;


    [Header("Equipment Drop Progression")]
    // 처치 시 검/방패 드롭 확률 - 위 스탯 드롭과는 별개의 독립 판정. 메인 스테이지에 따라 상승:
    // 1 = 30%, 2 = 35%, ... 등급별 스탯 보너스는 고정 테이블(EquipmentGradeBonus)이라 스테이지 무관.
    [Range(0f, 1f)] public float equipDropBaseChance = 0.3f;
    [Range(0f, 1f)] public float equipDropChancePerStage = 0.05f;

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
