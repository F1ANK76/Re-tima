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

    // 클립을 이름으로 못 찾았을 때만 쓰는 대체값 - Attack04_SwordAndShiled의 클립 길이(FBX
    // 임포트 데이터 기준 30fps에서 0-16 프레임)와 일치. 정상 경로에서는 아래 PlayRiposteAnimation이
    // 클립에서 실제 길이를 읽는다.
    private const float RiposteClipFallbackLength = 16f / 30f;
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

    // StageManager는 정상적인 죽음 처리 없이 현재 결투 상대를 파괴하므로, 여기서도
    // 결투 상태를 명시적으로 정리해줘야 한다.
    private void HandlePlayerDied()
    {
        parryTarget = null;

        // 리포스트 도중 죽으면 안 그래도 공격 틱이 억제된 채로 리스폰된 스테이지까지
        // 넘어가버리는데, 그걸 풀어줄 대상이 아무것도 남아있지 않게 된다.
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

    // 엘리트/보스의 공격이 텔레그래프 충전을 마치는 순간 호출된다.
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
        // 없으면 공격 틱이 리포스트 도중에도 자유롭게 발동하고, 그 Attack 트리거가 아직 재생 중인
        // 카운터 위에 일반 스윙을 블렌딩해 두 포즈가 동시에 재생된다. 카운터가 끝날 때까지 붙잡아
        // 다음 일반 타격이 겹치지 않고 그 뒤를 잇게 한다.
        combatLoop.RiposteInProgress = true;

        animator.Play(PlayerAnimStates.Riposte, 0, 0f);

        // 이 컨트롤러(SwordAndShieldStance)는 스테이트 이름과 그 안의 클립 이름이 같아서 상수
        // 하나로 둘 다 가리킨다 - Monster가 stateName과 clipName을 따로 들고 있는 건 몬스터
        // 컨트롤러에서는 둘이 다르기 때문이지, 규칙이 달라서가 아니다.
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
