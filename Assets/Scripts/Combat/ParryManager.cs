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
    [SerializeField] private GameObject parrySuccessVfx;
    [SerializeField] private ParticleSystem teleportVfx;
    [SerializeField] private CombatLoop combatLoop;
    [SerializeField] private UltimateManager ultimateManager;

    private const float ParryWindowDuration = 0.5f;
    private const int CooldownSeconds = 2;
    private const int SuccessCooldownSeconds = 1;

    private const float RiposteClipFallbackLength = 16f / 30f;
    private const float TeleportVfxDuration = 0.7f;
    private const float ShieldEffectDuration = 0.8f;
    // 패링 성공의 보상 - 리포스트 타격은 플레이어 공격력의 2배로 들어간다.
    private const float RiposteDamageMultiplier = 2f;

    private bool parryWindowOpen;
    private bool onCooldown;
    // null이면 패링 대상이 없다는 뜻 -> 버튼도 이 값으로 켜고 끈다
    private Monster parryTarget;
    private Coroutine shieldEffectRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnMonsterSpawned += HandleMonsterSpawned;
        GameEvents.OnMonsterDied += HandleMonsterDied;
        GameEvents.OnPlayerDied += HandlePlayerDied;

        parryButton.onClick.AddListener(OnParryButtonPressed);

        cooldownOverlay.SetActive(false);
        UpdateButtonInteractable();
    }

    private void OnDisable()
    {
        GameEvents.OnMonsterSpawned -= HandleMonsterSpawned;
        GameEvents.OnMonsterDied -= HandleMonsterDied;
        GameEvents.OnPlayerDied -= HandlePlayerDied;

        parryButton.onClick.RemoveListener(OnParryButtonPressed);
    }

    private void HandlePlayerDied()
    {
        parryTarget = null;

        combatLoop.RiposteInProgress = false;

        UpdateButtonInteractable();
    }

    private void HandleMonsterSpawned(Monster monster)
    {
        // 예비 동작이 있는 놈만 패링 대상 -> 일반 몬스터는 조짐 없이 고정 타이머로 때린다
        if (monster.Type == MonsterType.Boss || monster.Type == MonsterType.Elite)
        {
            parryTarget = monster;
            UpdateButtonInteractable();
        }
    }

    private void HandleMonsterDied(Monster monster)
    {
        if (monster == parryTarget)
        {
            parryTarget = null;
            UpdateButtonInteractable();
        }
    }

    private void OnParryButtonPressed()
    {
        // 패링 진행중일땐 안 눌리게
        if (parryWindowOpen) return;
        StartCoroutine(ParryWindowRoutine());
    }

    private IEnumerator ParryWindowRoutine()
    {
        parryWindowOpen = true;

        // 평타 캔슬
        combatLoop.CancelPendingAttack();

        // 플레이어 가드 애님 재생
        Animator animator = weaponSwing.PlayerAnimator;
        animator.Play(PlayerAnimStates.Defend, 0, 0f);

        // 궁극기 재생중일시 캔슬
        ultimateManager.CancelUltimate();

        // 실드 이펙트 생성
        ShowShieldEffect();

        // 0.5초 패링 시작
        yield return new WaitForSeconds(ParryWindowDuration);

        // 가드 자세 변경
        if (animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Defend))
        {
            animator.Play(PlayerAnimStates.Idle, 0, 0f);
        }

        // 패링 실패
        if (parryWindowOpen)
        {
            parryWindowOpen = false;
            StartCooldown(CooldownSeconds);
        }
    }

    // 패링 성공
    public bool TryConsumeParry()
    {
        if (!parryWindowOpen) return false;

        parryWindowOpen = false;

        StartCoroutine(PlayRiposteAnimation(weaponSwing.PlayerAnimator));

        StartCoroutine(PlayTeleportVfx());

        // 리포스트는 일반 타격의 2배 데미지 - 성공적인 패링에 대한 보상.
        if (parryTarget != null) player.Attack(parryTarget, RiposteDamageMultiplier);

        StartCooldown(SuccessCooldownSeconds);

        return true;
    }

    private IEnumerator PlayRiposteAnimation(Animator animator)
    {
        combatLoop.RiposteInProgress = true;

        animator.Play(PlayerAnimStates.Riposte, 0, 0f);

        yield return new WaitForSeconds(
            AnimClipTiming.ResolveClipLength(animator, PlayerAnimStates.Riposte, RiposteClipFallbackLength));

        if (animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Riposte))
        {
            animator.Play(PlayerAnimStates.Idle, 0, 0f);
        }

        combatLoop.RiposteInProgress = false;
    }

    private IEnumerator PlayTeleportVfx()
    {
        teleportVfx.Play(true);
        yield return new WaitForSeconds(TeleportVfxDuration);
        teleportVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // 쿨다운 중에는 버튼이 꺼져 있어 다시 들어올 수 없다 -> 코루틴이 겹칠 일이 없다
    private void StartCooldown(int seconds)
    {
        onCooldown = true;
        UpdateButtonInteractable();

        StartCoroutine(CooldownRoutine(seconds));
    }

    private IEnumerator CooldownRoutine(int seconds)
    {
        cooldownOverlay.SetActive(true);

        for (int remaining = seconds; remaining >= 1; remaining--)
        {
            cooldownText.text = remaining.ToString();
            yield return new WaitForSeconds(1f);
        }

        onCooldown = false;
        cooldownOverlay.SetActive(false);
        UpdateButtonInteractable();
    }

    // 둘 다 참일 때만 켜진다 -> 하나라도 아니면 꺼진다
    private void UpdateButtonInteractable()
    {
        // 패링 대상이 존재하면서 패링 쿨타임 상태가 아닐 경우 패링 버튼을 누를 수 있음
        if(parryTarget != null && onCooldown == false)
        {
            parryButton.interactable = true;
        }
        else
        {
            parryButton.interactable = false;
        }
    }

    private void ShowShieldEffect()
    {

        parrySuccessVfx.SetActive(true);
        if (shieldEffectRoutine != null) StopCoroutine(shieldEffectRoutine);
        shieldEffectRoutine = StartCoroutine(HideShieldEffectAfterDelay());
    }

    private IEnumerator HideShieldEffectAfterDelay()
    {
        yield return new WaitForSeconds(ShieldEffectDuration);
        parrySuccessVfx.SetActive(false);
        shieldEffectRoutine = null;
    }
}
