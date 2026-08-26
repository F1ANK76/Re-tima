using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ParryManager : MonoBehaviour
{
    public static ParryManager Instance { get; private set; }

    [SerializeField] private Button parryButton;
    [SerializeField] private GameObject cooldownOverlay;
    [SerializeField] private Text cooldownText;
    [SerializeField] private WeaponSwing weaponSwing;
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private ParrySuccessEffect parrySuccessVfx;
    [SerializeField] private ParticleSystem teleportVfx;
    [SerializeField] private CombatLoop combatLoop;

    private const float ParryWindowDuration = 0.5f;
    private const int CooldownSeconds = 3;

    private const string DefendStateName = "Defend_SwordAndShield";
    private const string IdleStateName = "Idle_Battle_SwordAndShield";
    private const string RiposteStateName = "Attack04_SwordAndShiled";
    // Matches Attack04_SwordAndShiled's own clip length (frames 0-16 at 30fps, per its
    // FBX import data) - same convention WeaponSwing uses for Attack01's impact timing.
    private const float RiposteAnimDuration = 16f / 30f;
    // Matches the Teleport prefab's own longest sub-system lifetime, so it plays out
    // one full cycle instead of being cut off mid-flourish.
    private const float TeleportVfxDuration = 0.7f;

    private bool parryWindowOpen;
    private bool onCooldown;
    private bool duelActive;
    private Monster currentDuelist;
    private Coroutine cooldownRoutine;

    // Only monsters that telegraph before striking can be parried - normal monsters
    // attack on a fixed timer with no tell, so there'd be nothing to react to.
    private static bool IsParryTarget(MonsterType type) =>
        type == MonsterType.Boss || type == MonsterType.Elite;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnMonsterSpawned += HandleMonsterSpawned;
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnPlayerDied += HandlePlayerDied;

        if (parryButton != null) parryButton.onClick.AddListener(OnParryButtonPressed);

        if (cooldownOverlay != null) cooldownOverlay.SetActive(false);
        UpdateButtonInteractable();
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterSpawned -= HandleMonsterSpawned;
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnPlayerDied -= HandlePlayerDied;

        if (parryButton != null) parryButton.onClick.RemoveListener(OnParryButtonPressed);
    }

    // StageManager destroys the current duelist without a normal death, so the duel has
    // to be torn down explicitly here too.
    private void HandlePlayerDied()
    {
        currentDuelist = null;
        duelActive = false;

        // Dying mid-riposte would otherwise leave the attack tick held off into the
        // respawned stage, where nothing is left to release it.
        if (combatLoop != null) combatLoop.RiposteInProgress = false;

        UpdateButtonInteractable();
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        if (IsParryTarget(monster.Type))
        {
            currentDuelist = monster;
            duelActive = true;
            UpdateButtonInteractable();
        }
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (monster == currentDuelist)
        {
            currentDuelist = null;
            duelActive = false;
            UpdateButtonInteractable();
        }
    }

    private void OnParryButtonPressed()
    {
        if (onCooldown || parryWindowOpen) return;
        StartCoroutine(ParryWindowRoutine());
    }

private IEnumerator ParryWindowRoutine()
    {
        parryWindowOpen = true;

        // A normal swing already in flight shouldn't land its hit or keep showing its
        // slash once the pose below cuts over to Defend.
        if (combatLoop != null) combatLoop.CancelPendingAttack();

        // Animator.Play jumps straight into the state with no blend, so this cuts an
        // in-progress attack swing short instead of waiting for it to finish.
        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator != null) animator.Play(DefendStateName, 0, 0f);

        // The shield's own duration matches the parry window exactly, so it's up for
        // the whole attempt regardless of whether it lands.
        if (parrySuccessVfx != null) parrySuccessVfx.Show();

        yield return new WaitForSeconds(ParryWindowDuration);

        // Only reclaim the animator back to idle if nothing else (a successful parry's
        // riposte, a fresh attack, death, victory) has already taken it over during the window.
        if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(DefendStateName))
        {
            animator.Play(IdleStateName, 0, 0f);
        }

        // Still open means nothing consumed it during the window - a mistimed attempt.
        if (parryWindowOpen)
        {
            parryWindowOpen = false;
            StartCooldown();
        }
    }

    // Called by an elite/boss attack the instant its telegraph finishes charging.
public bool TryConsumeParry()
    {
        if (!parryWindowOpen) return false;

        parryWindowOpen = false;

        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator != null) StartCoroutine(PlayRiposteAnimation(animator));

        if (teleportVfx != null) StartCoroutine(PlayTeleportVfx());

        if (player != null && currentDuelist != null) player.Attack(currentDuelist);

        return true;
    }

    private IEnumerator PlayRiposteAnimation(Animator animator)
    {
        // The attack tick is otherwise free to fire mid-riposte, and its Attack trigger
        // would blend a normal swing over the counter still playing - the two poses running
        // at once. Held until the counter finishes, so the next normal hit follows it
        // rather than sharing it.
        if (combatLoop != null) combatLoop.RiposteInProgress = true;

        animator.Play(RiposteStateName, 0, 0f);
        yield return new WaitForSeconds(RiposteAnimDuration);

        if (animator.GetCurrentAnimatorStateInfo(0).IsName(RiposteStateName))
        {
            animator.Play(IdleStateName, 0, 0f);
        }

        if (combatLoop != null) combatLoop.RiposteInProgress = false;
    }

    private IEnumerator PlayTeleportVfx()
    {
        teleportVfx.Play(true);
        yield return new WaitForSeconds(TeleportVfxDuration);
        teleportVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void StartCooldown()
    {
        onCooldown = true;
        UpdateButtonInteractable();

        if (cooldownRoutine != null) StopCoroutine(cooldownRoutine);
        cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        if (cooldownOverlay != null) cooldownOverlay.SetActive(true);

        for (int remaining = CooldownSeconds; remaining >= 1; remaining--)
        {
            if (cooldownText != null) cooldownText.text = remaining.ToString();
            yield return new WaitForSeconds(1f);
        }

        onCooldown = false;
        if (cooldownOverlay != null) cooldownOverlay.SetActive(false);
        UpdateButtonInteractable();
        cooldownRoutine = null;
    }

    private void UpdateButtonInteractable()
    {
        if (parryButton != null) parryButton.interactable = duelActive && !onCooldown;
    }
}
