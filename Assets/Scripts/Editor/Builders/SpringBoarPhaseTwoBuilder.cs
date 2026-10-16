using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
    public static class SpringBoarPhaseTwoBuilder
    {
        const string Art="Assets/Animations/Monster/Spring/SpringBoar/";
        public static string Build()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Art+"SpringBoar.controller");
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Monster/Spring/SpringBoar/Boss.prefab");
            try
            {
                var phase=new SerializedObject(prefab.GetComponent<SpringBoarPhaseTransition>());
                var skeleton=(Animator)phase.FindProperty("skeletonAnimator").objectReferenceValue;
                var clone=Object.Instantiate(skeleton.gameObject);clone.hideFlags=HideFlags.HideAndDontSave;clone.SetActive(true);
                try
                {
                    var bones=clone.GetComponentsInChildren<Transform>().Where(t=>t!=clone.transform).ToArray();
                    string[] names={"P2SweepReady","P2Sweep","P2Brake","P2BloomReady","P2Bloom","P2Storm","P2Death"};
                    string[] sources={"TuskReady","Tusk","Recovery","SlamReady","Roar","Roar","Idle"};
                    var head=bones.First(t=>t.name=="Head");var chest=bones.First(t=>t.name=="Chest");
                    for(int n=0;n<names.Length;n++)
                    {
                        var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Art+sources[n]+".anim");
                        var curves=new AnimationCurve[bones.Length,7];for(int b=0;b<bones.Length;b++)for(int k=0;k<7;k++)curves[b,k]=new AnimationCurve();
                        for(int frame=0;frame<=40;frame++)
                        {
                            float t=frame/40f;source.SampleAnimation(clone,(n==6?0:t)*source.length);
                            float yaw=n==0?-50*Mathf.SmoothStep(0,1,t):n==1?(t<.7f?Mathf.Lerp(-50,65,Mathf.SmoothStep(0,1,t/.7f)):Mathf.Lerp(65,0,(t-.7f)/.3f)):0;
                            if(n<2){chest.rotation=Quaternion.AngleAxis(yaw*.25f,Vector3.up)*chest.rotation;head.rotation=Quaternion.AngleAxis(yaw*.75f,Vector3.up)*head.rotation;}
                            if(n==2)chest.rotation=Quaternion.AngleAxis(Mathf.Sin(t*Mathf.PI)*8,clone.transform.right)*chest.rotation;
                            if(n>=3&&n<6){float pulse=Mathf.Sin(t*Mathf.PI)*(n==3?8:14);chest.rotation=Quaternion.AngleAxis(-pulse,clone.transform.right)*chest.rotation;head.rotation=Quaternion.AngleAxis(Mathf.Sin(t*Mathf.PI*2)*6,clone.transform.up)*head.rotation;}
                            if(n==6)
                            {
                                var hips=bones.First(b=>b.name=="Hips");
                                float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.72f,t));
                                float settle=Mathf.Sin(Mathf.InverseLerp(.72f,1,t)*Mathf.PI)*2*(1-t);
                                head.rotation=Quaternion.AngleAxis(22*Mathf.SmoothStep(0,1,t),clone.transform.right)*head.rotation;
                                foreach(var leg in bones.Where(b=>b.name.EndsWith("Upper")))leg.rotation=Quaternion.AngleAxis(28*fall,clone.transform.right)*leg.rotation;
                                hips.rotation=Quaternion.AngleAxis(-82*fall+settle,clone.transform.forward)*hips.rotation;
                                var renderer=clone.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();renderer.BakeMesh(baked);
                                float min=float.MaxValue;foreach(var v in baked.vertices)min=Mathf.Min(min,(renderer.transform.position+renderer.transform.rotation*v).y);
                                hips.position+=Vector3.up*(clone.transform.position.y+.035f-min)*Mathf.Clamp01(t/.12f);Object.DestroyImmediate(baked);
                            }
                            for(int b=0;b<bones.Length;b++)
                            {
                                var p=bones[b].localPosition;var q=bones[b].localRotation;float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};
                                for(int k=0;k<7;k++)curves[b,k].AddKey(new Keyframe(t,values[k]));
                            }
                        }
                        string path=Art+names[n]+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.ClearCurves();clip.frameRate=40;clip.name="SpringBoar_"+names[n];
                        string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                        for(int b=0;b<bones.Length;b++)for(int k=0;k<7;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[b],clone.transform),typeof(Transform),properties[k]),curves[b,k]);
                        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
                        var machine=controller.layers[0].stateMachine;var state=machine.states.FirstOrDefault(s=>s.state.name==names[n]).state;if(!state)state=machine.AddState(names[n]);
                        state.motion=clip;state.speed=1;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;
                    }
                }
                finally{Object.DestroyImmediate(clone);}
                var two=prefab.GetComponent<SpringBoarPhaseTwo>();if(!two)two=prefab.AddComponent<SpringBoarPhaseTwo>();
                if(!prefab.GetComponent<SpringBoarDeath>())prefab.AddComponent<SpringBoarDeath>();
                var settingsTwo=new SerializedObject(two);settingsTwo.FindProperty("animator").objectReferenceValue=skeleton;settingsTwo.FindProperty("effects").objectReferenceValue=prefab.GetComponentInChildren<SpringBoarVfx>(true);settingsTwo.ApplyModifiedPropertiesWithoutUndo();
                prefab.GetComponentInChildren<SpringBoarVfx>(true).phaseTwo=two;
                PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Monster/Spring/SpringBoar/Boss.prefab");EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            SpringBoarDeathBuilder.Build();
            return "Phase-two animations, persistent death pose and AI/VFX references saved";
        }
    }
}
