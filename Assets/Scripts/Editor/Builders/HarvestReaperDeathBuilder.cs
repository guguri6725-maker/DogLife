#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace DogLife.EditorTools
{
    // Animation authored in ArtSource/HarvestReaper/HarvestReaper_Death.blend.
    public static class HarvestReaperDeathBuilder
    {
        public static string Build()
        {
            const string model="Assets/Models/Monster/Autumn/HarvestReaper/Death.fbx";
            const string clipPath="Assets/Animations/Monster/Autumn/HarvestReaper/Death.anim";
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.SaveAndReimport();
            var source=AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,clipPath);}
            EditorUtility.CopySerialized(source,clip);clip.name="HarvestReaper_Death";
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Monster/Autumn/HarvestReaper/Boss.prefab");
            try
            {
                var animator=root.GetComponentInChildren<Animator>();var ctrl=(AnimatorController)animator.runtimeAnimatorController;
                var sm=ctrl.layers[0].stateMachine;var state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Death")??sm.AddState("Death");
                state.motion=clip;state.speed=1;state.speedParameterActive=false;
                var death=root.GetComponent<HarvestReaperDeath>()??root.AddComponent<HarvestReaperDeath>();var so=new SerializedObject(death);
                so.FindProperty("visual").objectReferenceValue=animator;so.FindProperty("deathClip").objectReferenceValue=clip;
                so.FindProperty("deathVisual").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(model);so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Monster/Autumn/HarvestReaper/Boss.prefab");AssetDatabase.SaveAssets();
                return "Blender death imported: "+clip.length+" seconds";
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
#endif
