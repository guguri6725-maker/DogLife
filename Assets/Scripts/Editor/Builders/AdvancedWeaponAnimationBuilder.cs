using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
    public static class AdvancedWeaponAnimationBuilder
    {
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static void Arm(Transform upper,Transform elbow,Transform hand,Vector3 target,Vector3 pole)
        {
            float a=Vector3.Distance(upper.position,elbow.position),b=Vector3.Distance(elbow.position,hand.position);
            Vector3 d=(target-upper.position).normalized;float length=Mathf.Clamp(Vector3.Distance(target,upper.position),Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 side=Vector3.ProjectOnPlane(pole-upper.position,d).normalized;
            float along=(a*a+length*length-b*b)/(2*length);
            var joint=upper.position+d*along+side*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(elbow.position-upper.position,joint-upper.position)*upper.rotation;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,upper.position+d*length-elbow.position)*elbow.rotation;
        }
        public static string Build()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/DogLight.controller");
            var machine=controller.layers[0].stateMachine;
            var locomotion=machine.states.First(x=>x.state.name=="Locomotion").state;
            var idle=((BlendTree)locomotion.motion).children[0].motion as AnimationClip;
            var upperMachine=controller.layers.First(x=>x.name=="Blade Upper Body").stateMachine;
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
            try
            {
                var animator=root.GetComponent<PlayerDirectionalAnimation>().animator;var model=animator.transform;
                var skeleton=model.Find("skel/Root");var bones=skeleton.GetComponentsInChildren<Transform>().Where(t=>!t.name.Contains("Socket")).ToArray();
                var map=bones.ToDictionary(t=>t.name,t=>t);var hip=map["Hips"];
                foreach(var kind in new[]{"Boomerang","Bow","Axe","Spear"})
                {
                    bool axe=kind=="Axe",spear=kind=="Spear",bow=kind=="Bow";int count=axe||spear?3:1;
                    var profile=AssetDatabase.LoadAssetAtPath<BasicWeaponMoveset>("Assets/Data/WeaponMovesets/"+kind+".asset");
                    profile.attacks=new AnimationClip[count];profile.strikeTimes=new float[count];profile.secondaryStrikeTimes=new float[count];profile.comboLinkNormalized=axe?.9f:.8f;
                    for(int step=0;step<count;step++)
                    {
                        bool upper=!axe&&(!spear||step<2);
                        float duration=bow?.85f:kind=="Boomerang"?.65f:axe?(step==0?.86f:step==1?.98f:1.38f):(step==0?.65f:step==1?.9f:1.5f);
                        float hit=bow?.51f:kind=="Boomerang"?.3f:axe?(step==0?.56f:step==1?.68f:1.1f):step==0?.28f:step==1?.26f:.34f;
                        int frames=Mathf.CeilToInt(duration*60);duration=frames/60f;
                        var clip=new AnimationClip{name="Bone"+kind+"_"+(step+1),frameRate=60};
                        var selected=bones.Where(t=>!upper||t.IsChildOf(map["Spine"])).ToArray();
                        var curves=selected.ToDictionary(t=>t,t=>Enumerable.Range(0,7).Select(i=>new AnimationCurve()).ToArray());
                        for(int f=0;f<=frames;f++)
                        {
                            idle.SampleAnimation(model.gameObject,0);float time=f/60f,t=time/duration;
                            float fade=1-Ease(.82f,1,t),weight=Ease(0,.14f,t)*fade;
                            float strike=Ease(hit-.15f,hit+.025f,time),yaw=0,lean=0;
                            Vector3 rightDir,leftDir=new Vector3(-.7f,.15f,.2f);float extension=.94f;
                            if(bow)
                            {
                                float draw=Ease(0,.42f,t)*(1-Ease(.5f,.72f,t));
                                rightDir=Vector3.Lerp(new Vector3(.1f,.15f,.9f),new Vector3(.75f,.15f,-.4f),draw);leftDir=new Vector3(-.1f,.15f,1);yaw=-12*draw;
                            }
                            else if(axe)
                            {
                                rightDir=Vector3.Lerp(new Vector3(.15f,1,-.4f),new Vector3(.08f,-.85f,1),strike);leftDir=rightDir+new Vector3(-.2f,-.15f,0);
                                lean=Mathf.Lerp(-14,step==2?38:26,strike);yaw=Mathf.Lerp(-12,12,strike);
                                if(step==2){float jump=Ease(.12f,.58f,time)*(1-Ease(.78f,1.1f,time));skeleton.position+=model.up*(1.7f*jump);}
                                hip.position-=model.up*((step==2?.13f:.08f)*weight*(1-strike));
                            }
                            else if(spear&&step==2)
                            {
                                float cycle=(time-(hit-.035f))/.08f;float phase=cycle-Mathf.Floor(cycle);
                                float jab=time<hit-.035f||time>hit+11*.08f+.06f?0:Mathf.Pow(Mathf.Sin(phase*Mathf.PI),2);
                                rightDir=Vector3.Lerp(new Vector3(.35f,-.05f,-.25f),new Vector3(.02f,0,1),jab);extension=Mathf.Lerp(.5f,.98f,jab);
                                leftDir=new Vector3(-.2f,-.05f,.7f);yaw=-10+18*jab;lean=8+8*jab;
                            }
                            else
                            {
                                float sweep=strike;
                                if(spear&&step==1)sweep=Ease(.1f,.29f,time)-Ease(.4f,.6f,time);
                                rightDir=Vector3.Lerp(new Vector3(1,.2f,-.2f),new Vector3(-.95f,.05f,.65f),sweep);
                                if(spear&&step==1)rightDir.x=-rightDir.x;
                                yaw=Mathf.Lerp(-40,40,sweep)*(spear&&step==1?-1:1);lean=6;
                            }
                            if(!upper)hip.localRotation*=Quaternion.Euler(lean*.3f*weight,yaw*.25f*weight,0);
                            map["Spine"].localRotation*=Quaternion.Euler(lean*.5f*weight,yaw*.5f*weight,0);
                            map["Spine2"].localRotation*=Quaternion.Euler(lean*.3f*weight,yaw*.5f*weight,0);
                            map["Head"].localRotation*=Quaternion.Euler(-lean*.25f*weight,-yaw*.35f*weight,0);
                            foreach(bool right in new[]{true,false})
                            {
                                string side=right?"Right":"Left";var u=map[side+"Arm"];var e=map[side+"ForeArm"];var h=map[side+"Hand"];
                                float length=Vector3.Distance(u.position,e.position)+Vector3.Distance(e.position,h.position);
                                var target=u.position+model.TransformDirection((right?rightDir:leftDir).normalized)*length*(right?extension:.87f);
                                Arm(u,e,h,Vector3.Lerp(h.position,target,weight),u.position+model.right*(right?length:-length)-model.up*length*.25f);
                                if(right){var blade=(spear&&step==2)||bow?model.forward:model.TransformDirection(rightDir.normalized);h.rotation=Quaternion.Slerp(h.rotation,Quaternion.FromToRotation(h.right,blade)*h.rotation,weight);}
                            }
                            if(!upper)
                            {
                                float bend=(axe?18:12)*weight;
                                map["LeftUpLeg"].localRotation*=Quaternion.Euler(-bend,0,0);map["RightUpLeg"].localRotation*=Quaternion.Euler(-bend,0,0);
                                map["LeftLeg"].localRotation*=Quaternion.Euler(bend*1.5f,0,0);map["RightLeg"].localRotation*=Quaternion.Euler(bend*1.5f,0,0);
                            }
                            foreach(var bone in selected){var p=bone.localPosition;var q=bone.localRotation;float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};for(int j=0;j<7;j++)curves[bone][j].AddKey(time,values[j]);}
                        }
                        foreach(var pair in curves)for(int j=0;j<7;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(pair.Key,model),typeof(Transform),j<3?"m_LocalPosition."+"xyz"[j]:"m_LocalRotation."+"xyzw"[j-3]),pair.Value[j]);
                        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
                        string path="Assets/Animations/Player/DogLight_BasicWeapons_"+clip.name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if(old){EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);clip=old;}else AssetDatabase.CreateAsset(clip,path);
                        profile.attacks[step]=clip;profile.strikeTimes[step]=hit;if(spear&&step==1)profile.secondaryStrikeTimes[step]=.57f;
                        var sm=upper?upperMachine:machine;var state=sm.states.FirstOrDefault(s=>s.state.name==clip.name).state;if(!state)state=sm.AddState(clip.name);state.motion=clip;state.writeDefaultValues=!upper;
                        EditorUtility.SetDirty(clip);
                    }
                    EditorUtility.SetDirty(profile);
                }
                EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return "8 clips: single throw, charged bow, axe combo, spear combo";
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
