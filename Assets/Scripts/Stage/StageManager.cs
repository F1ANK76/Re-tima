using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    [SerializeField] private float nextMonsterDelay = 2f;

    [SerializeField] private MonsterSpawner spawner;
    [SerializeField] private StageBannerView banner;
    [SerializeField] private ClearBannerView clearBanner;
    [SerializeField] private FailBannerView failBanner;
    [SerializeField] private GameCompleteView gameCompleteView;
    [SerializeField] private TitleScreenView titleScreen;
    [SerializeField] private CombatLoop combatLoop;
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private BossGaugeView bossGauge;

    public const int BossSubStage = 3;
    private const int BossGaugePerKill = 10;
    private const int BossGaugeMax = 100;

    public const int MaxMainStage = 2;

    public const int SkillUnlockStage = 1;

    public int MainStage { get; private set; } = 1;
    public int SubStage { get; private set; } = 1;

    private int bossGaugePercent;
    private Monster currentMonster;

    private float runStartTime;

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

        if (titleScreen != null) titleScreen.Show(BeginRun);
        else BeginRun();
    }

    private void BeginRun()
    {
        runStartTime = Time.time; // 실제 플레이가 시작된 시점 기록
        ShowBannerThenSpawn(); // 첫 스테이지 시작
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        currentMonster = monster;
    }

    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        banner.Cancel();

        clearBanner.Cancel();

        player.IsInvulnerable = true;

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            monster.StopAttacking();
        }

        StartCoroutine(PlayDeathThenRestart());
    }

    private IEnumerator PlayDeathThenRestart()
    {
        yield return combatLoop.PlayDeathSequence();

        yield return failBanner.Play();

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            Destroy(monster.gameObject);
        }
        currentMonster = null;

        foreach (DropPickup pickup in FindObjectsByType<DropPickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }

        combatLoop.ClearIdleHold();

        if (SubStage == BossSubStage) SubStage = BossSubStage - 1;
        bossGaugePercent = 0;
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        player.RestoreToFullHp();
        player.IsInvulnerable = false;

        combatLoop.ClearSuspend();

        ShowBannerThenSpawn();
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (currentMonster == monster) currentMonster = null;

        if (monster.Type == MonsterType.Boss)
        {
            int clearedStage = MainStage;
            // 지금 제작된 콘텐츠는 여기서 끝난다 - 다음 스테이지로 넘기는 대신 종료 화면을 띄운다.
            bool isFinalClear = clearedStage >= MaxMainStage;

            bossGaugePercent = 0;
            GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

            if (isFinalClear)
            {
                SubStage = BossSubStage - 1;
                StartCoroutine(PlayVictoryThenShowGameComplete());
            }
            else
            {
                MainStage++;
                SubStage = 1;
                StartCoroutine(PlayVictoryThenShowBanner(clearedStage == SkillUnlockStage));
            }
            return;
        }

        if (monster.Type == MonsterType.Elite)
        {
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
            StartCoroutine(SpawnEliteAfterDelay());
            return;
        }

        StartCoroutine(SpawnNormalAfterDelay(monster.DeathVisualDuration));
    }

    private IEnumerator PlayVictoryThenShowBanner(bool announceSkillUnlock = false)
    {
        clearBanner.Show();

        yield return combatLoop.PlayVictorySequence();

        if (announceSkillUnlock)
        {
            clearBanner.Show("Skill Unlocked !");
            yield return new WaitForSecondsRealtime(SkillUnlockBannerHold);
        }

        ShowBannerThenSpawn();
    }

    private const float SkillUnlockBannerHold = 1.1f;

    // 최종 보스(MaxMainStage) 클리어 전용 - 다음 스테이지 배너 대신 종료 화면으로 이어진다.
    private IEnumerator PlayVictoryThenShowGameComplete()
    {
        clearBanner.Show();

        yield return combatLoop.PlayVictorySequence();

        float elapsedSeconds = Time.time - runStartTime;
        gameCompleteView.Show(elapsedSeconds, HandleRestartRequested);
    }

    private void HandleRestartRequested()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator SpawnNormalAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        spawner.Spawn(MainStage, SubStage, MonsterType.Normal);
    }

    private const float EliteBannerDelayAfterFlourish = 0.5f;

    private IEnumerator SpawnEliteAfterDelay()
    {
        float flourishDelay = bossGauge.ReadyFlourishDuration;
        yield return new WaitForSeconds(flourishDelay);

        yield return new WaitForSeconds(EliteBannerDelayAfterFlourish);

        clearBanner.Show("Elite Boss !");
        yield return new WaitForSeconds(clearBanner.TotalPlayDuration);

        spawner.Spawn(MainStage, SubStage, MonsterType.Elite);
    }

    private void ShowBannerThenSpawn()
    {
        player.RestoreToFullHp();

        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);
        GameEvents.RaiseStageChanged(MainStage, SubStage);

        string label = SubStage == BossSubStage ? $"Stage {MainStage}-Boss" : $"Stage {MainStage}-{SubStage}";

        banner.Show(label, SpawnForCurrentSubStage);
    }

    private void SpawnForCurrentSubStage()
    {
        if (SubStage == BossSubStage)
        {
            // 보스는 바로 이전 서브스테이지의 엘리트를 기준으로 크기가 정해진다.
            spawner.Spawn(MainStage, BossSubStage - 1, MonsterType.Boss);
            clearBanner.Show("Stage Boss !");
        }
        else
        {
            spawner.Spawn(MainStage, SubStage, MonsterType.Normal);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugJumpTo(int mainStage, int subStage)
    {
        StopAllCoroutines();

        banner.Cancel();
        clearBanner.Cancel();

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            Destroy(monster.gameObject);
        }
        currentMonster = null;

        foreach (DropPickup pickup in FindObjectsByType<DropPickup>(FindObjectsSortMode.None))
        {
            Destroy(pickup.gameObject);
        }

        combatLoop.ClearIdleHold();

        MainStage = Mathf.Clamp(mainStage, 1, MaxMainStage);
        SubStage = Mathf.Clamp(subStage, 1, BossSubStage);

        bossGaugePercent = 0;
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        player.RestoreToFullHp();
        player.IsInvulnerable = false;
        combatLoop.ClearSuspend();

        ShowBannerThenSpawn();
    }
#endif
}
