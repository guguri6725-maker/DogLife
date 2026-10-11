using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
 public static class KrakenNaturalRigBuilder
 {
  public static string Build()
  {
   const string modelPath="Assets/Art/Kraken/Models/Kraken_Tentacle_Natural.fbx";
   var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;importer.SaveAndReimport();
   const string controllerPath="Assets/Art/Kraken/Animations/Tentacle_Natural.controller";
   var ctrl=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);if(!ctrl)ctrl=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
   var sm=ctrl.layers[0].stateMachine;foreach(var s in sm.states)sm.RemoveState(s.state);
   foreach(var source in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")))
   {
    string name=source.name.Substring(source.name.LastIndexOf('_')+1),path="Assets/Art/Kraken/Animations/NaturalTentacle_"+name+".anim";
    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}EditorUtility.CopySerialized(source,clip);clip.name="NaturalTentacle_"+name;
    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=name!="Settle";AnimationUtility.SetAnimationClipSettings(clip,settings);
    var state=sm.AddState(name);state.motion=clip;if(name=="Idle")sm.defaultState=state;
   }
   var preview=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
   var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SummerBossAssembly/Materials/AttackTentacle_Coastal.mat");
   foreach(var r in preview.GetComponentsInChildren<Renderer>())r.sharedMaterial=mat;
   var animator=preview.GetComponent<Animator>();if(!animator)animator=preview.AddComponent<Animator>();animator.runtimeAnimatorController=ctrl;animator.applyRootMotion=false;
   PrefabUtility.SaveAsPrefabAsset(preview,"Assets/Prefabs/monster/KrakenTentacle_NaturalRig.prefab");Object.DestroyImmediate(preview);
   AssetDatabase.SaveAssets();return "18-bone natural rig and Idle/Flow/Settle preview prefab saved";
  }
 }
}

