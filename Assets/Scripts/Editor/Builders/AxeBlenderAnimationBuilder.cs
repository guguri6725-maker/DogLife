#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Newtonsoft.Json.Linq;
namespace DogLife.EditorTools
{
    public static class AxeBlenderAnimationBuilder
    {
        public static string Build()
        {
            var data=JObject.Parse(System.IO.File.ReadAllText("ArtSource/PlayerAxe/animation-bake.json"));
            var reference=JObject.Parse(System.IO.File.ReadAllText("ArtSource/PlayerAxe/rig-reference.json"));
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/DogLight.controller");
            var machine=controller.layers[0].stateMachine;
            var created=new Dictionary<string,AnimationClip>();
            try
            {
                prefab.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var animator=prefab.GetComponent<PlayerDirectionalAnimation>().animator;
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/DogLight_Idle.anim").SampleAnimation(animator.gameObject,0);
                var bones=animator.GetComponentsInChildren<Transform>().GroupBy(b=>b.name).ToDictionary(g=>g.Key,g=>g.First());
                foreach(var entry in data["clips"])
                {
                    string name=(string)entry["name"];
                    var clip=new AnimationClip{name=name,frameRate=60};
                    var curves=new Dictionary<string,AnimationCurve[]>();
                    foreach(var f in entry["frames"])
                    {
                        float time=(float)f["t"];
                        foreach(var sample in f["bones"])
                        {
                            var bone=bones[(string)sample["name"]];var p=sample["p"];var q=sample["q"];
                            bone.SetPositionAndRotation(new Vector3((float)p[0],(float)p[1],(float)p[2]),new Quaternion((float)q[0],(float)q[1],(float)q[2],(float)q[3]));
                            string path=AnimationUtility.CalculateTransformPath(bone,animator.transform);
                            if(!curves.ContainsKey(path))curves[path]=Enumerable.Range(0,7).Select(i=>new AnimationCurve()).ToArray();
                            var c=curves[path];var lp=bone.localPosition;var lq=bone.localRotation;
                            float[] v={lp.x,lp.y,lp.z,lq.x,lq.y,lq.z,lq.w};
                            for(int i=0;i<7;i++)c[i].AddKey(time,v[i]);
                        }
                    }
                    foreach(var pair in curves)for(int i=0;i<7;i++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(pair.Key,typeof(Transform),i<3?"m_LocalPosition."+"xyz"[i]:"m_LocalRotation."+"xyzw"[i-3]),pair.Value[i]);
                    clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=(bool)entry["loop"];AnimationUtility.SetAnimationClipSettings(clip,settings);
                    string pathOut="Assets/Animations/Player/"+(name.StartsWith("Bone")?"DogLight_BasicWeapons_":"DogLight_")+name+".anim";
                    var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(pathOut);
                    if(existing){string backup="ArtSource/PlayerAxe/Backup/"+System.IO.Path.GetFileName(pathOut);System.IO.Directory.CreateDirectory("ArtSource/PlayerAxe/Backup");if(!System.IO.File.Exists(backup))System.IO.File.Copy(pathOut,backup);EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;}
                    else AssetDatabase.CreateAsset(clip,pathOut);
                    created[name]=clip;
                    var state=machine.states.FirstOrDefault(s=>s.state.name==name).state;if(!state)state=machine.AddState(name);state.motion=clip;
                    if(name.StartsWith("AxeWhirlwind")){state.speedParameterActive=false;state.speed=1;foreach(var transition in state.transitions)state.RemoveTransition(transition);}
                }
                var move=AssetDatabase.LoadAssetAtPath<BasicWeaponMoveset>("Assets/Data/WeaponMovesets/Axe.asset");
                move.axeWhirlwindEnter=created["AxeWhirlwind_Enter"];move.axeWhirlwindLoop=created["AxeWhirlwind_Loop"];move.axeWhirlwindExit=created["AxeWhirlwind_Exit"];
                EditorUtility.SetDirty(move);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return "Baked 6 Blender actions; existing attack timings preserved.";
            }finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
    }
}
#endif
