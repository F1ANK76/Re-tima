using UnityEngine;

// 필드에 떨어지는 모든 드롭 아이템. 종류(kind)만 다르고 동작은 완전히 같아서 클래스 하나로
// 합쳐져 있다 - 스탯 포션(스테이지 1~)과 장비(스테이지 2~)가 동일한 세 박자를 쓴다:
//   1. 몬스터를 지나쳐 던져진 아이템이 바닥에 튕기며 안착하는 동안 플레이어는 idle로 멈춘다.
//   2. 안착하는 순간 플레이어를 다시 움직임 상태로 풀어준다.
//   3. 아이템이 지면 높이에서 남은 거리를 좁혀 접촉 시 효과를 지급한다.
// 플레이어는 실제로는 어디로도 걸어가지 않으므로(CombatLoop의 "달리기"는 제자리 애니메이션,
// 이동감은 GroundScroller/BackdropScroller가 만든다) 3번 박자는 대신 아이템을 움직인다:
// 몬스터가 걷는 것과 정확히 같은 속도로 바닥에 붙어 등속으로 미끄러져 오는데, 이는 달리는
// 플레이어를 향해 세계가 다가오는 것과 같다. 유도/자석 끌림이 아니다 - 가속도 부양도 없어야
// 아이템이 날아오는 게 아니라 플레이어가 그 위를 달려 지나가는 것으로 읽힌다.
//
// 종류별로 다른 부분은 딱 두 군데(SpawnVisual의 메시 선택, ApplyEffect의 효과 지급)뿐이라
// 상속으로 나누는 대신 kind로 분기한다. 프리팹은 종류마다 하나씩 있고(StatPotionPickup.prefab,
// EquipmentDropPickup.prefab) 각자 자기 kind와 자기 비주얼 슬롯만 채워둔다 - 반대편 종류의
// 슬롯은 그 프리팹에서 그냥 비어있다.
public partial class DropPickup : MonoBehaviour
{
    public enum Kind
    {
        StatPotion,
        Equipment
    }

    [Header("Kind")]
    // 프리팹마다 고정이다. 이 값이 아래 비주얼 슬롯 중 어느 쪽을 쓸지, 그리고 접촉 시
    // 무엇을 지급할지를 함께 결정한다.
    [SerializeField] private Kind kind = Kind.StatPotion;

    [Header("Toss + bounce (beat 1)")]
    // 던져지고 안착하기까지 전체 과정, 시작부터 정지까지.
    [SerializeField] private float landDuration = 0.75f;
    // 사망 지점을 지나 플레이어 반대 방향으로 얼마나 멀리 던져지는지 - 몬스터는
    // 그 둘 사이에서 죽으므로, 이 값이 아이템을 몬스터 "뒤편"에 놓이게 만드는 요인이다.
    [SerializeField] private float tossDistance = 1.4f;
    // 첫 포물선 궤적의 정점. 이후의 모든 재반동은 이 값의 일부다(HopHeights 참고).
    [SerializeField] private float tossHeight = 1.1f;

    [Header("Run-over (beats 2-3)")]
    // 2번 박자: 안착한 아이템이 이만큼 정지해 있는다. GroundScroller/BackdropScroller는 최고
    // 속도로 곧바로 스냅되지 않고 약 0.4초에 걸쳐 스크롤 속도를 이징하므로, 풀려난 플레이어가
    // 속도를 붙이는 동안 아이템도 멈춰있어야 둘이 같은 동작으로 읽힌다.
    [SerializeField] private float settleHoldDuration = 0.2f;
    // 발밑으로 간주할 거리. XZ 평면으로만 비교한다 - 아이템은 바닥에 놓여있고 플레이어의
    // transform은 캡슐 중심이라, 진짜 3D 거리로는 이렇게 작은 값까지 절대 내려가지 않는다.
    [SerializeField] private float pickupRadius = 0.45f;

