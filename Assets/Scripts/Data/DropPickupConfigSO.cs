using System;
using UnityEngine;

// 드랍 픽업의 종류별 애셋 + 전역 튜닝값. 값과 조회를 같이 둬서 종류 추가 시 한 곳만 고친다.
[CreateAssetMenu(fileName = "DropPickupConfig", menuName = "Retima/Drop Pickup Config")]
public class DropPickupConfigSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameObject prefab;
        // 비었으면 프리팹 원본 재질을 쓴다
        public Material material;
        // 애셋별 값 -> 눕혀 임포트된 메시를 세운다
        public Vector3 euler;
        // 임포트 크기가 애셋마다 달라 여기서 맞춘다. 등급 배율은 따로 곱해진다
        public float scale = 1f;
    }

    [Header("Visual - StatPotion")]
    public Entry atk;
    public Entry hp;

    [Header("Visual - Equipment")]
    public Entry sword;
    public Entry shield;

    [Header("Toss + bounce (beat 1)")]
    // 던지기~안착 전체 시간
    public float landDuration = 0.9f;
    // 플레이어 반대쪽으로 던지는 거리 -> 아이템이 몬스터 뒤편에 놓인다
    public float tossDistance = 3f;
    // 첫 포물선 정점. 이후 반동은 HopHeights 배율
    public float tossHeight = 2.2f;

    [Header("Run-over (beats 2-3)")]
    // 안착 후 정지 시간 -> 배경 스크롤이 속도 붙는 동안 아이템도 멈춰 있어야 한다
    public float settleHoldDuration = 0.2f;
    // 획득 판정 반경. XZ 평면 -> 플레이어 transform이 캡슐 중심이라 3D 거리로는 안 닿는다
    public float pickupRadius = 0.45f;

    [Header("Grade aura")]
    public float auraSize = 1.5f;
    // 가산 헤일로 밝기 (등급 색에 곱해짐)
    public float auraBrightnessMax = 1.7f;
    // 바닥에 깔리는 빛 자국. 아이템이 지면에서 떠 보이지 않게 하는 요인
    public float groundGlowSize = 2.6f;
    public float groundGlowBrightness = 1.1f;

    [Header("Twinkle sparkles")]
    // 위상을 서로 어긋나게 줄 것 -> 동시에 번쩍이면 반짝임이 아니라 깜빡이는 조명 하나가 된다
    public int sparkleCount = 4;
    // 로컬 단위 -> 등급 스케일에 맞춰 고리도 넓어진다
    public float sparkleOrbitRadius = 0.5f;
    public float sparkleSize = 0.6f;
    // 헤일로보다 강해야 반짝임으로 인지된다
    public float sparkleBrightnessMax = 2.4f;
    // 별 하나당 초당 깜빡임 횟수
    public float sparkleBlinkSpeed = 1.1f;
    // 고리 전체 회전 -> 반짝임이 한자리에 박히지 않게
    public float sparkleOrbitSpeed = 35f;

    public Entry Get(DropPickup.Kind kind, StatType statType, EquipmentType equipType)
    {
        if (kind == DropPickup.Kind.StatPotion)
        {
            switch (statType)
            {
                case StatType.Attack: return atk;
                default: return hp;
            }
        }

        switch (equipType)
        {
            case EquipmentType.Sword: return sword;
            default: return shield;
        }
    }
}
