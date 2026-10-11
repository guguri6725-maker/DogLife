#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // 모델/씬을 생성하지 않고 원본에서 편집한 동작을 독립 AnimationClip 에셋으로 저장합니다.
    public static class FrozenKnightAnimationAuthoring
    {
        const string Source="Assets/VARCO3DImports/WinterBoss/", Output="Assets/Art/FrozenKnight/Animations/";
        static AnimationClip SourceClip(string name)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Source+name+".anim");
        public static string BuildPlant()=>FrozenKnightPlantAuthoring.Build();
        public static string Build()
        {
            System.IO.Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            Bake("Idle","Idle",0,1,SourceClip("Idle").length,true);
            Bake("Walk","Walk",0,1,SourceClip("Walk").length,true);
            BuildPlant();
            Bake("Kneel","Idle",0,.1f,1,false);
            Bake("StandUp","Idle",0,.1f,1,false);
            Bake("Mount","Idle",0,.1f,1,false);
            Bake("MountedIdle","Idle",0,1,2,true);
            Bake("MountedReady","내려찍기",.16f,.28f,1,false);
            Bake("MountedCharge","Idle",0,1,1,true);
            Bake("MountedGroggy","Idle",0,1,2,true);
            var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/FrozenKnight/Models/IceHorse_Rigged.fbx").OfType<AnimationClip>().Where(a=>!a.name.StartsWith("__preview__")).ToArray();
            foreach(var source in clips)
            {
                string name=source.name.Split('|').Last();
                var copy=UnityEngine.Object.Instantiate(source);copy.name=name;
                var settings=AnimationUtility.GetAnimationClipSettings(copy);settings.loopTime=name=="Idle"||name=="Gallop"||name=="Groggy";AnimationUtility.SetAnimationClipSettings(copy,settings);
                Save(copy,"Horse_"+name);
            }
            BakeArmorMesh();BakeCrystal();AssetDatabase.SaveAssets();return "Knight clips and "+clips.Length+" horse clips saved";
        }
        public static void BakeArmorMesh()
        {
            // 망토가 팔/다리에 끌려 길게 늘어나는 원본 웨이트만 몸통 쪽으로 부드럽게 보정합니다.
            var renderer=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"WinterBoss_Prefab.usdz").GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh=UnityEngine.Object.Instantiate(renderer.sharedMesh);mesh.name="FrozenKnight_StableArmor";
            var vertices=mesh.vertices;var weights=mesh.boneWeights;
            int hips=Array.FindIndex(renderer.bones,b=>b.name=="Hips"),spine=Array.FindIndex(renderer.bones,b=>b.name=="Spine"),chest=Array.FindIndex(renderer.bones,b=>b.name=="Spine2");
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=vertices[i];float rear=Mathf.InverseLerp(p.y<0?.03f:-.005f,.075f,p.z)*Mathf.InverseLerp(.31f,.23f,p.y);
                float core=Mathf.InverseLerp(.115f,.065f,Mathf.Abs(p.x))*Mathf.InverseLerp(.025f,.09f,p.y)*Mathf.InverseLerp(.32f,.27f,p.y);
                // 망토 보정이 발뒤꿈치까지 몸통에 묶지 않도록 발목 아래는 제외합니다.
                float blend=Mathf.Max(rear,core)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.40f,-.33f,p.y));if(blend<=0)continue;
                var sums=new float[renderer.bones.Length];var w=weights[i];
                sums[w.boneIndex0]+=w.weight0*(1-blend);sums[w.boneIndex1]+=w.weight1*(1-blend);sums[w.boneIndex2]+=w.weight2*(1-blend);sums[w.boneIndex3]+=w.weight3*(1-blend);
                float upper=Mathf.InverseLerp(-.02f,.22f,p.y);sums[hips]+=blend*(1-upper);sums[core>rear?chest:spine]+=blend*upper;
                var indices=Enumerable.Range(0,sums.Length).OrderByDescending(j=>sums[j]).Take(4).ToArray();float total=indices.Sum(j=>sums[j]);
                weights[i]=new BoneWeight{boneIndex0=indices[0],weight0=sums[indices[0]]/total,boneIndex1=indices[1],weight1=sums[indices[1]]/total,boneIndex2=indices[2],weight2=sums[indices[2]]/total,boneIndex3=indices[3],weight3=sums[indices[3]]/total};
            }
            mesh.boneWeights=weights;
            const string path="Assets/Art/FrozenKnight/Models/FrozenKnight_StableArmor.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        }
        public static void BakeCrystal()
        {
            // FBX 노드의 축/단위를 메시 자체에 굽습니다. Graphics.DrawMesh에는 FBX 루트 변환이 적용되지 않습니다.
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/FrozenKnight/Models/FrostCrystal.fbx").GetComponentInChildren<MeshFilter>();
            var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);mesh.name="FrostCrystal";
            var matrix=source.transform.localToWorldMatrix;var axis=Quaternion.Euler(-90,0,0);
            mesh.vertices=mesh.vertices.Select(v=>axis*matrix.MultiplyPoint3x4(v)).ToArray();mesh.RecalculateBounds();
            var bounds=mesh.bounds;float scale=1/Mathf.Max(.001f,bounds.size.y);
            mesh.vertices=mesh.vertices.Select(v=>(v-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale).ToArray();
            mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            const string path="Assets/Art/FrozenKnight/Models/FrostCrystal.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);
            AssetDatabase.SaveAssets();
        }
        static void Save(AnimationClip clip,string file)
        {
            string path=Output+file+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(old){EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,path);
        }
        static void Bake(string name,string sourceName,float from,float to,float duration,bool loop)
        {
            var source=SourceClip(sourceName);var idle=SourceClip("Idle");var clip=new AnimationClip{name=name,frameRate=30};
            var bindings=AnimationUtility.GetCurveBindings(source);bool mounted=name.StartsWith("Mounted")||name=="Mount";
            int count=Mathf.Max(2,Mathf.RoundToInt(duration*30)+1);
            // 골반 위치와 다리 각도는 탑승용으로 별도 제작하고, 상체에는 제공된 대검 모션을 사용합니다.
            foreach(var binding in bindings)
            {
                if(binding.propertyName.Contains("m_LocalRotation"))continue;
                var curve=new AnimationCurve();var src=AnimationUtility.GetEditorCurve(source,binding);
                for(int i=0;i<count;i++)
                {
                    float t=(float)i/(count-1),value=src.Evaluate(Mathf.Lerp(from,to,t)*source.length);
                    if(binding.path=="skel/Root"&&binding.propertyName.StartsWith("m_LocalPosition"))
                    {
                        value=binding.propertyName.EndsWith(".y")?.5165f:0;
                        if(mounted&&binding.propertyName.EndsWith(".y"))value=name=="Mount"?.5165f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.85f,t))):0;
                        if(name=="Kneel"&&binding.propertyName.EndsWith(".y"))value-=.2f*Mathf.SmoothStep(0,1,t);
                        if(name=="StandUp"&&binding.propertyName.EndsWith(".y"))value-=.2f*(1-Mathf.SmoothStep(0,1,t));
                    }
                    curve.AddKey(t*duration,value);
                }
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            var paths=bindings.Where(b=>b.propertyName.StartsWith("m_LocalRotation")).Select(b=>b.path).Distinct();
            foreach(string path in paths)
            {
                var src=new AnimationCurve[4];var dst=new AnimationCurve[4];
                for(int k=0;k<4;k++){src[k]=AnimationUtility.GetEditorCurve(source,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]));dst[k]=new AnimationCurve();}
                for(int i=0;i<count;i++)
                {
                    float t=(float)i/(count-1),at=Mathf.Lerp(from,to,t)*source.length;
                    Quaternion q=new Quaternion(src[0].Evaluate(at),src[1].Evaluate(at),src[2].Evaluate(at),src[3].Evaluate(at)).normalized;
                    var baseQ=ReadRotation(idle,path,0);
                    // 분리본도 원본 회전을 사용하며 강제 팔 궤적 보정은 적용하지 않습니다.
                    bool originalFoot=name=="Attack1"||name=="Attack2"||name=="Attack3"||name=="Slam"||name=="SwordReady"||name=="SpinReady"||name=="SlamReady";
                    if(name!="Idle"&&name!="Walk"&&!originalFoot)q=StableRotation(path,q,baseQ,mounted);
                    Vector3 extra=Vector3.zero;
                    if(mounted)
                    {
                        if(path.EndsWith("UpLeg"))extra=new Vector3(65,0,path.EndsWith("LeftUpLeg")?-28:28);
                        else if(path.EndsWith("/LeftLeg")||path.EndsWith("/RightLeg"))extra=new Vector3(-95,0,0);
                        else if(path.EndsWith("Spine"))extra=new Vector3(name=="MountedCharge"?22:name=="MountedGroggy"?30:8,0,0);
                        if(name=="Mount")
                        {
                            float seat=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.85f,t));extra*=seat;
                            // 왼발 디딤 → 상체 들어 올리기 → 오른다리 넘기기 → 안장에 앉기.
                            if(path.EndsWith("LeftUpLeg"))extra.x+=45*Mathf.Sin(Mathf.Clamp01(t/.55f)*Mathf.PI);
                            if(path.EndsWith("RightUpLeg"))extra.z+=65*Mathf.Sin(Mathf.InverseLerp(.3f,.85f,t)*Mathf.PI);
                            if(path.EndsWith("Spine"))extra+=new Vector3(22,0,-16)*Mathf.Sin(t*Mathf.PI);
                            if(path.EndsWith("LeftArm"))extra=new Vector3(0,0,-45*Mathf.Sin(t*Mathf.PI));
                        }
                    }
                    if(name=="Kneel"||name=="StandUp")
                    {
                        float w=name=="Kneel"?Mathf.SmoothStep(0,1,t):1-Mathf.SmoothStep(0,1,t);
                        if(path.EndsWith("RightUpLeg"))extra=new Vector3(-55*w,0,0);
                        if(path.EndsWith("/RightLeg"))extra=new Vector3(100*w,0,0);
                        if(path.EndsWith("/LeftLeg"))extra=new Vector3(45*w,0,0);
                        if(path.EndsWith("Spine"))extra=new Vector3(25*w,0,0);
                    }
                    q=q*Quaternion.Euler(extra);for(int k=0;k<4;k++)dst[k].AddKey(t*duration,q[k]);
                }
                for(int k=0;k<4;k++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[k]),dst[k]);
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);Save(clip,name);
        }
        static Quaternion ReadRotation(AnimationClip clip,string path,float time)
        {
            var values=new float[4];for(int i=0;i<4;i++)values[i]=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[i])).Evaluate(time);
            return new Quaternion(values[0],values[1],values[2],values[3]).normalized;
        }
        static Quaternion StableRotation(string path,Quaternion value,Quaternion rest,bool mounted)
        {
            string bone=path.Split('/').Last();Vector3 d=(Quaternion.Inverse(rest)*value).eulerAngles;
            d=new Vector3(Mathf.DeltaAngle(0,d.x),Mathf.DeltaAngle(0,d.y),Mathf.DeltaAngle(0,d.z));
            // AI의 정면과 다른 골반 회전은 제거하고, 허리 여러 마디의 과도한 비틀림을 제한합니다.
            if(bone=="Root")return Quaternion.identity;
            if(bone=="Hips")return rest*Quaternion.Euler(mounted?0:Mathf.Clamp(d.x,-8,12),0,mounted?0:Mathf.Clamp(d.z,-5,5));
            if(bone.StartsWith("Spine"))return rest*Quaternion.Euler(Mathf.Clamp(d.x,-8,12),Mathf.Clamp(d.y,-4,4),Mathf.Clamp(d.z,-5,5));
            if(bone=="Neck"||bone=="Head")return rest*Quaternion.Euler(Mathf.Clamp(d.x,-12,12),Mathf.Clamp(d.y,-8,8),Mathf.Clamp(d.z,-8,8));
            if(bone.EndsWith("Shoulder"))return Quaternion.RotateTowards(rest,value,18);
            if(bone.EndsWith("Hand"))return Quaternion.RotateTowards(rest,value,35);
            if(bone.EndsWith("ForeArm"))return Quaternion.RotateTowards(rest,value,100);
            if(bone.EndsWith("Arm"))return Quaternion.RotateTowards(rest,value,110);
            if(bone.EndsWith("UpLeg"))return mounted?rest:Quaternion.RotateTowards(rest,value,18);
            if(bone.EndsWith("Leg"))return mounted?rest:Quaternion.RotateTowards(rest,value,24);
            if(bone.EndsWith("Foot"))return mounted?rest:Quaternion.RotateTowards(rest,value,15);
            return value;
        }
    }
}
#endif