    // 드롭 매니저가 MonsterSpawner.ApproachSpeed로 설정한다 - 몬스터가 걸어 들어오는
    // 것과 같은 속도여야 한다. 다르면 플레이어가 두 가지 속도로 달리는 것처럼 보인다.
    private float approachSpeed = 5f;

    [Header("Visual (shared)")]
    [SerializeField] private float visualBaseScale = 0.4f;

    [Header("Visual - StatPotion")]
    // ATK는 RedVial, HP는 GreenVial로 드롭된다. 물약 색은 등급 틴트/발광 없이 그대로 둬야
    // 등급과 무관하게 ATK인지 HP인지 한눈에 구분된다 - 희귀도는 아래 아우라가 전담한다.
    [SerializeField] private GameObject atkVisualPrefab;
    [SerializeField] private GameObject hpVisualPrefab;

    [Header("Visual - Equipment")]
    // 타입별로 메시 하나씩. 희귀도는 등급별로 다른 모델이 아니라 아우라/반짝임/크기 램프로만
    // 표현된다(물약과 동일한 규칙).
    [SerializeField] private GameObject swordVisualPrefab;
    [SerializeField] private GameObject shieldVisualPrefab;
    // 임포트로 딸려온 재질이 무엇이든 그 위에 덮어씌운다 - 생성된 FBX는 PBR 텍스처를 외부
    // 사이드카 경로(model.fbm 폴더)로 참조하는데 다운로드한 .fbx 하나만으로는 그게 없어,
    // 텍스처 없는 재질로 임포트되어 화면에 보이지 않게 렌더링된다. 여기서 덮어씌우면 FBX
    // 임포트가 어떻게 해석되든 재질 설정이 그와 무관하게 유지된다.
    [SerializeField] private Material swordMaterial;
    [SerializeField] private Material shieldMaterial;
    // 방패 메시는 XZ 평면에 눕혀진 원반(면이 위를 향함)으로 제작되어, 그대로 떨어뜨리면
    // 바닥에 놓인 그릇처럼 보인다. 세우는 회전은 애셋별 프레이밍이라 드롭 로직에 안 박는다.
    [SerializeField] private Vector3 swordVisualEuler = Vector3.zero;
    [SerializeField] private Vector3 shieldVisualEuler = new Vector3(90f, 0f, 0f);

    [Header("Grade aura")]
    // 세 아우라 레이어 모두 GradeVisuals.GetAuraStrength(Normal 0.2 ~ Legendary 1)를 이
    // 상한값들에 곱해 구동되므로, 등급이 크기만이 아니라 광채의 깊이로도 표현된다. 크기는
    // 이 램프에서 의도적으로 뺐다 - 메시가 이미 등급에 따라 커지므로(GetPotionScale)
    // 헤일로까지 함께 키우면 중복이다.
    [SerializeField] private float auraSize = 1.5f;
    // 가산 헤일로 밝기. 등급 색상에 곱해지므로, Normal 드롭은 흐릿한 흰색 번짐으로,
    // Legendary는 짙은 초록빛 블룸으로 보인다.
    [SerializeField] private float auraBrightnessMax = 1.7f;
    // 아이템은 주변 바닥에도 색이 있는 빛을 던진다 - 이것이 뒤에 데칼을 붙여놓은 게
    // 아니라 실제로 아우라를 *내뿜는* 것처럼 보이게 만드는 요소다.
    [SerializeField] private float auraLightIntensityMax = 3f;
    [SerializeField] private float auraLightRange = 2.6f;

