#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    // Blender에서 실제 메시와 검을 보며 고친 본 회전을 Unity 클립으로 왕복합니다.
    public static class FrozenKnightBlenderBridge
    {
        const string Folder="Assets/Art/FrozenKnight/Animations/",Exchange="Temp/FrozenKnight/";
        [Serializable] public class Bone {public string name,path;public int parent;public Vector3 p,s;public Quaternion q;}
        [Serializable] public class Motion {public string name;public float duration;public int frames;public Vector3[] positions,scales;public Quaternion[] rotations;}
        [Serializable] public class Skin {public Vector3[] vertices;public int[] triangles,indices;public float[] weights;}
        [Serializable] public class Payload {public Bone[] bones;public Motion[] motions;public Skin skin;public Vector3[] sword;public int[] swordTriangles;public int hand;public Vector3 grip,tip;}
        public static string Export(bool cinematic=false,bool riding=false)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/FrozenKnight.prefab");
            var rig=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight").transform;
            var transforms=rig.GetComponentsInChildren<Transform>(true).Where(t=>AnimationUtility.CalculateTransformPath(t,rig).StartsWith("skel/")&&!t.name.StartsWith("Sword")&&!t.GetComponent<MeshFilter>()).ToArray();
            var data=new Payload{bones=transforms.Select(t=>new Bone{name=t.name,path=AnimationUtility.CalculateTransformPath(t,rig),parent=Array.IndexOf(transforms,t.parent),p=t.localPosition,q=t.localRotation,s=t.localScale}).ToArray()};
            var sources=cinematic?new[]{"SitUp","Idle","Mount","MountedIdle","Walk"}:new[]{"Idle","Walk","IceWallFull","PushUpper","Roar","FrostChannel","Spin3Sword"};
            // 승마 수정은 현재 사용 중인 탑승 클립을 함께 전달해 자세가 끊기지 않게 합니다.
            if(riding)sources=new[]{"MountedIdle","MountedCharge","MountedReady","MountedStompReady","MountedStompImpact","MountedGroggy","MountedCommand","MountedBackDash","MountedDeath","IceWallMounted","IceSwordMounted","SpearThrow","SpearRelease","Mount","CinematicMount"};
            data.motions=sources.Select(name=>
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(name=="SitUp"?"Assets/VARCO3DImports/WinterBoss/SitUp.anim":name=="Spin3Sword"?"Assets/VARCO3DImports/WinterBoss/3타.anim":Folder+name+".anim");
                int frames=Mathf.RoundToInt(clip.length*30)+1,count=frames*data.bones.Length;
                var motion=new Motion{name=name,duration=clip.length,frames=frames,positions=new Vector3[count],rotations=new Quaternion[count],scales=new Vector3[count]};
                for(int b=0;b<data.bones.Length;b++)
                {
                    var bone=data.bones[b];var pc=new AnimationCurve[3];var qc=new AnimationCurve[4];var sc=new AnimationCurve[3];
                    for(int k=0;k<3;k++){pc[k]=Curve(clip,bone.path,"m_LocalPosition."+"xyz"[k]);sc[k]=Curve(clip,bone.path,"m_LocalScale."+"xyz"[k]);}
                    for(int k=0;k<4;k++)qc[k]=Curve(clip,bone.path,"m_LocalRotation."+"xyzw"[k]);
                    for(int f=0;f<frames;f++)
                    {
                        float t=clip.length*f/(frames-1);int i=f*data.bones.Length+b;var p=bone.p;var q=bone.q;var s=bone.s;
                        for(int k=0;k<3;k++){if(pc[k]!=null)p[k]=pc[k].Evaluate(t);if(sc[k]!=null)s[k]=sc[k].Evaluate(t);}
                        for(int k=0;k<4;k++)if(qc[k]!=null)q[k]=qc[k].Evaluate(t);
                        motion.positions[i]=p;motion.rotations[i]=q.normalized;motion.scales[i]=s;
                    }
                }
                return motion;
            }).ToArray();
            var renderer=rig.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=renderer.sharedMesh;
            data.skin=new Skin{vertices=mesh.vertices.Select(v=>rig.InverseTransformPoint(renderer.transform.TransformPoint(v))).ToArray(),triangles=mesh.triangles,indices=new int[mesh.vertexCount*4],weights=new float[mesh.vertexCount*4]};
            var weights=mesh.boneWeights;
            for(int i=0;i<weights.Length;i++){var w=weights[i];var bi=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var bw=new[]{w.weight0,w.weight1,w.weight2,w.weight3};for(int k=0;k<4;k++){data.skin.indices[i*4+k]=Array.IndexOf(transforms,renderer.bones[bi[k]]);data.skin.weights[i*4+k]=bw[k];}}
            var hand=transforms.First(t=>t.name=="RightHand");data.hand=Array.IndexOf(transforms,hand);
            var sword=rig.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Sword");var filter=sword.GetComponentInChildren<MeshFilter>();
            data.sword=filter.sharedMesh.vertices.Select(v=>hand.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();data.swordTriangles=filter.sharedMesh.triangles;
            data.grip=hand.InverseTransformPoint(sword.TransformPoint(new Vector3(0,.84f,0)));data.tip=hand.InverseTransformPoint(sword.TransformPoint(new Vector3(0,.02f,0)));
            Directory.CreateDirectory(Exchange);File.WriteAllText(Exchange+(riding?"riding_input.json":cinematic?"cinematic_input.json":"blender_input.json"),JsonUtility.ToJson(data));return "Exported "+data.bones.Length+" bones, "+mesh.vertexCount+" vertices and "+sources.Length+" motions";
        }
        static AnimationCurve Curve(AnimationClip c,string p,string property)=>AnimationUtility.GetEditorCurve(c,EditorCurveBinding.FloatCurve(p,typeof(Transform),property));
        public static string Import(bool cinematic=false,bool riding=false,bool mounting=false)
        {
            var data=JsonUtility.FromJson<Payload>(File.ReadAllText(Exchange+(mounting?"mount_grip_output.json":riding?"riding_output.json":cinematic?"cinematic_output.json":"blender_output.json")));
            foreach(var motion in data.motions)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+motion.name+".anim");bool fresh=!clip;if(fresh)clip=new AnimationClip{name=motion.name,frameRate=30};
                // 과거 클립에 남아 있는 판정 오브젝트 경로도 제거합니다.
                if(riding)foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(binding=>binding.path.Contains("FinalSwordHitArea")))AnimationUtility.SetEditorCurve(clip,binding,null);
                for(int b=0;b<data.bones.Length;b++)for(int kind=0;kind<3;kind++)for(int k=0;k<(kind==1?4:3);k++)
                {
                    // 판정용 자식은 뼈가 아니므로 애니메이션으로 위치를 덮어쓰지 않습니다.
                    if(riding&&data.bones[b].name=="FinalSwordHitArea")
                    {
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(data.bones[b].path,typeof(Transform),(kind==0?"m_LocalPosition.":kind==1?"m_LocalRotation.":"m_LocalScale.")+"xyzw"[k]),null);
                        continue;
                    }
                    var curve=new AnimationCurve();for(int f=0;f<motion.frames;f++){int i=f*data.bones.Length+b;float v=kind==0?motion.positions[i][k]:kind==1?motion.rotations[i][k]:motion.scales[i][k];curve.AddKey(motion.duration*f/(motion.frames-1),v);}
                    // 변화 없는 채널은 양 끝 키만 남겨 클립 용량을 줄입니다.
                    if(riding&&curve.length>2)
                    {
                        var keys=curve.keys;float value=keys[0].value;
                        if(keys.All(key=>Mathf.Abs(key.value-value)<0.000001f))curve=new AnimationCurve(keys[0],keys[keys.Length-1]);
                    }
                    for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(data.bones[b].path,typeof(Transform),(kind==0?"m_LocalPosition.":kind==1?"m_LocalRotation.":"m_LocalScale.")+"xyzw"[k]),curve);
                }
                clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);
                // 기존 클립의 반복 설정은 보존하고 새 승마 보행만 반복으로 만듭니다.
                if(riding){if(motion.name=="MountedWalk")settings.loopTime=true;}
                else settings.loopTime=motion.name=="Idle"||motion.name=="Walk"||motion.name=="ThroneIdle";
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                if(fresh)AssetDatabase.CreateAsset(clip,Folder+motion.name+".anim");else EditorUtility.SetDirty(clip);
            }
            AssetDatabase.SaveAssets();return "Imported Blender sword-handling motion edits";
        }
    }
}
#endif
