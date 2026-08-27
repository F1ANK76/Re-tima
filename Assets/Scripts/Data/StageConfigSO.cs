using UnityEngine;

[CreateAssetMenu(fileName = "StageConfig", menuName = "UltimaSquad/Stage Config")]
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
    // 처치 시 검/방패가 드롭될 확률 - 위의 스탯 드롭과는 별개의 독립적인 판정이다.
    // 메인 스테이지에 따라 상승한다: 스테이지 1 = 30%, 스테이지 2 = 35%, ... 등급별 스탯 보너스
    // 자체는 고정 테이블(EquipmentGradeBonus 참고)이며 스테이지에 따라 스케일링되지 않는다.
    [Range(0f, 1f)] public float equipDropBaseChance = 0.3f;
    [Range(0f, 1f)] public float equipDropChancePerStage = 0.05f;

    [Header("Monster Movement")]
    // 월드의 이동 속도이며, 의도적으로 공유된다: 스탯 포션과 장비 픽업도 이 속도로 유인되며,
    // GroundScroller/BackdropScroller의 스크롤 속도도 여기서 파생되어 플레이어가 걷는 것처럼
    // 보이는 바로 그 속도로 지면이 흘러가게 된다. 이 값을 바꾸면 이 모든 것이 함께 재조정되는데,
    // 그것이 바로 의도이지만, 다시 말해 "몬스터가 더 빨리 도착하게 하고 싶다"를 위한 조절 값은
    // 아니라는 뜻이다.
    public float monsterMoveSpeed = 2f;
    // 몬스터가 걸어 들어오는 속도에만 적용되며, 위 속도에 대한 배율이다. 몬스터의 접근은 죽은
    // 시간(플레이어는 그저 전투 시작을 기다릴 뿐)인 반면, 픽업이 빨려들어오는 것과 지면 스크롤은
    // "앞으로 걷고 있다"는 착각을 만드는 요소이므로 분리했다 - 몬스터를 빠르게 한다고 포션이
    // 두 배 빨리 딸려오거나 지면이 걷는 속도와 어긋나서는 안 된다.
    public float monsterApproachMultiplier = 2f;
    public float meleeRange = 1.3f;

    // MonsterSpawner가 실제로 몬스터에게 전달하는 값. 세 곳의 개별 스폰 호출 지점에서 두 필드가
    // 서로 어긋나는 일이 없도록 프로퍼티로 유지한다.
    public float MonsterApproachSpeed => monsterMoveSpeed * monsterApproachMultiplier;

}
