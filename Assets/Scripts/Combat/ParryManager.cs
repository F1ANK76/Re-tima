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
    // 실패(타이밍을 놓친 시도)는 성공보다 더 오래 물린다 - 연속 패링 남발 방지용 최소 텀인
    // 성공 쿨타임과 달리, 실패는 페널티라 더 길다.
    private const int CooldownSeconds = 2;
    private const int SuccessCooldownSeconds = 1;

    // PlayerAnimStates.Riposte 자체의 클립 길이(FBX 임포트 데이터 기준 30fps에서 0-16 프레임)와
    // 일치한다 - WeaponSwing이 Attack01의 임팩트 타이밍에 쓰는 것과 같은 규칙이다.
    private const float RiposteAnimDuration = 16f / 30f;
    // Teleport 프리팹의 가장 긴 서브 시스템 수명과 일치시켜서, 화려한 연출 도중에
    // 끊기지 않고 한 사이클을 온전히 다 재생하도록 한다.
    private const float TeleportVfxDuration = 0.7f;
    // 패링 윈도우(0.5초)보다 살짝 길게 잡아서, 판정이 끝난 뒤에도 방패가 잠깐 남아있다가
    // 사라지도록 하는 의도적인 여유.
    private const float ShieldEffectDuration = 0.8f;
    // 패링 성공의 보상 - 리포스트 타격은 플레이어 공격력의 2배로 들어간다.
    private const float RiposteDamageMultiplier = 2f;

    private bool parryWindowOpen;
    private bool onCooldown;
    private bool duelActive;
    private Monster currentDuelist;
    private Coroutine cooldownRoutine;
    private Coroutine shieldEffectRoutine;

    // 타격 전에 예비 동작(텔레그래프)을 보이는 몬스터만 패링 가능하다 - 일반 몬스터는
    // 아무 조짐 없이 고정된 타이머로 공격하므로 반응할 대상이 애초에 없다.
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

    // StageManager는 정상적인 죽음 처리 없이 현재 결투 상대를 파괴하므로, 여기서도
    // 결투 상태를 명시적으로 정리해줘야 한다.
    private void HandlePlayerDied()
    {
        currentDuelist = null;
        duelActive = false;

        // 리포스트 도중 죽으면 안 그래도 공격 틱이 억제된 채로 리스폰된 스테이지까지
        // 넘어가버리는데, 그걸 풀어줄 대상이 아무것도 남아있지 않게 된다.
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

        // 이미 진행 중이던 일반 스윙은, 아래에서 포즈가 Defend로 전환되고 나면
        // 타격을 적중시키거나 슬래시를 계속 보여줘서는 안 된다.
        if (combatLoop != null) combatLoop.CancelPendingAttack();

        // Animator.Play는 블렌딩 없이 해당 스테이트로 바로 점프하므로, 진행 중인
        // 공격 스윙을 끝까지 기다리지 않고 즉시 끊어버린다.
        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator != null) animator.Play(PlayerAnimStates.Defend, 0, 0f);

        // 위와 같은 이유로 궁극기도 하드컷된다 - 애니메이터는 이미 Defend로 넘어갔는데 궁극기
        // 코루틴이 자기 타이머로 계속 돌아 뒤늦게 데미지/폭발을 따로 터뜨리지 않도록 여기서 함께 취소한다.
        if (ultimateManager != null) ultimateManager.CancelUltimate();

        // 방패 이펙트는 성공 여부와 무관하게 시도하는 순간 바로 뜬다 - 실제 성공/실패
        // 판정과는 무관하다.
        ShowShieldEffect();

        yield return new WaitForSeconds(ParryWindowDuration);

        // 윈도우가 진행되는 동안 다른 무언가(성공한 패링의 리포스트, 새로운 공격, 죽음,
        // 승리)가 이미 애니메이터를 가져가지 않았을 때만 idle로 되돌려 받는다.
        if (animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Defend))
        {
            animator.Play(PlayerAnimStates.Idle, 0, 0f);
        }

        // 여전히 열려 있다는 건 윈도우 동안 아무도 이걸 소비하지 않았다는 뜻 - 타이밍을 놓친 시도다.
        if (parryWindowOpen)
        {
            parryWindowOpen = false;
            StartCooldown(CooldownSeconds);
        }
    }

    // 엘리트/보스의 공격이 텔레그래프 충전을 마치는 순간 호출된다.
    public bool TryConsumeParry()
    {
        if (!parryWindowOpen) return false;

        parryWindowOpen = false;

        Animator animator = weaponSwing != null ? weaponSwing.CharacterAnimator : null;
        if (animator != null) StartCoroutine(PlayRiposteAnimation(animator));

        if (teleportVfx != null) StartCoroutine(PlayTeleportVfx());

        // 리포스트는 일반 타격의 2배 데미지 - 성공적인 패링에 대한 보상.
        if (player != null && currentDuelist != null) player.Attack(currentDuelist, RiposteDamageMultiplier);

        StartCooldown(SuccessCooldownSeconds);

        return true;
    }

    private IEnumerator PlayRiposteAnimation(Animator animator)
    {
        // 없으면 공격 틱이 리포스트 도중에도 자유롭게 발동하고, 그 Attack 트리거가 아직 재생 중인
        // 카운터 위에 일반 스윙을 블렌딩해 두 포즈가 동시에 재생된다. 카운터가 끝날 때까지 붙잡아
        // 다음 일반 타격이 겹치지 않고 그 뒤를 잇게 한다.
        if (combatLoop != null) combatLoop.RiposteInProgress = true;

        animator.Play(PlayerAnimStates.Riposte, 0, 0f);
        yield return new WaitForSeconds(RiposteAnimDuration);

        if (animator.GetCurrentAnimatorStateInfo(0).IsName(PlayerAnimStates.Riposte))
        {
            animator.Play(PlayerAnimStates.Idle, 0, 0f);
        }

        if (combatLoop != null) combatLoop.RiposteInProgress = false;
    }

    private IEnumerator PlayTeleportVfx()
    {
        teleportVfx.Play(true);
        yield return new WaitForSeconds(TeleportVfxDuration);
        teleportVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void StartCooldown(int seconds)
    {
        onCooldown = true;
        UpdateButtonInteractable();

        if (cooldownRoutine != null) StopCoroutine(cooldownRoutine);
        cooldownRoutine = StartCoroutine(CooldownRoutine(seconds));
    }

    private IEnumerator CooldownRoutine(int seconds)
    {
        if (cooldownOverlay != null) cooldownOverlay.SetActive(true);

        for (int remaining = seconds; remaining >= 1; remaining--)
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

    private void ShowShieldEffect()
    {
        if (parrySuccessVfx == null) return;

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
