#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 새 동작과 결정 메시만 에셋으로 제작합니다. Hierarchy/Inspector 연결은 Editor에서 합니다.
    public static class FrozenKnightEncounterAuthoring
    {
        const string Folder="Assets/Art/FrozenKnight/Animations/";
        static Quaternion Rotation(AnimationClip clip,string path,float time=0)
        {
            var q=Quaternion.identity;for(int i=0;i<4;i++){var c=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[i]));if(c!=null)q[i]=c.Evaluate(time);}return q.normalized;
        }
        static float GestureWeight(string name,float t)
        {
            // 포효는 움츠림→확장→회복, 투창은 준비 끝 자세에서 자연스럽게 이어집니다.
            if(name=="SpearRelease")return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.42f,t));
            if(name=="Roar")return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.52f,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,t)));
            return Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.48f));
        }
        public static string Build()
        {
            foreach(string name in new[]{"Roar","FrostChannel","SpearThrow","SpearRelease","MountedCommand","MountedStompReady","MountedStompImpact","MountedCharge"})
            {
                bool mounted=name.StartsWith("Mounted")||name.StartsWith("Spear");
                var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+(mounted?"MountedIdle":"Idle")+".anim");
                var clip=Object.Instantiate(source);clip.name=name;clip.ClearCurves();
                foreach(var binding in AnimationUtility.GetCurveBindings(source))
                {
                    if(binding.propertyName.StartsWith("m_LocalRotation"))continue;
                    float value=AnimationUtility.GetEditorCurve(source,binding).Evaluate(0);AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,2,value));
                }
                foreach(string path in AnimationUtility.GetCurveBindings(source).Where(b=>b.propertyName=="m_LocalRotation.x").Select(b=>b.path))
                {
                    var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};var rest=Rotation(source,path);
                    for(int i=0;i<=120;i++)
                    {
                        float t=i/120f,open=GestureWeight(name,t),strike=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.8f,t));Vector3 e=Vector3.zero;
                        if(name=="Roar")
                        {
                            float anticipation=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.26f))*5;
                            if(path.EndsWith("/Spine"))e.x=anticipation-14*open+8*strike*open;
                            if(path.EndsWith("/Spine2"))e.x=-6*open;
                            if(path.EndsWith("/Head"))e.x=-16*open+6*strike*open;
                            if(path.EndsWith("LeftArm"))e=new Vector3(-42*open,-15*open,-55*open);
                            if(path.EndsWith("RightArm"))e=new Vector3(-42*open,15*open,55*open);
                            if(path.EndsWith("ForeArm"))e.y=28*open;
                            if(path.EndsWith("UpLeg"))e.x=-8*open;
                            if(path.EndsWith("/LeftLeg")||path.EndsWith("/RightLeg"))e.x=12*open;
                        }
                        else if(name.StartsWith("Spear"))
                        {
                            float release=name=="SpearRelease"?Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.42f,t)):0;
                            open=name=="SpearRelease"?1-release:open;
                            float follow=release*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1,t)));
                            if(path.EndsWith("RightArm"))e=new Vector3(-65*open+45*follow,0,25*open+45*follow);
                            if(path.EndsWith("RightForeArm"))e.y=42*open-32*follow;
                            if(path.EndsWith("/Spine2"))e=new Vector3(-8*open+18*follow,-24*open+28*follow,0);
                            if(path.EndsWith("LeftArm"))e=new Vector3(-12*open,0,-25*open);
                        }
                        else if(name=="MountedStompReady"||name=="MountedStompImpact")
                        {
                            float lean=name=="MountedStompReady"?open:1-Mathf.SmoothStep(0,1,t);
                            float recoil=name=="MountedStompImpact"?Mathf.Sin(Mathf.PI*t)*9:0;
                            if(path.EndsWith("/Spine"))e.x=-22*lean+recoil;
                            if(path.EndsWith("/Head"))e.x=6*lean-recoil*.35f;
                            if(path.EndsWith("RightArm"))e=new Vector3(-35*lean,0,25*lean);
                            if(path.EndsWith("LeftArm"))e=new Vector3(-35*lean,0,-25*lean);
                        }
                        else if(name=="MountedCharge")
                        {
                            if(path.EndsWith("/Spine"))e=new Vector3(22+3*Mathf.Sin(t*Mathf.PI*4),0,2*Mathf.Sin(t*Mathf.PI*2));
                            if(path.EndsWith("/Spine2"))e.x=12;
                            if(path.EndsWith("/Head"))e.x=-8-2*Mathf.Sin(t*Mathf.PI*4);
                            if(path.EndsWith("LeftArm"))e=new Vector3(25,0,-15);
                            if(path.EndsWith("RightArm"))e=new Vector3(25,0,15);
                        }
                        else
                        {
                            if(path.EndsWith("/Spine"))e.x=-12*open;
                            if(name=="MountedCommand"&&path.EndsWith("/Spine2"))e.y=-10*open;
                            if(path.EndsWith("/Head"))e.x=-15*open;
                            if(path.EndsWith("LeftArm"))e=new Vector3(-45*open,-12*open,-48*open);
                            if(path.EndsWith("RightArm"))e=new Vector3(-45*open,12*open,48*open);
                            if(path.EndsWith("ForeArm"))e.y=22*open;
                        }
                        var q=rest*Quaternion.Euler(e);for(int k=0;k<4;k++)curves[k].AddKey(t*2,q[k]);
                    }
                    for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]),curves[k]);
                }
                if(name!="MountedCharge")RefineArms(clip,source,name);
                clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=name=="MountedCharge";AnimationUtility.SetAnimationClipSettings(clip,settings);
                var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+name+".anim");if(old){EditorUtility.CopySerialized(clip,old);Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,Folder+name+".anim");
            }
            // 팔면체를 두 번 세분화하고 면별 법선을 사용한 구형 얼음 결정입니다.
            var points=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
            int[] faces={0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};
            var tris=new List<Vector3>();foreach(int n in faces)tris.Add(points[n]);
            for(int level=0;level<2;level++){var next=new List<Vector3>();for(int i=0;i<tris.Count;i+=3){var a=tris[i];var b=tris[i+1];var c=tris[i+2];var ab=(a+b).normalized;var bc=(b+c).normalized;var ca=(c+a).normalized;next.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}tris=next;}
            var mesh=new Mesh{name="FacetedFrostOrb"};mesh.SetVertices(tris.Select(v=>v*.5f).ToList());mesh.SetTriangles(Enumerable.Range(0,tris.Count).ToArray(),0);mesh.colors=Enumerable.Repeat(Color.white,tris.Count).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            const string output="Assets/Art/FrozenKnight/Models/FrostOrb.asset";var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(output);if(oldMesh){EditorUtility.CopySerialized(mesh,oldMesh);Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,output);
            AssetDatabase.SaveAssets();return "Roar, spin, throw and faceted orb authored";
        }
        static void RefineArms(AnimationClip clip,AnimationClip source,string name)
        {
            // 팔 길이와 팔꿈치 평면을 보존한 키포즈를 클립에 굽습니다. 실행 중 팔을 강제 조종하지 않습니다.
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FrozenKnight.prefab");
            var rig=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight").transform;
            var bones=rig.GetComponentsInChildren<Transform>(true).Where(t=>AnimationUtility.CalculateTransformPath(t,rig).StartsWith("skel/")&&!t.name.StartsWith("Sword")).ToArray();
            var paths=bones.Select(t=>AnimationUtility.CalculateTransformPath(t,rig)).ToArray();var parents=bones.Select(t=>System.Array.IndexOf(bones,t.parent)).ToArray();
            var armIndices=Enumerable.Range(0,bones.Length).Where(i=>bones[i].name.EndsWith("Arm")||bones[i].name.EndsWith("Hand")).ToArray();
            var curves=armIndices.ToDictionary(i=>i,i=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()});
            for(int frame=0;frame<=120;frame++)
            {
                float t=frame/120f,time=t*2,open=GestureWeight(name,t);
                var q=paths.Select(p=>Rotation(clip,p,time)).ToArray();var p=bones.Select((b,i)=>b.localPosition).ToArray();
                for(int i=0;i<bones.Length;i++)for(int k=0;k<3;k++){var c=AnimationUtility.GetEditorCurve(source,EditorCurveBinding.FloatCurve(paths[i],typeof(Transform),"m_LocalPosition."+"xyz"[k]));if(c!=null)p[i][k]=c.Evaluate(0);}
                foreach(int i in armIndices)q[i]=Rotation(source,paths[i]);
                var wp=new Vector3[bones.Length];var wq=new Quaternion[bones.Length];
                System.Action fk=()=>{for(int i=0;i<bones.Length;i++){int parent=parents[i];wq[i]=parent<0?q[i]:wq[parent]*q[i];wp[i]=parent<0?p[i]:wp[parent]+wq[parent]*p[i];}};fk();
                foreach(string side in new[]{"Right","Left"})
                {
                    int upper=System.Array.FindIndex(bones,b=>b.name==side+"Arm"),lower=System.Array.FindIndex(bones,b=>b.name==side+"ForeArm"),hand=System.Array.FindIndex(bones,b=>b.name==side+"Hand");
                    float sign=side=="Right"?-1:1;Vector3 rest=wp[hand],shoulder=wp[upper],goal;Quaternion wrist=wq[hand];
                    if(name.StartsWith("Spear")&&side=="Right")
                    {
                        Vector3 raised=shoulder+new Vector3(-.12f,.28f,.12f),thrown=shoulder+new Vector3(-.015f,.055f,-.39f);
                        if(name=="SpearThrow")goal=Vector3.Lerp(rest,raised,open);
                        else {float release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.42f,t));goal=Vector3.Lerp(raised,thrown,release);goal=Vector3.Lerp(goal,rest,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1,t)));}
                    }
                    else
                    {
                        Vector3 offset=name=="FrostChannel"?new Vector3(sign*.19f,.24f,-.1f):new Vector3(sign*.29f,.075f,-.075f);
                        if(name.StartsWith("Spear"))offset=new Vector3(sign*.2f,-.1f,-.1f);
                        // 기마 명령은 오른손으로 이끌고 왼손은 낮게 유지해 대칭 만세 자세를 줄입니다.
                        if(name=="MountedCommand")offset=side=="Right"?new Vector3(sign*.2f,.18f,-.18f):new Vector3(sign*.13f,-.14f,-.18f);
                        if(name.StartsWith("MountedStomp"))offset=new Vector3(sign*.12f,.09f,-.24f);
                        float weight=name=="MountedStompImpact"?1-Mathf.SmoothStep(0,1,t):open;goal=Vector3.Lerp(rest,shoulder+offset,weight);
                    }
                    float l1=Vector3.Distance(shoulder,wp[lower]),l2=Vector3.Distance(wp[lower],wp[hand]);Vector3 axis=(goal-shoulder).normalized;
                    float distance=Mathf.Clamp(Vector3.Distance(shoulder,goal),Mathf.Abs(l1-l2)+.005f,l1+l2-.01f);goal=shoulder+axis*distance;
                    float along=(l1*l1-l2*l2+distance*distance)/(2*distance);
                    Vector3 elbow=shoulder+axis*along+Vector3.ProjectOnPlane(new Vector3(sign,-.35f,.25f),axis).normalized*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
                    Vector3 oldNormal=Vector3.Cross(wp[lower]-shoulder,wp[hand]-wp[lower]).normalized,newNormal=Vector3.Cross(elbow-shoulder,goal-elbow).normalized;
                    Quaternion turn=Quaternion.LookRotation(elbow-shoulder,newNormal)*Quaternion.Inverse(Quaternion.LookRotation(wp[lower]-shoulder,oldNormal));
                    q[upper]=Quaternion.Inverse(wq[parents[upper]])*turn*wq[upper];fk();
                    q[lower]=Quaternion.Inverse(wq[parents[lower]])*Quaternion.FromToRotation(wp[hand]-wp[lower],goal-wp[lower])*wq[lower];fk();
                    q[hand]=Quaternion.Inverse(wq[parents[hand]])*wrist;fk();
                }
                foreach(int i in armIndices)for(int k=0;k<4;k++)curves[i][k].AddKey(time,q[i][k]);
            }
            foreach(int i in armIndices)for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(paths[i],typeof(Transform),"m_LocalRotation."+"xyzw"[k]),curves[i][k]);
        }
        public static string BuildPillar()
        {
            // 밑면에 붙어 있는 육각 기둥과 비대칭 꼭대기: 단순 마름모 대신 긴 얼음 절벽 실루엣입니다.
            var vertices=new List<Vector3>();var colors=new List<Color>();
            var levels=new[]{0f,.12f,.8f,1f};var radii=new[]{.5f,.56f,.42f,.15f};
            for(int layer=0;layer<3;layer++)for(int face=0;face<6;face++)
            {
                var quad=new Vector3[4];for(int j=0;j<4;j++){int ring=layer+(j>=2?1:0);int side=face+(j==1||j==2?1:0);float a=side*Mathf.PI/3;quad[j]=new Vector3(Mathf.Cos(a)*radii[ring],levels[ring]-(ring==3?.05f*(side%3):0),Mathf.Sin(a)*radii[ring]);}
                foreach(int k in new[]{0,2,1,0,3,2}){vertices.Add(quad[k]);colors.Add(Color.white);}
            }
            for(int face=0;face<6;face++){float a=face*Mathf.PI/3,b=(face+1)*Mathf.PI/3;vertices.AddRange(new[]{new Vector3(0,1.06f,0),new Vector3(Mathf.Cos(b)*.15f,1-.05f*((face+1)%3),Mathf.Sin(b)*.15f),new Vector3(Mathf.Cos(a)*.15f,1-.05f*(face%3),Mathf.Sin(a)*.15f)});colors.AddRange(new[]{Color.white,Color.white,Color.white});}
            var mesh=new Mesh{name="FacetedFrostPillar"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            const string path="Assets/Art/FrozenKnight/Models/FrostPillar.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);AssetDatabase.SaveAssets();return "Pillar mesh authored";
        }
        public static string BuildSpear()
        {
            // 가는 육각 자루, 넓은 어깨, 긴 얼음 촉을 한 메시로 만듭니다. +Z가 날아가는 방향입니다.
            float[] z={-1.8f,-1.55f,.4f,.7f,1.05f,1.75f,2.5f};
            float[] radius={.02f,.065f,.065f,.34f,.28f,.15f,0};
            var v=new List<Vector3>();var colors=new List<Color>();
            for(int ring=0;ring<z.Length-1;ring++)for(int side=0;side<8;side++)
            {
                var q=new Vector3[4];for(int j=0;j<4;j++){int r=ring+(j>=2?1:0),s=side+(j==1||j==2?1:0);float a=s*Mathf.PI/4;q[j]=new Vector3(Mathf.Cos(a)*radius[r],Mathf.Sin(a)*radius[r]*(r>=3?.48f:1),z[r]);}
                foreach(int n in new[]{0,1,2,0,2,3}){v.Add(q[n]);colors.Add(side%2==0?new Color(.6f,.9f,1,1):Color.white);}
            }
            var mesh=new Mesh{name="RoyalIceSpear"};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(Enumerable.Range(0,v.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            const string path="Assets/Art/FrozenKnight/Models/RoyalIceSpear.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);AssetDatabase.SaveAssets();return "Royal spear authored";
        }
    }
}
#endif
