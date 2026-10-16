using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DogLife.EditorTools
{
    // Author only the sword/dagger clips; other weapon movesets retain their tuning.
    public static class BladeComboAnimationBuilder
    {
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static Vector3 Track(float t,float[] times,Vector3[] values)
        {
            for(int i=1;i<times.Length;i++)if(t<=times[i])return Vector3.Lerp(values[i-1],values[i],Ease(times[i-1],times[i],t));
            return values[values.Length-1];
        }
        static void IK(Transform upper,Transform joint,Transform tip,Vector3 target,Vector3 pole)
        {
            float a=Vector3.Distance(upper.position,joint.position),b=Vector3.Distance(joint.position,tip.position);
            Vector3 delta=target-upper.position;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
            Vector3 dir=delta.normalized,side=Vector3.ProjectOnPlane(pole-upper.position,dir).normalized;
            float along=(a*a+d*d-b*b)/(2*d);
            Vector3 elbow=upper.position+dir*along+side*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(joint.position-upper.position,elbow-upper.position)*upper.rotation;
            joint.rotation=Quaternion.FromToRotation(tip.position-joint.position,upper.position+dir*d-joint.position)*joint.rotation;
        }
        public static string Build(bool daggerOnly=false,bool swordOnly=false)
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/DogLight.controller");
            var machine=controller.layers[0].stateMachine;
            var idle=((BlendTree)machine.states.First(s=>s.state.name=="Locomotion").state.motion).children[0].motion as AnimationClip;
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/SeasonGameplay.prefab");
            try
            {
                var animator=root.GetComponentInChildren<PlayerDirectionalAnimation>(true).animator;
                var model=animator.transform;
                var skeleton=model.Find("skel/Root");
                var bones=skeleton.GetComponentsInChildren<Transform>().Where(t=>t.name!="WeaponSocket").ToArray();
                var map=bones.ToDictionary(t=>t.name,t=>t);
                var hip=map["Hips"];var rh=map["RightHand"];var lh=map["LeftHand"];
                foreach(bool dagger in new[]{false,true})
                {
                    if(daggerOnly&&!dagger)continue;
                    if(swordOnly&&dagger)continue;
                    string category=dagger?"Dagger":"Sword";
                    var profile=AssetDatabase.LoadAssetAtPath<BasicWeaponMoveset>("Assets/Data/WeaponMovesets/"+category+".asset");
                    profile.comboLinkNormalized=.76f;
                    profile.secondaryStrikeTimes=new float[3];
                    profile.inputBuffer=.32f;
                    for(int step=0;step<3;step++)
                    {
                        float duration=dagger?(step==2?.92f:step==1?.58f:.45f):(step==0?.74f:step==1?.94f:1.16f);
                        int frames=Mathf.CeilToInt(duration*60);duration=frames/60f;
                        var clip=new AnimationClip{name="Bone"+category+"_"+(step+1),frameRate=60};
                        var curves=new Dictionary<Transform,AnimationCurve[]>();
                        foreach(var bone in bones)curves[bone]=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
                        for(int frame=0;frame<=frames;frame++)
                        {
                            idle.SampleAnimation(model.gameObject,0);
                            float t=frame/(float)frames,fade=1-Ease(.78f,1,t),engage=step==0?Ease(0,.16f,t):1;
                            float weight=fade*engage;
                            float spin=!dagger&&step==2?360*Ease(.18f,.72f,t):0;
                            skeleton.rotation=Quaternion.AngleAxis(spin,model.up)*skeleton.rotation;
                            var leftPlant=map["LeftFoot"].position;var rightPlant=map["RightFoot"].position;
                            var leftRot=map["LeftFoot"].rotation;var rightRot=map["RightFoot"].rotation;
                            Quaternion turn=Quaternion.AngleAxis(spin,Vector3.up);
                            Vector3 arm;
                            float yaw,lean;
                            if(dagger)
                            {
                                float push=Ease(step==2?.42f:.2f,step==2?.56f:.4f,t)*(1-Ease(.64f,.78f,t));
                                Vector3 wind=step==0?new Vector3(.3f,-.4f,-.15f):step==1?new Vector3(.7f,1,-.25f):new Vector3(.9f,.25f,-.65f);
                                Vector3 thrust=step==0?new Vector3(.08f,-.25f,1):step==1?new Vector3(-.3f,.38f,1):new Vector3(.02f,0,1);
                                arm=Vector3.Lerp(wind,thrust,push);
                                yaw=step==0?Mathf.Lerp(-12,18,push):step==1?Mathf.Lerp(38,-32,push):Mathf.Lerp(-55,42,push);
                                lean=step==2?Mathf.Lerp(-16,30,push):step==1?12*push:4*push;
                            }
                            else if(step==0)
                            {
                                arm=Track(t,new[]{0,.2f,.45f,.7f,1},new[]{new Vector3(.3f,-.2f,.4f),new Vector3(.95f,.55f,-.25f),new Vector3(-.9f,-.25f,.7f),new Vector3(-.65f,.05f,.4f),new Vector3(-.65f,.05f,.4f)});
                                yaw=Mathf.Lerp(-42,48,Ease(.2f,.45f,t));lean=10*Ease(.2f,.45f,t);
                            }
                            else if(step==1)
                            {
                                arm=Track(t,new[]{0,.13f,.32f,.42f,.6f,.72f,1},new[]{new Vector3(-.65f,.05f,.4f),new Vector3(-.85f,-.2f,.1f),new Vector3(.95f,.25f,.6f),new Vector3(.95f,.4f,-.15f),new Vector3(-.95f,-.15f,.6f),new Vector3(.7f,.35f,-.2f),new Vector3(.7f,.35f,-.2f)});
                                yaw=Track(t,new[]{0,.13f,.32f,.42f,.6f,.72f,1},new[]{new Vector3(48,0,0),new Vector3(45,0,0),new Vector3(-48,0,0),new Vector3(-50,0,0),new Vector3(48,0,0),new Vector3(-35,0,0),new Vector3(-35,0,0)}).x;
                                lean=8;
                            }
                            else
                            {
                                arm=Vector3.Lerp(new Vector3(.7f,.35f,-.2f),new Vector3(.95f,.05f,.45f),Ease(.08f,.3f,t));
                                yaw=-35*(1-Ease(.12f,.35f,t));lean=10*Mathf.Sin(t*Mathf.PI);
                            }
                            // Hips and legs remain driven by locomotion.
                            if(!dagger&&step==2)hip.localRotation*=Quaternion.Euler(lean*.4f*weight,yaw*.25f*weight,0);
                            map["Spine"].localRotation*=Quaternion.Euler(lean*.6f*weight,yaw*.55f*weight,0);
                            map["Spine2"].localRotation*=Quaternion.Euler(dagger?lean*.4f*weight:0,yaw*.45f*weight,dagger?(step==1?-12:step==2?10:0)*weight:-8*weight);
                            map["Head"].localRotation*=Quaternion.Euler(-lean*.3f*weight,-yaw*.35f*weight,0);
                            if(!dagger)hip.position-=model.up*(.065f*weight);
                            var ru=map["RightArm"];var re=map["RightForeArm"];
                            float length=Vector3.Distance(ru.position,re.position)+Vector3.Distance(re.position,rh.position);
                            Vector3 armDir=model.TransformDirection(turn*arm.normalized);
                            float extension=dagger?Mathf.Lerp(step==2?.65f:.45f,.98f,Ease(step==2?.42f:.2f,step==2?.56f:.4f,t)*(1-Ease(.64f,.78f,t))):.94f;
                            IK(ru,re,rh,Vector3.Lerp(rh.position,ru.position+armDir*length*extension,weight),ru.position+model.TransformDirection(turn*Vector3.right)*length-model.up*length*.4f);
                            Vector3 blade=dagger?model.forward:model.TransformDirection(turn*(step==2?new Vector3(.8f,0,.6f):arm.normalized));
                            var aimed=Quaternion.FromToRotation(rh.right,blade)*rh.rotation;
                            rh.rotation=Quaternion.Slerp(rh.rotation,aimed,weight);
                            var lu=map["LeftArm"];var le=map["LeftForeArm"];
                            float ll=Vector3.Distance(lu.position,le.position)+Vector3.Distance(le.position,lh.position);
                            Vector3 guard=model.TransformDirection(turn*(dagger?(step==2?Vector3.Lerp(new Vector3(-.2f,.4f,.4f),new Vector3(-.65f,-.2f,-.35f),Ease(.42f,.56f,t)):step==1?new Vector3(-.7f,-.2f,.1f):new Vector3(-.4f,.1f,.5f)):new Vector3(-.75f,.15f,-.2f)).normalized);
                            IK(lu,le,lh,Vector3.Lerp(lh.position,lu.position+guard*ll*.75f,weight),lu.position-model.TransformDirection(turn*Vector3.right)*ll);
                            // Plant feet while transferring weight; lift the trailing foot during the spin.
                            if(!dagger&&step==2)rightPlant+=model.up*(.07f*Mathf.Sin(Mathf.PI*Ease(.18f,.72f,t)));
                            IK(map["LeftUpLeg"],map["LeftLeg"],map["LeftFoot"],leftPlant,map["LeftUpLeg"].position+model.TransformDirection(turn*Vector3.forward));
                            IK(map["RightUpLeg"],map["RightLeg"],map["RightFoot"],rightPlant,map["RightUpLeg"].position+model.TransformDirection(turn*Vector3.forward));
                            map["LeftFoot"].rotation=leftRot;map["RightFoot"].rotation=rightRot;
                            foreach(var bone in bones)
                            {
                                var p=bone.localPosition;var q=bone.localRotation;var values=new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w};
                                for(int j=0;j<7;j++)curves[bone][j].AddKey(t*duration,values[j]);
                            }
                        }
                        foreach(var pair in curves)for(int j=0;j<7;j++)
                        {
                            if((dagger||step<2)&&!pair.Key.IsChildOf(map["Spine"]))continue;
                            var curve=pair.Value[j];
                            for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);}
                            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(pair.Key,model),typeof(Transform),j<3?"m_LocalPosition."+"xyz"[j]:"m_LocalRotation."+"xyzw"[j-3]),curve);
                        }
                        clip.EnsureQuaternionContinuity();
                        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
                        var destination=profile.attacks[step];EditorUtility.CopySerialized(clip,destination);Object.DestroyImmediate(clip);
                        profile.strikeTimes[step]=duration*(dagger?(step==2?.54f:.38f):step==0?.38f:step==1?.28f:.48f);
                        if(!dagger&&step==1)profile.secondaryStrikeTimes[step]=duration*.56f;
                        EditorUtility.SetDirty(destination);
                    }
                    EditorUtility.SetDirty(profile);
                }
                ConfigureBladeLayer(controller,model,map["Spine"]);
                AssetDatabase.SaveAssets();return "Upper-body dagger clips and masked layer saved.";
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void ConfigureBladeLayer(AnimatorController controller,Transform model,Transform spine)
        {
            const string path="Assets/Animations/Player/DogLight_DaggerUpperBody.mask";
            var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if(!mask){mask=new AvatarMask();AssetDatabase.CreateAsset(mask,path);}
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,i==(int)AvatarMaskBodyPart.Body||i==(int)AvatarMaskBodyPart.Head||i==(int)AvatarMaskBodyPart.LeftArm||i==(int)AvatarMaskBodyPart.RightArm||i==(int)AvatarMaskBodyPart.LeftFingers||i==(int)AvatarMaskBodyPart.RightFingers);
            var transforms=model.GetComponentsInChildren<Transform>(true);mask.transformCount=transforms.Length;
            for(int i=0;i<transforms.Length;i++){mask.SetTransformPath(i,AnimationUtility.CalculateTransformPath(transforms[i],model));mask.SetTransformActive(i,transforms[i].IsChildOf(spine));}
            var layers=controller.layers.ToList();var layer=layers.FirstOrDefault(x=>x.name=="Blade Upper Body"||x.name=="Dagger Upper Body");
            if(layer==null){var sm=new AnimatorStateMachine{name="Blade Upper Body"};AssetDatabase.AddObjectToAsset(sm,controller);layer=new AnimatorControllerLayer{name="Blade Upper Body",stateMachine=sm};layers.Add(layer);}
            layer.name="Blade Upper Body";layer.stateMachine.name=layer.name;layer.avatarMask=mask;layer.defaultWeight=0;layer.blendingMode=AnimatorLayerBlendingMode.Override;
            var empty=layer.stateMachine.states.FirstOrDefault(x=>x.state.name=="Empty").state;
            if(!empty)empty=layer.stateMachine.AddState("Empty");empty.writeDefaultValues=false;layer.stateMachine.defaultState=empty;
            foreach(var category in new[]{"Sword","Dagger"})
            {
            var profile=AssetDatabase.LoadAssetAtPath<BasicWeaponMoveset>("Assets/Data/WeaponMovesets/"+category+".asset");
            foreach(var clip in profile.attacks){var state=layer.stateMachine.states.FirstOrDefault(x=>x.state.name==clip.name).state;if(!state)state=layer.stateMachine.AddState(clip.name);state.motion=clip;state.writeDefaultValues=false;}
            }
            controller.layers=layers.ToArray();EditorUtility.SetDirty(mask);EditorUtility.SetDirty(controller);
        }
    }
}
