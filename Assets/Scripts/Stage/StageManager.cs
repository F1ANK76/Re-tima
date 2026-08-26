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
    // Absent, the run starts the moment the scene does - which is what the play-mode tests
    // and any scene without a menu expect.
    [SerializeField] private TitleScreenView titleScreen;
    [SerializeField] private CombatLoop combatLoop;
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private BossGaugeView bossGauge;

    // Each main stage is four normal substages then the boss: 1-1..1-4, then 1-5 is the boss
    // encounter. Shortened from 10 - at 10, clearing a main stage took ~100 kills (9 normal
    // substages x 10 kills to fill the boss gauge, plus the elite and boss), which read as a
    // long grind rather than steady progress. MonsterSpawner's HP curve still reserves a
    // band of ten per main stage ((mainStage - 1) * 10 + subStage) - that's untouched by this,
    // it just leaves subStage 5-9 of each band unused rather than needing to be re-packed.
    public const int BossSubStage = 5;
    private const int BossGaugePerKill = 10;
    private const int BossGaugeMax = 100;

    // Content ends here for now. Clearing the final stage's boss loops back to the substage
    // right before it rather than advancing into a stage that has no roster, so the run keeps
    // going (3-9 -> 3-10 boss -> 3-9 -> ...) instead of dead-ending.
    public const int MaxMainStage = 3;

    // Beating this stage's boss is what unlocks the player's ultimate (see UltimateManager),
    // so the clear gets an extra callout on top of the usual one.
    public const int SkillUnlockStage = 1;

    public int MainStage { get; private set; } = 1;
    public int SubStage { get; private set; } = 1;

    private int bossGaugePercent;
    private Monster currentMonster;

    // Bumped every time a new stage run begins. Spawns are queued behind waits (the stage
    // banner, the between-monsters delay) that outlive the run that scheduled them, so each
    // one captures this and bails if it no longer matches - otherwise a death mid-wait lets
    // the old run's monster land on top of the fresh stage.
    private int stageGeneration;

    // The last substage the player actually cleared - dying rewinds here instead of
    // wherever they happened to die, so a death never loses more than the in-progress attempt.
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

        // The menu holds the run until Play; the first stage banner then comes up underneath
        // it and the menu dissolves into it.
        if (titleScreen != null) titleScreen.Show(ShowBannerThenSpawn);
        else ShowBannerThenSpawn();
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        currentMonster = monster;
    }

    // Every monster currently in the scene stops attacking but stays put (no death
    // rewards, no progression) until the death animation actually finishes - only then
    // do they get cleared out for the checkpoint respawn. The player is made invulnerable
    // for the duration: at 2 HP a single stray hit mid-sequence would kill them again and
    // restart this whole coroutine before it ever reached the cleanup below.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();
        stageGeneration++;

        // The banner runs its own coroutine on its own GameObject, so StopAllCoroutines
        // above doesn't reach it. Left alone, a spawn queued behind an in-flight banner
        // fires after the wipe below and drops a monster into the fresh stage.
        if (banner != null) banner.Cancel();

        // A death landing in the same beat as the kill that raised it would otherwise
        // leave "CLEAR !" hanging over the death sequence.
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

        // Dims the screen and pops "FAIL !" for a beat before the checkpoint rewind below -
        // yielded (not fire-and-forget like clearBanner) so the restart never lands while the
        // fade is still up.
        if (failBanner != null) yield return failBanner.Play();

        foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            Destroy(monster.gameObject);
        }
        currentMonster = null;

        // A drop from the kill that killed the player (or one already in flight from an
        // earlier kill) freezes in place rather than finishing its slide-in - see
        // StatPotionPickup/EquipmentDropPickup.HandlePlayerDied. Cleared here alongside the
        // monsters so it doesn't linger as clutter into the respawned field.
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

        // Directly Destroy()ing a pickup above skips its own PopIdleHold if it was still
        // mid-toss (idle hold pushed, not yet released) - left alone that strands the player
        // in idle forever, since nothing is left alive to ever release it. Every pickup that
        // could have been holding it up is gone by this line, so it is always safe to clear.
        if (combatLoop != null) combatLoop.ClearIdleHold();

        MainStage = checkpointMainStage;
        SubStage = checkpointSubStage;
        bossGaugePercent = 0;
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        // This is the revival moment: the death animation is done and the field is clear,
        // so bring the player back at full health before they can be touched again.
        // Invulnerability is cleared here rather than after the banner - leaving it set
        // across the banner risks stranding the flag on if another death interrupts first.
        if (player != null)
        {
            player.RestoreToFullHp();
            player.IsInvulnerable = false;
        }

        // A second death arriving mid-death-sequence re-enters HandlePlayerDied, whose
        // StopAllCoroutines() above can cut off CombatLoop.PlayDeathSequence before it
        // restores its own suspend flag - leaving combat permanently frozen even after
        // the player respawns. Combat is always meant to be live once the player's back
        // up, so force it on here instead of trusting the interrupted coroutine.
        if (combatLoop != null) combatLoop.ClearSuspend();

        ShowBannerThenSpawn();
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (currentMonster == monster) currentMonster = null;

        // The boss only ever appears as the final substage's encounter, so beating it
        // clears the whole main stage.
        if (monster.Type == MonsterType.Boss)
        {
            int clearedStage = MainStage;

            // The checkpoint must never point at the boss substage itself - a boss is a
            // one-time encounter, so any later death (even several main stages further in)
            // rewinds to the last normal substage that led up to it, not back into a
            // rematch with a boss that's already been cleared.
            checkpointMainStage = MainStage;
            checkpointSubStage = BossSubStage - 1;

            // Beating a boss always advances. Past the last authored stage there is nowhere
            // to advance to, so the boss instead loops back to the substage feeding into it
            // and stays farmable.
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
            // Captured before the coroutine runs: MainStage has already been advanced above,
            // so the check has to be against what was just cleared, not where we are now.
            StartCoroutine(PlayVictoryThenShowBanner(clearedStage == SkillUnlockStage));
            return;
        }

        if (monster.Type == MonsterType.Elite)
        {
            // The elite caps off a normal substage's grind - clearing it is the checkpoint,
            // and always advances to the next substage. A death instead rewinds to this same
            // checkpoint (see HandlePlayerDied), which is what replays the current stage
            // rather than pushing forward - clearing is the only way past it.
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
            // One elite appears at the end of the substage's normal-monster grind -
            // not spawned on a fixed timer or pre-placed anywhere.
            StartCoroutine(SpawnEliteAfterDelay());
            return;
        }

        StartCoroutine(SpawnNormalAfterDelay());
    }

    // Held between the kill and the next banner/spawn so the player celebrates before
    // the game moves on, instead of already running toward the next encounter mid-pose.
    private IEnumerator PlayVictoryThenShowBanner(bool announceSkillUnlock = false)
    {
        // Only the elite and boss kills route through here, which is exactly the set that
        // should read as clearing something - normal kills return before this.
        if (clearBanner != null) clearBanner.Show();

        if (combatLoop != null) yield return combatLoop.PlayVictorySequence();

        // Shown after the victory pose rather than alongside the "CLEAR !" above: both use
        // the same single banner object, so playing them together would just have the second
        // call restart the first (see ClearBannerView.Show). Sequencing them gives the unlock
        // its own beat, held briefly before the next stage card takes over.
        if (announceSkillUnlock && clearBanner != null)
        {
            clearBanner.Show("Skill Unlocked !");
            yield return new WaitForSecondsRealtime(SkillUnlockBannerHold);
        }

        ShowBannerThenSpawn();
    }

    // Roughly how long the unlock callout owns the screen before the stage card follows.
    private const float SkillUnlockBannerHold = 1.1f;

    private IEnumerator SpawnNormalAfterDelay()
    {
        int generation = stageGeneration;
        yield return new WaitForSeconds(nextMonsterDelay);
        if (generation != stageGeneration) yield break;

        spawner.SpawnNormal(MainStage, SubStage);
    }

    // Beat 2: how long after the gauge's own flourish settles before "Elite Boss !" pops up.
    private const float EliteBannerDelayAfterFlourish = 0.5f;

    private IEnumerator SpawnEliteAfterDelay()
    {
        int generation = stageGeneration;

        // Beat 1: timed off the gauge's own punch/flash flourish rather than the generic
        // nextMonsterDelay (tuned for a normal-monster respawn breather, and unrelated to
        // how long that flourish takes) - so the banner's own delay below starts counting
        // from when the gauge actually finishes celebrating, not a separate, drifting timer.
        float flourishDelay = bossGauge != null ? bossGauge.ReadyFlourishDuration : nextMonsterDelay;
        yield return new WaitForSeconds(flourishDelay);
        if (generation != stageGeneration) yield break;

        yield return new WaitForSeconds(EliteBannerDelayAfterFlourish);
        if (generation != stageGeneration) yield break;

        // Beat 3: the elite doesn't walk in until the banner has completely finished playing
        // itself out (pop-in, hold, AND fade-out) - shown well before it exists, so the text
        // always reads as the herald rather than a label slapped on something already on
        // screen, and never overlaps the arrival it's announcing.
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
        // This announcement now owns the stage; anything still queued from before is stale.
        int generation = ++stageGeneration;

        if (player != null) player.RestoreToFullHp();

        // The elite-kill celebration displays the gauge at full (see HandleMonsterDied),
        // but the underlying counter is already back at 0 for the new substage - resync
        // the UI here so the fresh stage always opens on an empty gauge.
        GameEvents.RaiseBossGaugeChanged(bossGaugePercent);

        GameEvents.RaiseStageChanged(MainStage, SubStage);

        // The final substage is the boss encounter, so announce it as such rather than
        // by its number.
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
            // The boss is sized against the elite from the substage right before it.
            spawner.SpawnBoss(MainStage, BossSubStage - 1);
            if (clearBanner != null) clearBanner.Show("Final Boss !");
        }
        else
        {
            spawner.SpawnNormal(MainStage, SubStage);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Test-only jump: wipes the current encounter and re-enters ShowBannerThenSpawn on the
    // requested stage, bypassing the normal kill/gauge progression entirely. Guarded out of
    // release builds by the #if above, so it never ships.
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

        // Same field-wipe as PlayDeathThenRestart - a jump mid-drop shouldn't leave a stray
        // potion/equipment pickup behind on the stage being left.
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

        // Directly Destroy()ing a pickup above skips its own PopIdleHold if it was still
        // mid-toss (idle hold pushed, not yet released) - left alone that strands the player
        // in idle forever, since nothing is left alive to ever release it. Every pickup that
        // could have been holding it up is gone by this line, so it is always safe to clear.
        if (combatLoop != null) combatLoop.ClearIdleHold();

        MainStage = Mathf.Clamp(mainStage, 1, MaxMainStage);
        SubStage = Mathf.Clamp(subStage, 1, BossSubStage);
        // Jumping in sets the checkpoint too, so a death while testing rewinds back to the
        // jumped-to stage rather than all the way to wherever the real run last checkpointed.
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
