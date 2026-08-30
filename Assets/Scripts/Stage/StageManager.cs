using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    [SerializeField] private float nextMonsterDelay = 2f;

    [SerializeField] private MonsterSpawner spawner;
    [SerializeField] private StageConfigSO stageConfig;
    [SerializeField] private StageBannerView banner;
    [SerializeField] private ClearBannerView clearBanner;
    [SerializeField] private FailBannerView failBanner;
    [SerializeField] private GameCompleteView gameCompleteView;
    // 없으면 런은 씬이 시작되는 순간 바로 시작된다 - 플레이 모드 테스트나
    // 메뉴가 없는 씬에서 기대하는 동작이 바로 이것이다.
    [SerializeField] private TitleScreenView titleScreen;
    [SerializeField] private CombatLoop combatLoop;
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private BossGaugeView bossGauge;

    // 메인 스테이지 = 일반 서브스테이지 2개 + 보스(1-1..1-2, 1-3이 보스 조우). MonsterSpawner의
    // HP/공격력 곡선(GetNormalHp/GetNormalAttack)이 이 값을 직접 참조해서 스테이지당 칸 수를
    // 정하므로, 여길 바꾸면 그쪽도 자동으로 맞춰진다.
    public const int BossSubStage = 3;
    private const int BossGaugePerKill = 10;
    private const int BossGaugeMax = 100;

    // itch.io 1차 출시 범위 - 스테이지 3(스톤 시스템 해금)은 아직 제대로 즐길 만큼의 콘텐츠가
    // 갖춰지지 않아 다음 업데이트로 미뤄뒀다. 이 값을 클리어하면 다음 스테이지 대신 종료 화면
    // (GameCompleteView)이 뜬다.
    public const int MaxMainStage = 2;

    // 이 스테이지의 보스를 물리치는 것이 플레이어의 궁극기를 해금하는 조건이므로
    // (UltimateManager 참고), 이 클리어에는 평소보다 추가 연출이 붙는다.
    public const int SkillUnlockStage = 1;

    public int MainStage { get; private set; } = 1;
    public int SubStage { get; private set; } = 1;

    private int bossGaugePercent;
    private Monster currentMonster;

    // 실제 플레이가 시작된 시점(타이틀 화면이 있으면 Play를 누른 순간) - 3-3(최종 보스) 클리어
    // 배너에 띄우는 "걸린 시간"의 기준점이다.
    private float runStartTime;

    // 스테이지 런이 시작될 때마다 증가한다. 스폰은 자신을 예약한 런보다 오래 사는 대기(스테이지
    // 배너, 몬스터 사이의 딜레이) 뒤에 큐잉되므로, 이 값을 캡처해두고 더 이상 일치하지 않으면
    // 실행을 취소한다 - 없으면 대기 중 사망 시 이전 런의 몬스터가 새 스테이지에 떨어진다.
    private int stageGeneration;

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
        if (titleScreen != null) titleScreen.Show(BeginRun);
        else BeginRun();
    }

    // 클리어 타임의 기준점은 씬이 로드된 순간이 아니라 실제로 플레이가 시작되는 순간이어야
    // 한다 - 타이틀 화면에 얼마나 머물렀는지는 "깨는 데 걸린 시간"에 포함되면 안 된다.
    private void BeginRun()
    {
        runStartTime = Time.time;
        ShowBannerThenSpawn();
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        currentMonster = monster;
    }

    // 사망 애니메이션이 끝날 때까지 씬의 모든 몬스터는 공격만 멈추고 제자리에 남는다(사망 보상도
    // 진행도 없음) - 정리는 그 후 같은 서브스테이지를 다시 시작하기 위해서만. 이 기간 플레이어는 무적이다:
    // HP 2에서 스치는 한 방만 맞아도 재사망해, 아래 정리에 도달하기 전에 코루틴이 재시작된다.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();
        stageGeneration++;

        // 배너는 자기 GameObject에서 자기 코루틴을 돌려 위의 StopAllCoroutines가 닿지 않는다.
        // 그냥 두면 배너 뒤에 큐잉된 스폰이 아래 초기화 이후 실행돼 새 스테이지에 몬스터를 떨군다.
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

        // 화면을 어둡게 하고 아래에서 서브스테이지를 재시작하기 전에 잠깐 "FAIL !"을 띄운다 - clearBanner처럼
        // 발사 후 무시가 아니라 yield로 기다리므로, 페이드가 떠 있는 동안 재시작이 일어나지 않는다.
        if (failBanner != null) yield return failBanner.Play();

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            Destroy(monster.gameObject);
        }
        currentMonster = null;

        // 플레이어를 죽인 처치(또는 그 이전 처치)의 드롭은 미끄러져 들어오던 걸 마치지 못하고
        // 제자리에 멈춘다 - StatPotionPickup/EquipmentDropPickup.HandlePlayerDied 참고. 리스폰된
        // 필드에 잡동사니로 남지 않게 몬스터와 함께 정리한다.
        foreach (StatPotionPickup potion in FindObjectsByType<StatPotionPickup>(FindObjectsSortMode.None))
        {
            Destroy(potion.gameObject);
        }
        foreach (EquipmentDropPickup pickup in FindObjectsByType<EquipmentDropPickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }

        // 위에서 픽업을 곧바로 Destroy()하면 아직 던져지는 도중(idle hold는 걸렸고 아직 안 풀린
        // 상태)일 때 자신의 PopIdleHold 호출을 건너뛴다 - 그러면 풀어줄 주체가 아무것도 남지 않아
        // 플레이어가 영원히 idle에 갇힌다. 이 시점엔 idle hold를 걸었을 모든 픽업이 이미 사라졌으므로
        // 여기서 정리해도 항상 안전하다.
        if (combatLoop != null) combatLoop.ClearIdleHold();

        // 실패한 서브스테이지를 그대로 반복한다 - 다만 보스 조우(항상 마지막 서브스테이지) 도중의
        // 사망만은 그 보스로 이어지는 직전 서브스테이지로 되돌린다, 클리어된 보스를 다시 붙잡고
        // 있지 않도록.
        if (SubStage == BossSubStage) SubStage = BossSubStage - 1;
        bossGaugePercent = 0;
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        // 부활 시점: 사망 애니메이션이 끝나고 필드도 정리됐으니, 다시 피격 가능해지기 전에 풀피로
        // 되돌린다. 무적은 배너 이후가 아니라 여기서 해제한다 - 배너 재생 중까지 켜두면 그 사이
        // 또 다른 사망이 끼어들었을 때 플래그가 켜진 채 남을 위험이 있다.
        if (player != null)
        {
            player.RestoreToFullHp();
            player.IsInvulnerable = false;
        }

        // 사망 시퀀스 중 두 번째 사망이 나면 HandlePlayerDied가 재호출되고, 위의 StopAllCoroutines()가
        // CombatLoop.PlayDeathSequence를 suspend 플래그 복원 전에 끊어버릴 수 있다 - 그러면 리스폰
        // 후에도 전투가 영구히 멈춘다. 부활 후엔 전투가 항상 살아있어야 하므로 여기서 강제로 켜준다.
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
            // 지금 제작된 콘텐츠는 여기서 끝난다 - 다음 스테이지로 넘기는 대신 종료 화면을 띄운다.
            bool isFinalClear = clearedStage >= MaxMainStage;

            // 보스를 물리치면 항상 다음으로 진행한다. 마지막 제작 스테이지를 지나면 갈 곳이 없으므로,
            // 보스는 자신으로 이어지는 서브스테이지로 되돌아가 계속 파밍 가능한 상태로 남는다.
            if (isFinalClear)
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

            if (isFinalClear)
            {
                StartCoroutine(PlayVictoryThenShowGameComplete());
            }
            else
            {
                // 코루틴 실행 전에 미리 캡처한다: 위에서 MainStage가 이미 진행됐으므로, 판단 기준은
                // 현재 위치가 아니라 방금 클리어한 스테이지여야 한다.
                StartCoroutine(PlayVictoryThenShowBanner(clearedStage == SkillUnlockStage));
            }
            return;
        }

        if (monster.Type == MonsterType.Elite)
        {
            // 엘리트는 일반 서브스테이지 노가다의 마무리 - 클리어하면 항상 다음 서브스테이지로
            // 진행한다. 반면 사망은 같은 서브스테이지를 다시 반복하게 만든다(HandlePlayerDied
            // 참고) - 넘어서는 방법은 클리어뿐이다.
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

        StartCoroutine(SpawnNormalAfterDelay(monster.DeathVisualDuration));
    }

    // 처치와 다음 배너/스폰 사이에 대기시켜서, 다음 조우를 향해 이미 포즈
    // 도중에 달려가는 게 아니라 플레이어가 먼저 승리를 만끽하게 한다.
    private IEnumerator PlayVictoryThenShowBanner(bool announceSkillUnlock = false)
    {
        // 엘리트와 보스 처치만 여기를 거치는데, 이는 정확히 "무언가를 클리어했다"고
        // 읽혀야 하는 대상들이다 - 일반 처치는 이 지점 이전에 이미 리턴한다.
        if (clearBanner != null) clearBanner.Show();

        if (combatLoop != null) yield return combatLoop.PlayVictorySequence();

        // 위의 "CLEAR !"와 동시가 아니라 승리 포즈 이후에 표시한다: 둘 다 같은 단일 배너 오브젝트를
        // 쓰므로 함께 재생하면 두 번째 호출이 첫 번째를 재시작할 뿐이다(ClearBannerView.Show 참고).
        // 순서를 나누면 해금 연출이 다음 스테이지 카드로 넘어가기 전까지 자기 박자를 가진다.
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

    // 최종 보스(MaxMainStage) 클리어 전용 - 다음 스테이지 배너 대신 종료 화면으로 이어진다.
    private IEnumerator PlayVictoryThenShowGameComplete()
    {
        if (clearBanner != null) clearBanner.Show();

        if (combatLoop != null) yield return combatLoop.PlayVictorySequence();

        float elapsedSeconds = Time.time - runStartTime;
        if (gameCompleteView != null) gameCompleteView.Show(elapsedSeconds, HandleRestartRequested);
    }

    // 개별 매니저(장비/스톤/스탯/게이지 등)를 하나씩 손으로 되돌리는 대신 씬을 통째로 다시
    // 로드한다 - "1-1부터 초기화 상태"를 보장하는 가장 확실한 방법이고, 새로 추가되는 진행
    // 상태가 생겨도 여기 손댈 필요가 없다.
    private void HandleRestartRequested()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator SpawnNormalAfterDelay(float delay)
    {
        int generation = stageGeneration;
        yield return new WaitForSeconds(delay);
        if (generation != stageGeneration) yield break;

        spawner.SpawnNormal(MainStage, SubStage);
    }

    // 2번 박자: 게이지 자체의 연출이 가라앉은 후 "Elite Boss !"가 뜨기까지의
    // 시간.
    private const float EliteBannerDelayAfterFlourish = 0.5f;

    private IEnumerator SpawnEliteAfterDelay()
    {
        int generation = stageGeneration;

        // 1번 박자: nextMonsterDelay(일반 몬스터 리스폰 사이의 숨 고르기용 값이며 연출 길이와는
        // 무관)가 아니라 게이지 자체의 강조/번쩍임 연출 시간에 맞춘다 - 그래야 아래 배너 딜레이가
        // 어긋난 별도 타이머가 아니라 게이지 축하 연출이 실제로 끝나는 시점부터 카운트된다.
        float flourishDelay = bossGauge != null ? bossGauge.ReadyFlourishDuration : nextMonsterDelay;
        yield return new WaitForSeconds(flourishDelay);
        if (generation != stageGeneration) yield break;

        yield return new WaitForSeconds(EliteBannerDelayAfterFlourish);
        if (generation != stageGeneration) yield break;

        // 3번 박자: 배너가 등장-유지-페이드아웃까지 스스로 다 재생하기 전엔 엘리트가 걸어 들어오지
        // 않는다 - 엘리트가 존재하기 훨씬 전에 배너가 뜨므로, 텍스트는 이미 화면에 있는 것의 라벨이
        // 아니라 앞으로 올 것의 전조로 읽히며 자신이 예고하는 등장과 절대 겹치지 않는다.
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

        // 엘리트 처치 축하 연출은 게이지를 가득 찬 상태로 보여주지만(HandleMonsterDied 참고) 실제
        // 카운터는 새 서브스테이지를 위해 이미 0이다 - 빈 게이지로 시작하도록 UI를 재동기화한다.
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
    // 테스트 전용 점프: 현재 조우를 초기화하고 처치/게이지 진행을 전부 건너뛴 채 요청된 스테이지로
    // ShowBannerThenSpawn을 재진입시킨다. 위의 #if로 릴리스 빌드에서는 제외된다.
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

        // 위에서 픽업을 곧바로 Destroy()하면 아직 던져지는 도중(idle hold는 걸렸고 아직 안 풀린
        // 상태)일 때 자신의 PopIdleHold 호출을 건너뛴다 - 그러면 풀어줄 주체가 아무것도 남지 않아
        // 플레이어가 영원히 idle에 갇힌다. 이 시점엔 idle hold를 걸었을 모든 픽업이 이미 사라졌으므로
        // 여기서 정리해도 항상 안전하다.
        if (combatLoop != null) combatLoop.ClearIdleHold();

        MainStage = Mathf.Clamp(mainStage, 1, MaxMainStage);
        SubStage = Mathf.Clamp(subStage, 1, BossSubStage);

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