    [Header("Twinkle sparkles")]
    // 아이템 주위를 도는 별빛들. 위상 오프셋을 서로 어긋나게 줘서 항상 무언가는 빛을 반사하는
    // 것처럼 보인다 - 헤일로 하나의 맥동이 "빛나는" 느낌이라면 이건 "반짝반짝"이다.
    // 동기화 금지: 별 네 개가 동시에 번쩍이면 반짝임이 아니라 깜빡이는 조명 하나로 보인다.
    [SerializeField] private int sparkleCount = 4;
    // 루트 로컬 단위이므로, 아이템 자체의 등급 스케일에 맞춰 고리도 함께 넓어진다.
    [SerializeField] private float sparkleOrbitRadius = 0.5f;
    [SerializeField] private float sparkleSize = 0.6f;
    // 가산 별들은 이 크기에서 반짝임으로 인지되려면 부드러운 헤일로보다 더 강한
    // 임팩트가 필요하다.
    [SerializeField] private float sparkleBrightnessMax = 2.4f;
    // 별 하나당 초당 완전한 깜빡임 사이클 수.
    [SerializeField] private float sparkleBlinkSpeed = 1.1f;
    // 고리 전체가 천천히 회전하여, 반짝임들이 화면상 고정된 위치에 박혀있지 않게 한다.
    [SerializeField] private float sparkleOrbitSpeed = 35f;

    // 연속된 도약: 첫 던지기, 그 다음 점점 줄어드는 재반동들. 각 도약은 정확히 지면 높이에서
    // 시작해 지면 높이로 돌아오는 사인 곡선이라, 바닥에서 뚝뚝 끊기지 않고 이어지는 접촉으로
    // 보인다. HopDurations의 합은 1이다.
    private static readonly float[] HopHeights = { 1f, 0.34f, 0.13f, 0.05f };
    private static readonly float[] HopDurations = { 0.42f, 0.26f, 0.18f, 0.14f };

    private Renderer[] renderers;
    // SpawnAura가 붙이는 아우라/반짝임 쿼드가 아니라 메시 자체의 렌더러만 - 모든 등급/메시에
    // 맞는 고정 카메라 거리를 추측하는 대신, EquipmentPreviewRig가 아이템의 실제 실루엣에
    // 맞춰 UI 아이콘을 프레이밍하도록 노출한다.
    public Renderer[] VisualRenderers => renderers;

    private StatGrade grade;
    private Transform player;
    private CombatLoop combatLoop;

    // kind == StatPotion일 때만 의미가 있다.
    private StatType statType;
    private float amount;

    // kind == Equipment일 때만 의미가 있다.
    private EquipmentType equipType;
    private EquipmentDropManager dropManager;

    // PushIdleHold가 PopIdleHold로 짝이 맞춰졌는지 추적한다 - HandlePlayerDied가
    // TossThenRunOver를 세 박자 중 어디서든(자신의 PopIdleHold에 도달하기도 전에도)
    // StopAllCoroutines로 끊을 수 있어서, 이게 없으면 던지는 중 죽었을 때 CombatLoop의
    // idle hold가 영구히 켜진 채 남아 리스폰 후에도 달릴 수 없게 된다.
    private bool idleHoldActive;
    private Vector3 restScale;
    // restScale에서 피벗부터 메시 바닥까지의 거리, 추측이 아니라 실측값이다 - 등급이 높을수록
    // 메시가 피벗 중심으로 양방향 커지므로 고정된 상승값을 쓰면 큰 아이템이 그만큼 바닥 아래로
    // 파묻힌다. 인스턴스마다 직접 재면 등급별 수동 상수 없이 모든 크기가 바닥에 딱 맞는다.
    private float restBottomOffset;
    // 인스턴스마다 코드로 생성된다(등급별로 색조가 다르므로), 이 오브젝트가 사라질
    // 때 다른 누구도 대신 수거해주지 않는다 - OnDestroy 참고.
    private Material auraMaterial;
    // 이 아이템의 모든 반짝임이 공유하는 재질 하나 - auraMaterial과 같은 이유로,
    // 코드로 생성되어 다른 누구도 대신 수거해주지 않는다(OnDestroy 참고).
    private Material sparkleMaterial;
    // 등급 색을 입힌 공용 sword/shield 재질의 인스턴스별 복사본 - 활성화된 모든 픽업이
    // 같은 공용 애셋을 참조하므로 원본 애셋 자체는 절대 건드리지 않아야 한다.
    private Material visualMaterialInstance;

