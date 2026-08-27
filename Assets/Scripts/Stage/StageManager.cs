using System.Collections;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private float nextMonsterDelay = 2f;

    [SerializeField] private MonsterSpawner spawner;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private StageBannerView banner;
    [SerializeField] private ClearBannerView clearBanner;
    [SerializeField] private FailBannerView failBanner;
    // 없으면 런은 씬이 시작되는 순간 바로 시작된다 - 플레이 모드 테스트나
    // 메뉴가 없는 씬에서 기대하는 동작이 바로 이것이다.
    [SerializeField] private TitleScreenView titleScreen;
    [SerializeField] private CombatLoop combatLoop;
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private BossGaugeView bossGauge;

    // 각 메인 스테이지는 일반 서브스테이지 네 개 다음에 보스로 구성된다: 1-1..1-4,
    // 그리고 1-5가 보스 조우다. 원래 10에서 줄인 값이다 - 10이었을 때는 메인
    // 스테이지 하나를 클리어하는 데 약 100번의 처치(일반 서브스테이지 9개 x
    // 보스 게이지를 채우는 10번의 처치, 여기에 엘리트와 보스까지)가 필요했는데,
    // 이는 꾸준한 진행이 아니라 긴 노가다로 느껴졌다. MonsterSpawner의 HP 곡선은
    // 여전히 메인 스테이지마다 10개짜리 구간을 예약해둔다((mainStage - 1) * 10 +
    // subStage) - 이건 이 변경과 무관하게 그대로이며, 단지 각 구간의 subStage
    // 5-9를 재배치할 필요 없이 그냥 쓰지 않을 뿐이다.
    public const int BossSubStage = 5;
    private const int BossGaugePerKill = 10;
    private const int BossGaugeMax = 100;

    // 지금은 콘텐츠가 여기서 끝난다. 마지막 스테이지의 보스를 클리어하면 몬스터
    // 구성이 없는 스테이지로 진행하는 대신 바로 이전 서브스테이지로 되돌아가므로,
    // 런이 막다른 길에 다다르지 않고 계속 진행된다(3-9 -> 3-10 보스 -> 3-9 ->
    // ...).
    public const int MaxMainStage = 3;

    // 이 스테이지의 보스를 물리치는 것이 플레이어의 궁극기를 해금하는 조건이므로
    // (UltimateManager 참고), 이 클리어에는 평소보다 추가 연출이 붙는다.
    public const int SkillUnlockStage = 1;

    public int MainStage { get; private set; } = 1;
    public int SubStage { get; private set; } = 1;

    private int bossGaugePercent;
    private Monster currentMonster;

    // 새로운 스테이지 런이 시작될 때마다 증가한다. 스폰들은 자신을 예약한 런보다
    // 더 오래 살아남는 대기(스테이지 배너, 몬스터 사이의 딜레이) 뒤에 큐잉되므로,
    // 각 스폰은 이 값을 캡처해두었다가 값이 더 이상 일치하지 않으면 실행을
    // 취소한다 - 그렇지 않으면 대기 도중 사망했을 때 이전 런의 몬스터가 새
    // 스테이지 위에 그대로 떨어지게 된다.
    private int stageGeneration;

    // 플레이어가 실제로 클리어한 마지막 서브스테이지 - 사망하면 죽은 위치가
    // 아니라 이 지점으로 되돌아가므로, 사망으로 잃는 것은 진행 중이던 시도
    // 뿐이다.
    private int checkpointMainStage = 1;
    private int checkpointSubStage = 1;

    private void OnEnable()
    {
        GameEvents.OnMonsterSpawned += HandleMonsterSpawned;
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterSpawned -= HandleMonsterSpawned;
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    private void Start()
    {
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        // 메뉴는 Play를 누를 때까지 런을 붙잡아둔다; 그 후 첫 스테이지 배너가
        // 메뉴 아래에서 나타나고 메뉴는 그 안으로 서서히 사라진다.
        if (titleScreen != null) titleScreen.Show(ShowBannerThenSpawn);
        else ShowBannerThenSpawn();
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        currentMonster = monster;
    }

    // 현재 씬에 있는 모든 몬스터는 사망 애니메이션이 실제로 끝날 때까지 공격은
    // 멈추지만 제자리에 그대로 남는다(사망 보상도, 진행도 없음) - 그 후에야
    // 체크포인트 리스폰을 위해 정리된다. 이 기간 동안 플레이어는 무적 상태가
    // 된다: HP가 2일 때 시퀀스 도중 스치는 공격 한 번만 맞아도 다시 죽어서
    // 아래의 정리 로직에 도달하기도 전에 이 코루틴 전체가 재시작되어 버리기
    // 때문이다.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();
        stageGeneration++;

        // 배너는 자기 자신의 GameObject에서 자기만의 코루틴을 실행하므로, 위의
        // StopAllCoroutines가 거기까지 닿지 않는다. 그냥 두면 진행 중인 배너 뒤에
        // 큐잉된 스폰이 아래의 초기화 이후에 실행되어 새 스테이지에 몬스터를
        // 떨어뜨리게 된다.
        if (banner != null) banner.Cancel();

        // 이게 없으면 클리어를 유발한 처치와 같은 순간에 발생한 사망이 사망
        // 시퀀스 위에 "CLEAR !"를 계속 띄운 채로 남겨두게 된다.
        if (clearBanner != null) clearBanner.Cancel();

        if (player != null) player.IsInvulnerable = true;

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            monster.StopAttacking();
        }

        StartCoroutine(PlayDeathThenRestart());
    }

    private IEnumerator PlayDeathThenRestart()
    {
        if (combatLoop != null) yield return combatLoop.PlayDeathSequence();

        // 화면을 어둡게 하고 아래의 체크포인트 되감기 전에 잠깐 "FAIL !"을
        // 띄운다 - clearBanner처럼 발사 후 무시하지 않고 yield로 기다리므로,
        // 페이드가 아직 떠 있는 동안에는 재시작이 절대 일어나지 않는다.
        if (failBanner != null) yield return failBanner.Play();

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            Destroy(monster.gameObject);
        }
        currentMonster = null;

        // 플레이어를 죽인 처치에서 나온 드롭(또는 그 이전 처치에서 이미 진행
        // 중이던 드롭)은 미끄러져 들어오던 걸 끝까지 마치지 않고 제자리에서
        // 멈춘다 - StatPotionPickup/EquipmentDropPickup.HandlePlayerDied 참고.
        // 리스폰된 필드에 잡동사니로 남지 않도록 여기서 몬스터와 함께 정리한다.
        foreach (StatPotionPickup potion in FindObjectsByType<StatPotionPickup>(FindObjectsSortMode.None))
        {
            Destroy(potion.gameObject);
        }
        foreach (EquipmentDropPickup pickup in FindObjectsByType<EquipmentDropPickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }
        foreach (StoneDropPickup stone in FindObjectsByType<StoneDropPickup>(FindObjectsSortMode.None))
        {
            Destroy(stone.gameObject);
        }

        // 위에서 픽업을 곧바로 Destroy()하면, 그게 아직 던져지는 도중이었을 경우
        // (idle hold는 걸렸지만 아직 풀리지 않은 상태) 자신의 PopIdleHold 호출을
        // 건너뛰게 된다 - 그냥 두면 그걸 풀어줄 살아있는 존재가 아무것도 남지
        // 않아 플레이어가 영원히 idle 상태에 갇힌다. 이 시점에는 idle hold를
        // 걸어놨을 가능성이 있는 모든 픽업이 이미 사라진 상태이므로, 여기서
        // 정리해도 항상 안전하다.
        if (combatLoop != null) combatLoop.ClearIdleHold();

        MainStage = checkpointMainStage;
        SubStage = checkpointSubStage;
        bossGaugePercent = 0;
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        // 이것이 바로 부활의 순간이다: 사망 애니메이션이 끝났고 필드도 정리됐으니,
        // 다시 공격당할 수 있게 되기 전에 플레이어를 풀피로 되돌려놓는다. 무적
        // 상태는 배너 이후가 아니라 여기서 해제한다 - 배너가 재생되는 동안까지
        // 무적을 켜둔 채로 두면, 그 사이 또 다른 사망이 먼저 끼어들었을 때
        // 플래그가 계속 켜진 채로 남을 위험이 있다.
        if (player != null)
        {
            player.RestoreToFullHp();
            player.IsInvulnerable = false;
        }

        // 사망 시퀀스 도중에 두 번째 사망이 발생하면 HandlePlayerDied가 다시
        // 호출되는데, 이때 위쪽의 StopAllCoroutines()가 CombatLoop.PlayDeathSequence를
        // 자신의 suspend 플래그를 복원하기도 전에 끊어버릴 수 있다 - 그러면
        // 플레이어가 리스폰한 후에도 전투가 영구히 멈춘 채로 남게 된다. 플레이어가
        // 다시 일어난 이후에는 전투가 항상 살아있어야 하므로, 중단된 코루틴을
        // 믿는 대신 여기서 강제로 켜준다.
        if (combatLoop != null) combatLoop.ClearSuspend();

        ShowBannerThenSpawn();
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (currentMonster == monster) currentMonster = null;

        // 보스는 항상 마지막 서브스테이지의 조우로만 등장하므로, 이를 물리치면
        // 메인 스테이지 전체가 클리어된다.
        if (monster.Type == MonsterType.Boss)
        {
            int clearedStage = MainStage;

            // 체크포인트는 절대 보스 서브스테이지 자체를 가리켜서는 안 된다 - 보스는
            // 일회성 조우이므로, 이후의 어떤 사망이든(몇 메인 스테이지를 더 진행한
            // 뒤라도) 이미 클리어된 보스와의 재대결이 아니라 그 보스로 이어지는
            // 마지막 일반 서브스테이지로 되돌아간다.
            checkpointMainStage = MainStage;
            checkpointSubStage = BossSubStage - 1;

            // 보스를 물리치면 항상 다음으로 진행한다. 마지막으로 제작된 스테이지를
            // 지나면 더 이상 진행할 곳이 없으므로, 대신 보스는 자신으로 이어지는
            // 서브스테이지로 되돌아가 계속 파밍 가능한 상태로 남는다.
            if (MainStage >= MaxMainStage)
            {
                SubStage = BossSubStage - 1;
            }
            else
            {
                MainStage++;
                SubStage = 1;
            }

            bossGaugePercent = 0;
            GameEvents.RaiseBossGaugeChanged(bossGaugePercent);
            // 코루틴이 실행되기 전에 미리 캡처한다: 위에서 이미 MainStage가 진행된
            // 상태이므로, 판단 기준은 현재 위치가 아니라 방금 클리어한 스테이지가
            // 되어야 한다.
            StartCoroutine(PlayVictoryThenShowBanner(clearedStage == SkillUnlockStage));
            return;
        }

        if (monster.Type == MonsterType.Elite)
        {
            // 엘리트는 일반 서브스테이지의 노가다를 마무리 짓는 존재다 - 이를 클리어
            // 하는 것이 곧 체크포인트이며, 항상 다음 서브스테이지로 진행한다. 반면
            // 사망은 같은 체크포인트로 되돌아가는데(HandlePlayerDied 참고), 이는
            // 앞으로 나아가는 게 아니라 현재 스테이지를 다시 플레이하게 만든다 -
            // 이를 넘어서는 유일한 방법은 클리어하는 것뿐이다.
            checkpointMainStage = MainStage;
            checkpointSubStage = SubStage;

            bossGaugePercent = 0;
            GameEvents.RaiseBossGaugeChanged(BossGaugeMax);

            SubStage++;
            StartCoroutine(PlayVictoryThenShowBanner());
            return;
        }

        bossGaugePercent = Mathf.Min(BossGaugeMax, bossGaugePercent + BossGaugePerKill);
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        if (bossGaugePercent >= BossGaugeMax)
        {
            // 엘리트 한 마리는 서브스테이지의 일반 몬스터 노가다가 끝날 때 등장한다 -
            // 고정된 타이머로 스폰되거나 미리 어딘가에 배치되어 있는 게 아니다.
            StartCoroutine(SpawnEliteAfterDelay());
            return;
        }

        StartCoroutine(SpawnNormalAfterDelay());
    }

    // 처치와 다음 배너/스폰 사이에 대기시켜서, 다음 조우를 향해 이미 포즈
    // 도중에 달려가는 게 아니라 플레이어가 먼저 승리를 만끽하게 한다.
    private IEnumerator PlayVictoryThenShowBanner(bool announceSkillUnlock = false)
    {
        // 엘리트와 보스 처치만 여기를 거치는데, 이는 정확히 "무언가를 클리어했다"고
        // 읽혀야 하는 대상들이다 - 일반 처치는 이 지점 이전에 이미 리턴한다.
        if (clearBanner != null) clearBanner.Show();

        if (combatLoop != null) yield return combatLoop.PlayVictorySequence();

        // 위의 "CLEAR !"와 동시에가 아니라 승리 포즈 이후에 표시된다: 둘 다 동일한
        // 단일 배너 오브젝트를 사용하므로, 함께 재생하면 두 번째 호출이 그냥
        // 첫 번째를 재시작해버릴 뿐이다(ClearBannerView.Show 참고). 순서를 나눠
        // 재생하면 해금 연출이 다음 스테이지 카드가 넘겨받기 전까지 잠깐이나마
        // 자기만의 박자를 가질 수 있다.
        if (announceSkillUnlock && clearBanner != null)
        {
            clearBanner.Show("Skill Unlocked !");
            yield return new WaitForSecondsRealtime(SkillUnlockBannerHold);
        }

        ShowBannerThenSpawn();
    }

    // 해금 연출이 다음 스테이지 카드로 넘어가기 전까지 화면을 차지하는
    // 대략적인 시간.
    private const float SkillUnlockBannerHold = 1.1f;

    private IEnumerator SpawnNormalAfterDelay()
    {
        int generation = stageGeneration;
        yield return new WaitForSeconds(nextMonsterDelay);
        if (generation != stageGeneration) yield break;

        spawner.SpawnNormal(MainStage, SubStage);
    }

    // 2번 박자: 게이지 자체의 연출이 가라앉은 후 "Elite Boss !"가 뜨기까지의
    // 시간.
    private const float EliteBannerDelayAfterFlourish = 0.5f;

    private IEnumerator SpawnEliteAfterDelay()
    {
        int generation = stageGeneration;

        // 1번 박자: 일반적인 nextMonsterDelay(일반 몬스터 리스폰 사이의 숨 고르기용으로
        // 튜닝된 값이며, 그 연출이 얼마나 걸리는지와는 무관하다)가 아니라 게이지
        // 자체의 강조/번쩍임 연출 시간에 맞춰 타이밍을 잡는다 - 그래야 아래에
        // 있는 배너 자체의 딜레이가 별도의 어긋나는 타이머가 아니라, 게이지의
        // 축하 연출이 실제로 끝나는 시점부터 카운트를 시작하게 된다.
        float flourishDelay = bossGauge != null ? bossGauge.ReadyFlourishDuration : nextMonsterDelay;
        yield return new WaitForSeconds(flourishDelay);
        if (generation != stageGeneration) yield break;

        yield return new WaitForSeconds(EliteBannerDelayAfterFlourish);
        if (generation != stageGeneration) yield break;

        // 3번 박자: 배너가 등장, 유지, 페이드아웃까지 스스로 완전히 재생을 마치기
        // 전까지는 엘리트가 걸어 들어오지 않는다 - 엘리트가 존재하기 훨씬 전에
        // 배너가 먼저 표시되므로, 텍스트는 항상 이미 화면에 있는 것에 붙인
        // 라벨이 아니라 앞으로 올 것을 알리는 전조로 읽히며, 자신이 예고하는
        // 등장과 절대 겹치지 않는다.
        if (clearBanner != null)
        {
            clearBanner.Show("Elite Boss !");
            yield return new WaitForSeconds(clearBanner.TotalPlayDuration);
            if (generation != stageGeneration) yield break;
        }

        spawner.SpawnElite(MainStage, SubStage);
    }

    private void ShowBannerThenSpawn()
    {
        // 이제 이 알림이 스테이지를 주도한다; 그 이전부터 큐잉되어 있던 것은
        // 전부 낡은 것이다.
        int generation = ++stageGeneration;

        if (player != null) player.RestoreToFullHp();

        // 엘리트 처치 축하 연출은 게이지를 가득 찬 상태로 보여주지만
        // (HandleMonsterDied 참고), 실제 내부 카운터는 새 서브스테이지를 위해
        // 이미 0으로 돌아가 있다 - 새 스테이지가 항상 빈 게이지로 시작되도록
        // 여기서 UI를 다시 동기화한다.
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        GameEvents.RaiseStageChanged(MainStage, SubStage);

        // 마지막 서브스테이지는 보스 조우이므로, 번호가 아니라 그 사실 그대로
        // 알린다.
        string label = SubStage == BossSubStage ? $"Stage {MainStage}-Boss" : $"Stage {MainStage}-{SubStage}";
        if (banner != null)
        {
            banner.Show(label, () =>
            {
                if (generation != stageGeneration) return;

                SpawnForCurrentSubStage();
            });
        }
        else
        {
            SpawnForCurrentSubStage();
        }
    }

    private void SpawnForCurrentSubStage()
    {
        if (SubStage == BossSubStage)
        {
            // 보스는 바로 이전 서브스테이지의 엘리트를 기준으로 크기가 정해진다.
            spawner.SpawnBoss(MainStage, BossSubStage - 1);
            if (clearBanner != null) clearBanner.Show("Final Boss !");
        }
        else
        {
            spawner.SpawnNormal(MainStage, SubStage);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 테스트 전용 점프: 현재 조우를 초기화하고 일반적인 처치/게이지 진행
    // 과정을 완전히 건너뛴 채 요청된 스테이지로 ShowBannerThenSpawn을 다시
    // 진입시킨다. 위의 #if로 릴리스 빌드에서는 제외되므로 실제 출시본에는
    // 절대 포함되지 않는다.
    public void DebugJumpTo(int mainStage, int subStage)
    {
        StopAllCoroutines();
        stageGeneration++;

        if (banner != null) banner.Cancel();
        if (clearBanner != null) clearBanner.Cancel();

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            Destroy(monster.gameObject);
        }
        currentMonster = null;

        // PlayDeathThenRestart와 동일한 필드 초기화 - 드롭이 진행 중일 때 점프하더라도
        // 떠나는 스테이지에 물약/장비 픽업이 낙오되어 남아있으면 안 된다.
        foreach (StatPotionPickup potion in FindObjectsByType<StatPotionPickup>(FindObjectsSortMode.None))
        {
            Destroy(potion.gameObject);
        }
        foreach (EquipmentDropPickup pickup in FindObjectsByType<EquipmentDropPickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }
        foreach (StoneDropPickup stone in FindObjectsByType<StoneDropPickup>(FindObjectsSortMode.None))
        {
            Destroy(stone.gameObject);
        }

        // 위에서 픽업을 곧바로 Destroy()하면, 그게 아직 던져지는 도중이었을 경우
        // (idle hold는 걸렸지만 아직 풀리지 않은 상태) 자신의 PopIdleHold 호출을
        // 건너뛰게 된다 - 그냥 두면 그걸 풀어줄 살아있는 존재가 아무것도 남지
        // 않아 플레이어가 영원히 idle 상태에 갇힌다. 이 시점에는 idle hold를
        // 걸어놨을 가능성이 있는 모든 픽업이 이미 사라진 상태이므로, 여기서
        // 정리해도 항상 안전하다.
        if (combatLoop != null) combatLoop.ClearIdleHold();

        MainStage = Mathf.Clamp(mainStage, 1, MaxMainStage);
        SubStage = Mathf.Clamp(subStage, 1, BossSubStage);
        // 점프할 때 체크포인트도 함께 설정되므로, 테스트 도중 사망해도 실제 런이
        // 마지막으로 체크포인트를 찍었던 곳까지가 아니라 점프해 들어온 스테이지로
        // 되돌아간다.
        checkpointMainStage = MainStage;
        checkpointSubStage = SubStage == BossSubStage ? BossSubStage - 1 : SubStage;

        bossGaugePercent = 0;
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        if (player != null)
        {
            player.RestoreToFullHp();
            player.IsInvulnerable = false;
        }
        if (combatLoop != null) combatLoop.ClearSuspend();

        ShowBannerThenSpawn();
    }
#endif
}
