using UnityEditor;
using UnityEngine;

namespace DogLife.EditorTools
{
    // FBX relative texture paths no longer match the type/season asset folders.
    // Reconnect imported materials without changing their local IDs or prefab overrides.
    public sealed class AutumnModelMaterialImporter : AssetPostprocessor
    {
        void OnPostprocessModel(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material) OnPostprocessMaterial(material);
        }

        void OnPostprocessMaterial(Material material)
        {
            const string models = "Assets/Models/Monster/Autumn/";
            if (!assetPath.StartsWith(models)) return;
            string name = material.name;
            string folder = "Assets/Textures/Monster/Autumn/";
            string colorPath = null, normalPath = null;
            foreach (string monster in new[] { "BoneCrow", "Scarecrow", "LeafHedgehog", "MyceliumSkeleton" })
            {
                if (name.StartsWith(monster + "_Material"))
                {
                    colorPath = folder + monster + "/" + monster + "_BaseColor.png";
                    normalPath = folder + monster + "/" + monster + "_Normal.png";
                    break;
                }
            }
            if (name.StartsWith("BoneCrow_FlightLeaf_"))
                colorPath = folder + "Shared/Autumn_LeafGrain.png";
            else if (name == "Scarecrow_ExtensionWood")
                colorPath = folder + "Scarecrow/Autumn_ExtensionBark.png";
            else if (name.StartsWith("HarvestReaper_Body"))
            {
                colorPath = folder + "HarvestReaper/BaseColor.png";
                normalPath = folder + "HarvestReaper/Normal.png";
            }
            else if (name.StartsWith("HarvestReaper_Scythe"))
            {
                colorPath = folder + "HarvestReaper/Scythe_BaseColor.png";
                normalPath = folder + "HarvestReaper/Scythe_Normal.png";
            }
            if (colorPath == null) return; // Preserve intentionally untextured bone/vein colors.
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            if (!color) { Debug.LogError("Missing autumn model texture: " + colorPath); return; }
            material.mainTexture = color;
            if (normalPath != null)
            {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (!normal) { Debug.LogError("Missing autumn model normal: " + normalPath); return; }
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
        }
    }
}
