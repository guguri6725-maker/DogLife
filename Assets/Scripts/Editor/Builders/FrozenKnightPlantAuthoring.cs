#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 대기 자세를 출발점으로 새 양손 꽂기/손 놓기/회수 동작을 굽습니다. 공격 클립은 재사용하지 않습니다.
    public static class FrozenKnightPlantAuthoring
    {
        const string Folder="Assets/Art/FrozenKnight/Animations/";
        public static string Build()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FrozenKnight.prefab");
            var knight=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight").transform;
            var bones=knight.GetComponentsInChildren<Transform>(true).Where(t=>AnimationUtility.CalculateTransformPath(t,knight).StartsWith("skel/")&&!t.name.StartsWith("Sword")).ToArray();
            var paths=bones.Select(t=>AnimationUtility.CalculateTransformPath(t,knight)).ToArray();
            var parents=bones.Select(t=>Array.IndexOf(bones,t.parent)).ToArray();
            int ra=Array.FindIndex(bones,t=>t.name=="RightArm"),rf=Array.FindIndex(bones,t=>t.name=="RightForeArm"),rh=Array.FindIndex(bones,t=>t.name=="RightHand");
            int la=Array.FindIndex(bones,t=>t.name=="LeftArm"),lf=Array.FindIndex(bones,t=>t.name=="LeftForeArm"),lh=Array.FindIndex(bones,t=>t.name=="LeftHand");
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"Idle.anim");
            var baseQ=bones.Select((t,i)=>ReadQ(idle,paths[i],t.localRotation)).ToArray();
            var baseP=bones.Select((t,i)=>new Vector3(Value(idle,paths[i],"m_LocalPosition.x",t.localPosition.x),Value(idle,paths[i],"m_LocalPosition.y",t.localPosition.y),Value(idle,paths[i],"m_LocalPosition.z",t.localPosition.z))).ToArray();
            var sword=knight.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Sword");
            Quaternion swordInHand=Quaternion.Inverse(bones[rh].rotation)*sword.rotation;
            // 검 모델의 -Y가 칼끝입니다. 손잡이를 위로, 칼끝을 수직 아래로 향하게 합니다.
            Quaternion rightWrist=Quaternion.Inverse(swordInHand);
            var clips=new[]{new AnimationClip{name="Plant",frameRate=60},new AnimationClip{name="PlantRetrieve",frameRate=60}};
            for(int c=0;c<clips.Length;c++)
            {
                float duration=c==0?2f:1.4f;int frames=Mathf.RoundToInt(duration*60);
                var qr=paths.Select(p=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()}).ToArray();
                var pr=paths.Select(p=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()}).ToArray();
                for(int frame=0;frame<=frames;frame++)
                {
                    float t=frame/(float)frames;if(c==1)t=1-t;
                    var q=(Quaternion[])baseQ.Clone();var p=(Vector3[])baseP.Clone();
                    var wp=new Vector3[bones.Length];var wq=new Quaternion[bones.Length];
                    Action fk=()=>{for(int j=0;j<bones.Length;j++){int par=parents[j];wq[j]=par<0?q[j]:wq[par]*q[j];wp[j]=par<0?p[j]:wp[par]+wq[par]*p[j];}};
                    fk();var initialRight=wq[rh];var initialLeft=wq[lh];
                    float lift=Smooth(t,0,.32f),press=Smooth(t,.35f,.55f),release=Smooth(t,.66f,.94f);
                    Vector3 raised=new Vector3(-.025f,1.05f,-.19f),planted=new Vector3(-.025f,.87f,-.23f);
                    Vector3 grip=Vector3.Lerp(raised,planted,press);
                    // 커진 대검의 칼끝이 준비 때부터 지면 속에 들어가지 않도록 세우며 약간 기울여 듭니다.
                    Quaternion upright=Quaternion.Euler(Mathf.Lerp(25,0,press),0,0);
                    // 각 팔의 길이와 바깥쪽 팔꿈치 평면을 유지한 정적 키포즈를 계산합니다.
                    Action<int,int,int,Vector3,Vector3,Quaternion> solve=(upper,lower,hand,goal,pole,wrist)=>
                    {
                        fk();Vector3 shoulder=wp[upper],axis=(goal-shoulder).normalized;
                        float l1=Vector3.Distance(shoulder,wp[lower]),l2=Vector3.Distance(wp[lower],wp[hand]);
                        float d=Mathf.Clamp(Vector3.Distance(shoulder,goal),Mathf.Abs(l1-l2)+.005f,l1+l2-.008f);goal=shoulder+axis*d;
                        float along=(l1*l1-l2*l2+d*d)/(2*d);float height=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
                        Vector3 elbow=shoulder+axis*along+Vector3.ProjectOnPlane(pole,axis).normalized*height;
                        Vector3 oldNormal=Vector3.Cross(wp[lower]-shoulder,wp[hand]-wp[lower]).normalized;
                        Vector3 newNormal=Vector3.Cross(elbow-shoulder,goal-elbow).normalized;
                        var turn=Quaternion.LookRotation(elbow-shoulder,newNormal)*Quaternion.Inverse(Quaternion.LookRotation(wp[lower]-shoulder,oldNormal));
                        q[upper]=Quaternion.Inverse(wq[parents[upper]])*turn*wq[upper];fk();
                        q[lower]=Quaternion.Inverse(wq[parents[lower]])*Quaternion.FromToRotation(wp[hand]-wp[lower],goal-wp[lower])*wq[lower];fk();
                        q[hand]=Quaternion.Inverse(wq[parents[hand]])*wrist;fk();
                    };
                    solve(ra,rf,rh,grip,new Vector3(-1,-.25f,.4f),upright*rightWrist);
                    solve(la,lf,lh,grip+upright*Vector3.down*.065f,new Vector3(1,-.25f,.4f),upright*rightWrist*Quaternion.Inverse(initialRight)*initialLeft);
                    foreach(int j in new[]{ra,rf,rh,la,lf,lh})
                    {
                        q[j]=Quaternion.Slerp(baseQ[j],q[j],lift);
                        // 꽂은 뒤 손을 놓고 양팔을 몸 옆으로 내립니다.
                        q[j]=Quaternion.Slerp(q[j],bones[j].localRotation,release);
                    }
                    for(int j=0;j<bones.Length;j++)
                    {
                        for(int n=0;n<4;n++)qr[j][n].AddKey(frame/60f,q[j][n]);
                        for(int n=0;n<3;n++)pr[j][n].AddKey(frame/60f,p[j][n]);
                    }
                }
                for(int j=0;j<bones.Length;j++)
                {
                    for(int n=0;n<4;n++)AnimationUtility.SetEditorCurve(clips[c],EditorCurveBinding.FloatCurve(paths[j],typeof(Transform),"m_LocalRotation."+"xyzw"[n]),qr[j][n]);
                    for(int n=0;n<3;n++)AnimationUtility.SetEditorCurve(clips[c],EditorCurveBinding.FloatCurve(paths[j],typeof(Transform),"m_LocalPosition."+"xyz"[n]),pr[j][n]);
                }
                clips[c].EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clips[c]);settings.loopTime=false;settings.stopTime=duration;AnimationUtility.SetAnimationClipSettings(clips[c],settings);
                string path=Folder+clips[c].name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(old){EditorUtility.CopySerialized(clips[c],old);UnityEngine.Object.DestroyImmediate(clips[c]);}else AssetDatabase.CreateAsset(clips[c],path);
            }
            AssetDatabase.SaveAssets();return "Authored Plant (2s) and PlantRetrieve (1.4s): upright two-hand press, release and regrip";
        }
        static float Smooth(float t,float a,float b)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static float Value(AnimationClip clip,string path,string property,float fallback)
        {var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property));return curve==null?fallback:curve.Evaluate(0);}
        static Quaternion ReadQ(AnimationClip clip,string path,Quaternion fallback)=>new Quaternion(Value(clip,path,"m_LocalRotation.x",fallback.x),Value(clip,path,"m_LocalRotation.y",fallback.y),Value(clip,path,"m_LocalRotation.z",fallback.z),Value(clip,path,"m_LocalRotation.w",fallback.w)).normalized;
    }
}
#endif
