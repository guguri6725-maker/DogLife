#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 가상 본 좌표로 두 관절 IK를 계산해 클립에 굽습니다. 씬 오브젝트는 만들거나 변경하지 않습니다.
    public static class FrozenKnightForwardAuthoring
    {
        const string Folder="Assets/Art/FrozenKnight/Animations/";
        public static string Build()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FrozenKnight.prefab");
            var knight=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight").transform;
            var bones=knight.GetComponentsInChildren<Transform>(true).Where(t=>AnimationUtility.CalculateTransformPath(t,knight).StartsWith("skel/")).ToArray();
            var paths=bones.Select(t=>AnimationUtility.CalculateTransformPath(t,knight)).ToArray();
            var parent=bones.Select(t=>Array.IndexOf(bones,t.parent)).ToArray();
            int arm=Array.FindIndex(bones,t=>t.name=="RightArm"),fore=Array.FindIndex(bones,t=>t.name=="RightForeArm"),hand=Array.FindIndex(bones,t=>t.name=="RightHand");
            int leftArm=Array.FindIndex(bones,t=>t.name=="LeftArm"),leftFore=Array.FindIndex(bones,t=>t.name=="LeftForeArm"),leftHand=Array.FindIndex(bones,t=>t.name=="LeftHand");
            var sword=knight.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Sword");
            Vector3 bladeAxis=Quaternion.Inverse(bones[hand].rotation)*sword.TransformDirection(Vector3.down);
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"Idle.anim");
            foreach(string name in new[]{"SwordReady","Attack1","Attack2","SpinReady","Attack3","SlamReady","Slam"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+name+".anim");
                var output=paths.Select(p=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()}).ToArray();
                var positions=bones.Select(t=>t.localPosition).ToArray();
                for(int frame=0;frame<=60;frame++)
                {
                    float t=frame/60f;var rotations=new Quaternion[bones.Length];
                    for(int j=0;j<bones.Length;j++)
                    {
                        var rest=Read(idle,paths[j],0,bones[j].localRotation);var q=Read(clip,paths[j],t*clip.length,rest);
                        string bone=bones[j].name;
                        // 허리와 하체의 작은 무게 이동은 살리고 뒤돌기/갑옷 비틀림은 제한합니다.
                        float limit=bone=="Hips"?7:bone.StartsWith("Spine")?6:bone.Contains("Shoulder")?12:bone.Contains("Leg")?15:bone.Contains("Foot")?12:bone=="Head"||bone=="Neck"?10:bone.StartsWith("Left")?20:80;
                        bool armBone=bone.Contains("Arm")||bone.Contains("Hand")||bone.Contains("Shoulder");
                        rotations[j]=bone=="Root"?Quaternion.identity:armBone?rest:Quaternion.RotateTowards(rest,q,limit);
                        positions[j]=new Vector3(Value(clip,paths[j],"m_LocalPosition.x",t*clip.length,positions[j].x),Value(clip,paths[j],"m_LocalPosition.y",t*clip.length,positions[j].y),Value(clip,paths[j],"m_LocalPosition.z",t*clip.length,positions[j].z));
                    }
                    var wp=new Vector3[bones.Length];var wr=new Quaternion[bones.Length];
                    Action fk=()=>{for(int j=0;j<bones.Length;j++){int p=parent[j];wr[j]=p<0?rotations[j]:wr[p]*rotations[j];wp[j]=p<0?positions[j]:wp[p]+wr[p]*positions[j];}};fk();
                    bool slam=name=="Slam"||name=="SlamReady";float progress=Mathf.SmoothStep(0,1,t);
                    float angle=name=="Attack1"?Mathf.Lerp(70,-70,progress):name=="Attack2"?Mathf.Lerp(-70,70,progress):name=="Attack3"?Mathf.Lerp(70,-85,progress):70;
                    Vector3 axis=Quaternion.AngleAxis(angle,Vector3.up)*new Vector3(0,-.18f,-1).normalized;
                    // 손잡이를 몸 가까이 두어 양팔 모두 닿는 반경 안에서 함께 움직입니다.
                    Vector3 target=new Vector3(axis.x*.10f,.65f,-.14f);
                    if(slam){float s=name=="SlamReady"?0:progress;axis=Vector3.Slerp(new Vector3(0,.97f,-.24f),new Vector3(0,-.73f,-.684f),s).normalized;target=new Vector3(0,Mathf.Lerp(.86f,.60f,s),-.15f);}
                    var original=(Quaternion[])rotations.Clone();var restWorld=(Quaternion[])wr.Clone();
                    Vector3 restBlade=wr[hand]*bladeAxis;
                    Quaternion bladeTurn=Quaternion.LookRotation(axis,slam?Vector3.Cross(axis,Vector3.right):Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(restBlade,Vector3.up));
                    Action<int,int,int,Vector3,Vector3> solve=(upperIndex,lowerIndex,wristIndex,goal,poleHint)=>
                    {
                        fk();Vector3 shoulder=wp[upperIndex],aim=(goal-shoulder).normalized;
                        float upper=Vector3.Distance(wp[upperIndex],wp[lowerIndex]),lower=Vector3.Distance(wp[lowerIndex],wp[wristIndex]);
                        float distance=Mathf.Clamp(Vector3.Distance(goal,shoulder),Mathf.Abs(upper-lower)+.002f,upper+lower-.012f);goal=shoulder+aim*distance;
                        float along=(upper*upper-lower*lower+distance*distance)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
                        Vector3 elbow=shoulder+aim*along+Vector3.ProjectOnPlane(poleHint,aim).normalized*height;
                        // 팔꿈치 굽힘 평면까지 맞춰 상완의 축 비틀림과 역꺾임을 방지합니다.
                        Vector3 oldPlane=Vector3.Cross(wp[lowerIndex]-shoulder,wp[wristIndex]-wp[lowerIndex]).normalized;
                        Vector3 newPlane=Vector3.Cross(elbow-shoulder,goal-elbow).normalized;
                        Quaternion turn=Quaternion.LookRotation(elbow-shoulder,newPlane)*Quaternion.Inverse(Quaternion.LookRotation(wp[lowerIndex]-shoulder,oldPlane));
                        rotations[upperIndex]=Quaternion.Inverse(wr[parent[upperIndex]])*turn*wr[upperIndex];fk();
                        var desired=Quaternion.FromToRotation(wp[wristIndex]-wp[lowerIndex],goal-wp[lowerIndex])*wr[lowerIndex];
                        rotations[lowerIndex]=Quaternion.Inverse(wr[parent[lowerIndex]])*desired;fk();
                        rotations[wristIndex]=Quaternion.Inverse(wr[parent[wristIndex]])*bladeTurn*restWorld[wristIndex];fk();
                    };
                    solve(arm,fore,hand,target,new Vector3(-.6f,-1,.15f));
                    // 오른손 아래 손잡이에 왼손을 고정합니다. 칼날 방향과 같은 좌표계를 사용합니다.
                    solve(leftArm,leftFore,leftHand,wp[hand]-axis*.075f,new Vector3(.6f,-1,.15f));
                    // 첫 준비 동작은 기존 대기 손 위치에서 서서히 칼을 들어 올립니다.
                    float weight=name=="SwordReady"||name=="SlamReady"?progress:1;
                    foreach(int index in new[]{arm,fore,hand,leftArm,leftFore,leftHand})rotations[index]=Quaternion.Slerp(original[index],rotations[index],weight);
                    for(int j=0;j<bones.Length;j++)for(int c=0;c<4;c++)output[j][c].AddKey(t*clip.length,rotations[j][c]);
                }
                for(int j=0;j<bones.Length;j++)for(int c=0;c<4;c++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(paths[j],typeof(Transform),"m_LocalRotation."+"xyzw"[c]),output[j][c]);
                clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);
            }
            AssetDatabase.SaveAssets();return "Forward sword trajectories baked into seven foot clips";
        }
        static float Value(AnimationClip clip,string path,string property,float t,float fallback)
        {var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property));return curve==null?fallback:curve.Evaluate(t);}
        static Quaternion Read(AnimationClip clip,string path,float t,Quaternion fallback)
        {return new Quaternion(Value(clip,path,"m_LocalRotation.x",t,fallback.x),Value(clip,path,"m_LocalRotation.y",t,fallback.y),Value(clip,path,"m_LocalRotation.z",t,fallback.z),Value(clip,path,"m_LocalRotation.w",t,fallback.w)).normalized;}
    }
}
#endif
