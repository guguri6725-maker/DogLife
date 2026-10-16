using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
    public static class SpringBoarDeathBuilder
    {
        const string Art="Assets/Animations/Monster/Spring/SpringBoar/";
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        public static string Build()
        {
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Monster/Spring/SpringBoar/Boss.prefab");
            try
            {
                var so=new SerializedObject(prefab.GetComponent<SpringBoarPhaseTransition>());
                var animator=(Animator)so.FindProperty("skeletonAnimator").objectReferenceValue;
                var clone=Object.Instantiate(animator.gameObject);clone.hideFlags=HideFlags.HideAndDontSave;clone.SetActive(true);
                try
                {
                    var bones=clone.GetComponentsInChildren<Transform>().Where(t=>t!=clone.transform).ToArray();
                    var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Art+"Idle.anim");
                    var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Art+"SpringBoar.controller");
                    var hips=bones.First(t=>t.name=="Hips");var chest=bones.First(t=>t.name=="Chest");var neck=bones.First(t=>t.name=="Neck");var head=bones.First(t=>t.name=="Head");var jaw=bones.First(t=>t.name=="Jaw");
                    foreach(int side in new[]{-1,1})
                    {
                        var curves=new AnimationCurve[bones.Length,7];for(int b=0;b<bones.Length;b++)for(int k=0;k<7;k++)curves[b,k]=new AnimationCurve();
                        for(int frame=0;frame<=120;frame++)
                        {
                            float t=frame/120f;source.SampleAnimation(clone,0);
                            string[] prefixes={"LFront","RFront","LRear","RRear"};
                            var feet=prefixes.Select(p=>bones.First(b=>b.name==p+"Foot")).ToArray();
                            var footPositions=feet.Select(f=>f.position).ToArray();var footRotations=feet.Select(f=>f.rotation).ToArray();
                            float height=hips.position.y-clone.transform.position.y;
                            // Author a downward trajectory first, then bend legs to their planted feet.
                            // No mesh-minimum correction may lift the body back into a standing pose.
                            float drop=.10f*Ease(.04f,.16f,t)+.72f*Ease(.18f,.40f,t)+.18f*Ease(.43f,.58f,t);
                            hips.position-=Vector3.up*height*.42f*drop;
                            chest.position-=Vector3.up*height*.03f*Ease(.14f,.32f,t);
                            chest.rotation=Quaternion.AngleAxis(4*Ease(.12f,.35f,t),clone.transform.right)*chest.rotation;
                            neck.rotation=Quaternion.AngleAxis(-12*drop,clone.transform.right)*neck.rotation;
                            head.rotation=Quaternion.AngleAxis(3*Ease(.62f,.82f,t),clone.transform.right)*head.rotation;
                            var tail=bones.FirstOrDefault(b=>b.name=="Tail");if(tail)tail.rotation=Quaternion.AngleAxis(35*drop,clone.transform.right)*tail.rotation;
                            jaw.rotation=Quaternion.AngleAxis(4*Ease(.08f,.25f,t)*(1-.7f*Ease(.75f,1,t)),clone.transform.right)*jaw.rotation;
                            for(int leg=0;leg<prefixes.Length;leg++)
                            {
                                string prefix=prefixes[leg];bool front=leg<2;
                                var upper=bones.First(b=>b.name==prefix+"Upper");var lower=bones.First(b=>b.name==prefix+"Lower");
                                var foot=feet[leg];var target=footPositions[leg]+clone.transform.right*(leg%2==0?-1:1)*height*.08f*drop;
                                var a=upper.position;var b=lower.position;var c=foot.position;
                                float ab=Vector3.Distance(a,b),bc=Vector3.Distance(b,c);var direction=(target-a).normalized;
                                float d=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(ab-bc)+.001f,ab+bc-.001f);
                                // Preserve the anatomical bend plane instead of twisting knees out sideways.
                                var bend=Vector3.ProjectOnPlane(b-a,direction).normalized;
                                if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(clone.transform.forward*(front?1:-1),direction).normalized;
                                float x=(ab*ab+d*d-bc*bc)/(2*d);var knee=a+direction*x+bend*Mathf.Sqrt(Mathf.Max(0,ab*ab-x*x));
                                upper.rotation=Quaternion.FromToRotation(b-a,knee-a)*upper.rotation;
                                lower.rotation=Quaternion.FromToRotation(foot.position-lower.position,target-lower.position)*lower.rotation;
                                foot.rotation=footRotations[leg];
                            }
                            for(int b=0;b<bones.Length;b++)
                            {
                                var p=bones[b].localPosition;var q=bones[b].localRotation;float[] v={p.x,p.y,p.z,q.x,q.y,q.z,q.w};
                                for(int k=0;k<7;k++)curves[b,k].AddKey(t,v[k]);
                            }
                        }
                        string name=side<0?"P2DeathLeft":"P2DeathRight",path=Art+name+".anim";
                        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.ClearCurves();clip.frameRate=30;clip.name="SpringBoar_"+name;
                        string[] props={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                        for(int b=0;b<bones.Length;b++)for(int k=0;k<7;k++)
                        {for(int key=0;key<curves[b,k].length;key++)curves[b,k].SmoothTangents(key,0);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[b],clone.transform),typeof(Transform),props[k]),curves[b,k]);}
                        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
                        var sm=controller.layers[0].stateMachine;var state=sm.states.FirstOrDefault(s=>s.state.name==name).state;if(!state)state=sm.AddState(name);state.motion=clip;state.speed=1;state.speedParameter="PhaseSpeed";state.speedParameterActive=true;
                    }
                    EditorUtility.SetDirty(controller);
                }
                finally{Object.DestroyImmediate(clone);}
                var death=prefab.GetComponent<SpringBoarDeath>();var settingsDeath=new SerializedObject(death);settingsDeath.FindProperty("deathDuration").floatValue=5.2f;settingsDeath.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefabs/Monster/Spring/SpringBoar/Boss.prefab");AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            return "Leg-collapse death animations baked: chest then pelvis settle onto the ground without side roll";
        }
    }
}
