using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
namespace DogLife.EditorTools
{
    public static class BasicWeaponAnimationBuilder
    {
        static Transform Bone(Animator a,string name)=>a.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static void Rotate(Transform t,Vector3 angles){t.localRotation*=Quaternion.Euler(angles);}
        static void Arm(Transform upper,Transform elbow,Transform hand,Vector3 target,Vector3 pole)
        {
            float a=Vector3.Distance(upper.position,elbow.position),b=Vector3.Distance(elbow.position,hand.position);
            var direction=(target-upper.position).normalized;float d=Mathf.Clamp(Vector3.Distance(target,upper.position),Mathf.Abs(a-b)+.001f,a+b-.001f);
            var side=Vector3.ProjectOnPlane(pole-upper.position,direction).normalized;
            float along=(a*a+d*d-b*b)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var joint=upper.position+direction*along+side*height;
            upper.rotation=Quaternion.FromToRotation(elbow.position-upper.position,joint-upper.position)*upper.rotation;
            elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,upper.position+direction*d-elbow.position)*elbow.rotation;
        }
        public static string Build()
        {
            const string folder="Assets/Art/Player3D/DogLight/Animations/BasicWeapons";
            System.IO.Directory.CreateDirectory(folder);System.IO.Directory.CreateDirectory("Assets/Data/WeaponMovesets");AssetDatabase.Refresh();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Player3D/DogLight/Animations/DogLight.controller");
            var machine=controller.layers[0].stateMachine;var locomotion=machine.states.First(s=>s.state.name=="Locomotion").state;
            var idle=((BlendTree)locomotion.motion).children[0].motion as AnimationClip;
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/SeasonGameplay.prefab");
            var animator=root.GetComponentInChildren<PlayerDirectionalAnimation>(true).animator;
            var model=animator.transform;
            var bones=animator.GetComponentsInChildren<Transform>().Where(t=>t.IsChildOf(model.Find("skel/Root"))).ToArray();
            var hip=Bone(animator,"Hips");var spine=Bone(animator,"Spine");var chest=Bone(animator,"Spine2");var head=Bone(animator,"Head");
            var ru=Bone(animator,"RightArm");var re=Bone(animator,"RightForeArm");var rh=Bone(animator,"RightHand");var lu=Bone(animator,"LeftArm");var le=Bone(animator,"LeftForeArm");var lh=Bone(animator,"LeftHand");
            var leftThigh=Bone(animator,"LeftUpLeg");var leftKnee=Bone(animator,"LeftLeg");var leftFoot=Bone(animator,"LeftFoot");
            var rightThigh=Bone(animator,"RightUpLeg");var rightKnee=Bone(animator,"RightLeg");var rightFoot=Bone(animator,"RightFoot");
            var sets=new List<BasicWeaponMoveset>();float[] durations={.65f,.82f,1.1f,.8f,.42f,.8f};float[] ranges={2,3,2.4f,14,1.35f,7};
            try
            {
                for(int kind=0;kind<6;kind++)
                {
                    string category=((InventoryWeapon.WeaponCategory)kind).ToString();string profilePath="Assets/Data/WeaponMovesets/"+category+".asset";
                    var profile=AssetDatabase.LoadAssetAtPath<BasicWeaponMoveset>(profilePath);if(!profile){profile=ScriptableObject.CreateInstance<BasicWeaponMoveset>();AssetDatabase.CreateAsset(profile,profilePath);}
                    profile.category=(InventoryWeapon.WeaponCategory)kind;profile.attacks=new AnimationClip[3];profile.strikeTimes=new float[3];profile.reach=ranges[kind];profile.projectileSpeed=kind==3?16:10;
                    profile.effectMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Enemies/BlossomTelegraph.mat");
                    for(int step=0;step<3;step++)
                    {
                        float duration=durations[kind]*(step==2?1.22f:1);int frames=Mathf.CeilToInt(duration*30);duration=frames/30f;
                        string name="Bone"+category+"_"+(step+1);var clip=new AnimationClip{name=name,frameRate=30};
                        var curves=new Dictionary<string,AnimationCurve[]>();foreach(var bone in bones){var arr=new AnimationCurve[7];for(int j=0;j<7;j++)arr[j]=new AnimationCurve();curves[AnimationUtility.CalculateTransformPath(bone,model)]=arr;}
                        for(int f=0;f<=frames;f++)
                        {
                            idle.SampleAnimation(animator.gameObject,0);float t=f/(float)frames;
                            var leftPlant=leftFoot.position;var rightPlant=rightFoot.position;var leftRotation=leftFoot.rotation;var rightRotation=rightFoot.rotation;
                            float prepare=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.33f)),strike=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.33f)/.22f)),recover=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.65f)/.35f));
                            float sign=step==1?-1:1;float twist=(kind==2||kind==1)?8:(kind==4?20:38);
                            float yaw=Mathf.Lerp(-twist*prepare,twist,strike)*recover*sign;
                            Rotate(hip,new Vector3((kind==2?18:8)*strike*recover,yaw*.35f,0));Rotate(spine,new Vector3(8*strike*recover,yaw*.35f,0));Rotate(chest,new Vector3(0,yaw*.3f,0));Rotate(head,new Vector3(-4*strike*recover,-yaw*.4f,0));
                            float leg=prepare*recover;
                            Rotate(Bone(animator,"LeftUpLeg"),new Vector3(-10*leg,0,-3*leg));Rotate(Bone(animator,"RightUpLeg"),new Vector3(8*leg,0,3*leg));Rotate(Bone(animator,"LeftLeg"),new Vector3(15*leg,0,0));Rotate(Bone(animator,"RightLeg"),new Vector3(12*leg,0,0));
                            Vector3 prep=new Vector3(.8f,.2f,-.3f),hit=new Vector3(-.65f,-.15f,1);
                            if(kind==1){prep=new Vector3(.3f,-.1f,-.35f);hit=new Vector3(.05f,0,1);}
                            if(kind==2||kind==0&&step==2){prep=new Vector3(.15f,1,-.25f);hit=new Vector3(.1f,-.6f,1);}
                            if(kind==3){prep=new Vector3(.7f,.1f,-.4f);hit=new Vector3(.8f,.15f,-.6f);}
                            if(kind==4){prep=new Vector3(.4f,0,-.25f);hit=new Vector3(-.2f,.1f,1);}
                            if(kind==5){prep=new Vector3(1,.2f,-.3f);hit=new Vector3(-.5f,.1f,1);}
                            if(step==1&&(kind==0||kind==4)){prep.x=-prep.x;hit.x=-hit.x;}
                            float armLength=Vector3.Distance(ru.position,re.position)+Vector3.Distance(re.position,rh.position);
                            Vector3 direction=model.TransformDirection(Vector3.Lerp(prep.normalized,hit.normalized,strike));
                            var target=ru.position+direction*armLength*.94f;
                            Arm(ru,re,rh,Vector3.Lerp(rh.position,target,prepare*recover),ru.position+model.right*armLength-model.up*armLength*.3f);
                            float leftLength=Vector3.Distance(lu.position,le.position)+Vector3.Distance(le.position,lh.position);
                            var leftDir=kind==3?new Vector3(-.1f,.1f,1):kind==2?Vector3.Lerp(new Vector3(.5f,1,-.1f),new Vector3(.5f,-.4f,1),strike):new Vector3(-.7f,.15f,.2f);
                            Arm(lu,le,lh,Vector3.Lerp(lh.position,lu.position+model.TransformDirection(leftDir.normalized)*leftLength*.9f,prepare*recover),lu.position-model.right*leftLength-model.up*leftLength*.3f);
                            hip.position-=model.up*(.025f*prepare*recover);
                            Arm(leftThigh,leftKnee,leftFoot,leftPlant,leftThigh.position+model.forward);
                            Arm(rightThigh,rightKnee,rightFoot,rightPlant,rightThigh.position+model.forward);
                            leftFoot.rotation=leftRotation;rightFoot.rotation=rightRotation;
                            foreach(var bone in bones){var arr=curves[AnimationUtility.CalculateTransformPath(bone,model)];var p=bone.localPosition;var q=bone.localRotation;float time=f/30f;arr[0].AddKey(time,p.x);arr[1].AddKey(time,p.y);arr[2].AddKey(time,p.z);arr[3].AddKey(time,q.x);arr[4].AddKey(time,q.y);arr[5].AddKey(time,q.z);arr[6].AddKey(time,q.w);}
                        }
                        foreach(var pair in curves)for(int j=0;j<7;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(pair.Key,typeof(Transform),j<3?"m_LocalPosition."+"xyz"[j]:"m_LocalRotation."+"xyzw"[j-3]),pair.Value[j]);
                        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
                        string clipPath=folder+"/"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(existing){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;}else AssetDatabase.CreateAsset(clip,clipPath);
                        profile.attacks[step]=clip;profile.strikeTimes[step]=duration*.5f;
                        var state=machine.states.FirstOrDefault(s=>s.state.name==name).state;if(!state)state=machine.AddState(name);state.motion=clip;
                        if(state.transitions.Length==0){var transition=state.AddTransition(locomotion);transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;transition.duration=.1f;}
                    }
                    EditorUtility.SetDirty(profile);sets.Add(profile);
                }
            }finally{PrefabUtility.UnloadPrefabContents(root);}
            foreach(string path in new[]{"Assets/Prefabs/Player.prefab","Assets/Prefabs/SeasonGameplay.prefab"})
            {
                var prefab=PrefabUtility.LoadPrefabContents(path);try{var player=prefab.GetComponentInChildren<PlayerWeapons>(true);var combat=player.GetComponent<BasicWeaponCombat>()??player.gameObject.AddComponent<BasicWeaponCombat>();combat.animator=player.GetComponent<PlayerDirectionalAnimation>().animator;combat.movesets=sets.ToArray();PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();return "18 newly authored clips and 6 weapon movesets";
        }
    }
}
