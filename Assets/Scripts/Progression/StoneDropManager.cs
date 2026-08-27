using System.Collections;
using UnityEngine;

// stage 3의 드롭 롤. 메인 스테이지가 순서대로 그라인드를 넘긴다: stage 1은 스탯 포션
// (StatDropManager), stage 2는 장비(EquipmentDropManager), stage 3는 크리스탈. 항상 셋 중
// 하나만 활성이라, 후반 스테이지에서 세 드롭 타입에 한꺼번에 파묻히지 않는다.
//
// 스톤은 아직 누적만 된다 - 소비하는 쪽이 생기면 여기서 카운트를 읽어가면 되고, 드롭·픽업
// 모션·집계는 그것 없이도 의도적으로 완결되고 독립적으로 동작한다.
public class StoneDropManager : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private StoneDropPickup stonePrefab;
    [SerializeField] private CombatLoop combatLoop;

    // 고정값. 다른 두 매니저의 곡선과 달리 스테이지 스케일링을 의도적으로 안 한다 - stage 3가
    // 마지막으로 제작된 스테이지라서 확률이 올라갈 "이후 스테이지" 자체가 없다.
    [Range(0f, 1f)][SerializeField] private float stoneDropChance = 0.5f;
    // 두 크리스탈 사이의 균등 분배다. Red/green은 stage 1이 RedVial/GreenVial 포션으로
    // 이미 정해놓은 색상 언어를 그대로 따른다 - red는 ATK 쪽 스톤, green은 HP 쪽 스톤이다.
    [Range(0f, 1f)][SerializeField] private float attackStoneChance = 0.5f;

    public const int UnlockStage = 3;

    private int currentMainStage = 1;

    // 크리스탈 색상별 누적 카운트. 스톤이 아직 장비와 무관하므로 장비 매니저가 아니라 여기,
    // 스톤 시스템 자체의 상태로 둔다 - 모은 스톤에 일어나는 일은 현재 이게 전부다. 0이 아니라
    // 미리 채워 시작해서 Stone 탭이 stage 3 드롭률을 기다리지 않고 바로 리롤할 수 있게 한다.
    private int attackStones = 100;
    private int hpStones = 100;

    public int AttackStones => attackStones;
    public int HpStones => hpStones;
    public int GetStones(StatType statType) => statType == StatType.Attack ? attackStones : hpStones;

    private void OnEnable()
    {
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void HandleStageChanged(int mainStage, int subStage)
    {
        currentMainStage = mainStage;
    }

    private void HandleMonsterDied(Monster monster)
    {
        // Elite/Boss는 그라인드에 기여하는 게 아니라 서브스테이지를 마무리 짓는다 - 다른 두
        // 드롭 매니저도 따르는 동일한 규칙이라, 일반 몬스터 처치에서만 뭔가가 드롭된다.
        if (monster.Type != MonsterType.Normal) return;

        if (currentMainStage < UnlockStage) return;

        if (Random.value > stoneDropChance) return;

        // 드롭에서 롤하는 건 오직 두 스톤 중 어느 쪽이냐 뿐이다 - 스톤에는 등급이 없어서
        // 모든 red 스톤은 다른 red 스톤과 완전히 동일하다.
        StatType statType = Random.value < attackStoneChance ? StatType.Attack : StatType.Hp;

        // prefab/player가 없어도 스톤은 그대로 적립한다 - 비주얼 미연결로 드롭이 조용히
        // 유실되지 않게, 형제 매니저들의 prefab 누락 대체 처리와 동일하게 맞췄다.
        int count = RollStoneCount();
        if (stonePrefab == null || player == null)
        {
            for (int i = 0; i < count; i++) AddStone(statType);
            return;
        }

        StartCoroutine(SpawnStoneStream(monster.transform.position, statType, count));
    }

    // 60/30/10 - 처치 한 번으로 스톤이 여러 개 쏟아지는 경우는 가끔뿐이라, 2-3개 연속은
    // 평범한 결과가 아니라 눈에 띄게 좋은 처치처럼 느껴진다.
    private static int RollStoneCount()
    {
        float roll = Random.value;
        if (roll < 0.6f) return 1;
        if (roll < 0.9f) return 2;
        return 3;
    }

    // 한 번에 쏟아지는 스톤은 모두 이 처치에서 롤된 같은 statType이다. 한꺼번에 스폰하지 않고
    // 시간차를 두며 하나씩 더 뒤에 떨어뜨려, 겹쳐 쌓인 덩어리가 아니라 몬스터 뒤로 짧게
    // 흘러나오는 흔적처럼 보이게 한다.
    private IEnumerator SpawnStoneStream(Vector3 position, StatType statType, int count)
    {
        const float SpawnStagger = 0.12f;
        const float TrailSpacing = 0.55f;

        for (int i = 0; i < count; i++)
        {
            // 스트림 도중 죽으면(끝나기 전에 StageManager의 리스폰이 플레이어를 리셋한다)
            // 아직 도달하지도 않은 체크포인트에 스톤을 계속 뿌려선 안 된다.
            if (player == null || player.Stats.IsDead) yield break;

            StoneDropPickup stone = Instantiate(stonePrefab, position, Quaternion.identity);
            // 몬스터가 걸어 들어오는 속도와 동일하게 맞춰서, 형제 픽업 두 종류 모두와 일치시켜
            // 모든 드롭 타입이 같은 전진 이동으로 보이게 한다.
            stone.Initialize(statType, player.transform, combatLoop,
                stageConfig != null ? stageConfig.monsterMoveSpeed : 0f, this, i * TrailSpacing);

            if (i < count - 1) yield return new WaitForSeconds(SpawnStagger);
        }
    }

    // 해당 타입의 스톤을 하나 소비한다. 소비할 게 없으면 false를 반환해서 호출자가 공짜로
    // 롤할 수 없게 한다.
    public bool TryConsumeStone(StatType statType)
    {
        if (statType == StatType.Attack)
        {
            if (attackStones <= 0) return false;
            attackStones--;
        }
        else
        {
            if (hpStones <= 0) return false;
            hpStones--;
        }

        GameEvents.RaiseStonesChanged(statType, GetStones(statType), -1);
        return true;
    }

    // 플레이어가 실제로 크리스탈을 밟았을 때 StoneDropPickup이 호출한다.
    public void AddStone(StatType statType)
    {
        if (statType == StatType.Attack) attackStones++;
        else hpStones++;

        GameEvents.RaiseStonesChanged(statType, GetStones(statType), 1);
    }
}