    // StatDropManager가 스폰할 때 호출한다.
    public void InitializeStatPotion(StatType statType, StatGrade grade, float amount, Transform player, CombatLoop combatLoop, float approachSpeed)
    {
        kind = Kind.StatPotion;
        this.statType = statType;
        this.amount = amount;

        BeginDropSequence(grade, player, combatLoop, approachSpeed);
    }

    // EquipmentDropManager가 스폰할 때(그리고 EquipmentPreviewRig가 UI 아이콘용으로) 호출한다.
    public void InitializeEquipment(EquipmentType equipType, StatGrade grade, Transform player, CombatLoop combatLoop, float approachSpeed, EquipmentDropManager dropManager)
    {
        kind = Kind.Equipment;
        this.equipType = equipType;
        this.dropManager = dropManager;

        BeginDropSequence(grade, player, combatLoop, approachSpeed);
    }

    private void BeginDropSequence(StatGrade grade, Transform player, CombatLoop combatLoop, float approachSpeed)
    {
        this.grade = grade;
        this.player = player;
        this.combatLoop = combatLoop;
        if (approachSpeed > 0.01f) this.approachSpeed = approachSpeed;

        SpawnVisual();
        // 프리팹에 제작된 그대로의 스케일이 Normal 크기다 - 더 희귀한 등급은 단지 색만
        // 다른 게 아니라 그 위에 눈에 띄게 더 큰 아이템으로 보인다.
        restScale = transform.localScale * GradeVisuals.GetPotionScale(grade);

        transform.localScale = restScale;
        restBottomOffset = ComputeBottomOffset();

        // 반드시 ComputeBottomOffset 이후: 헤일로 쿼드는 아이템보다 훨씬 아래까지 뻗어있어서
        // 그 바운드까지 포함해 재면 아이템이 자기 광채 높이만큼 공중에 떠버린다.
        SpawnAura();

        // 여기서 1번 박자가 시작된다: CombatLoop.Update()가 이 값을 읽어, 아이템이 아직
        // 공중에 있는 동안에는 플레이어를 계속 달리게 두지 않고 idle 상태로 붙잡아둔다.
        if (combatLoop != null)
        {
            combatLoop.PushIdleHold();
            idleHoldActive = true;
        }

        StartCoroutine(TossThenRunOver());
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandlePlayerDied;
    }

    // 몬스터 처치와 플레이어 사망이 같은 타격에서 동시에 날 수 있다 - 이걸 떨어뜨린 몬스터가
    // 플레이어를 죽인 그 타격에 죽는 경우. StageManager의 StopAllCoroutines는 자기 코루틴만
    // 멈추므로 이 픽업의 코루틴은 사망 시퀀스와 무관하게 계속 돌아, 아이템이 이미 죽은
    // 플레이어에게 미끄러져 들어가고 효과까지 지급했다. 여기서 멈춰 세워야 사망 시퀀스가
    // 제대로 보인다.
    private void HandlePlayerDied()
    {
        StopAllCoroutines();

        if (idleHoldActive)
        {
            if (combatLoop != null) combatLoop.PopIdleHold();
            idleHoldActive = false;
        }
    }


    // 접촉 시 실제 효과 지급. 물약은 플레이어 스탯을 직접 올리고, 장비는 장착 판정이 있는
    // EquipmentDropManager에 위임한다.
    private void ApplyEffect()
    {
        if (kind == Kind.StatPotion)
        {
            PlayerCharacter pc = player.GetComponent<PlayerCharacter>();

            if (statType == StatType.Attack) pc.IncreaseAttack(amount);
            else pc.IncreaseMaxHp(amount);
            
            GameEvents.RaiseStatDropGained(grade, statType, amount);
        }
        else
        {
            if (dropManager != null) dropManager.CompleteDrop(equipType, grade);
        }
    }


}
