using System;
using UnityEngine;

// 드랍 종류별 비주얼 애셋. 값과 조회를 같이 둬서 종류 추가 시 한 곳만 고친다.
[CreateAssetMenu(fileName = "DropVisualTable", menuName = "Retima/Drop Visual Table")]
public class DropVisualTableSO : ScriptableObject
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

    [Header("StatPotion")]
    public Entry atk;
    public Entry hp;

    [Header("Equipment")]
    public Entry sword;
    public Entry shield;

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
