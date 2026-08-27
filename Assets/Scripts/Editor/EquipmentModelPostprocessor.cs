using UnityEditor;
using UnityEngine;

// VARCO로 생성된 장비 FBX는 file scale factor가 0.01이다. 그대로 반영하면 지오메트리가 제작 당시
// 크기의 1/100로 줄어든다 - sword 메시 자체는 높이 ~1 유닛인데 file scale이 적용되면 ~1cm로 측정되어,
// ~2 유닛인 플레이어 옆에서는 사실상 안 보이게 렌더링된다. 임포트 에러는 없다; 그냥 드롭 아이템에
// 모델이 없는 것처럼 보인다.
//
// file factor를 무시하면 메시가 제작 당시의 ~1 유닛 크기를 유지하며, 여기서 고정해두면 임포터가
// 추론한 기본값에 의존하지 않고 재임포트해도 안전하다.
public class EquipmentModelPostprocessor : AssetPostprocessor
{
    private const string TargetFolder = "Assets/Models/Equipment/";

    // 1 = 제작된 지오메트리를 그대로 사용한다는 의미이며, 이는 ~1 유닛 크기로 pickup 자체의
    // visualBaseScale이 맞춰 조정된 기준 크기다.
    private const float EquipmentScaleFactor = 1f;

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(TargetFolder)) return;

        var importer = (ModelImporter)assetImporter;
        importer.useFileScale = false;
        importer.globalScale = EquipmentScaleFactor;
    }
}
