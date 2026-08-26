using UnityEditor;
using UnityEngine;

// The VARCO-generated equipment FBXs declare a 0.01 file scale factor. Honouring it shrinks
// the geometry to a hundredth of its authored size - the sword's own mesh is ~1 unit tall, but
// imported with file scale it measures ~1cm, which next to a ~2 unit player renders as nothing
// at all. There is no import error; the drop simply appears to have no model.
//
// Ignoring the file factor keeps the meshes at their authored ~1 unit size, and pinning it here
// makes that reimport-proof instead of depending on the importer's inferred defaults.
public class EquipmentModelPostprocessor : AssetPostprocessor
{
    private const string TargetFolder = "Assets/Models/Equipment/";

    // 1 = take the authored geometry as-is, which measures ~1 unit and is the size the pickup's
    // own visualBaseScale is tuned against.
    private const float EquipmentScaleFactor = 1f;

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(TargetFolder)) return;

        var importer = (ModelImporter)assetImporter;
        importer.useFileScale = false;
        importer.globalScale = EquipmentScaleFactor;
    }
}
