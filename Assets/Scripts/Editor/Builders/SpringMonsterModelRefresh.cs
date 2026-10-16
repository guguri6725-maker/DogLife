using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DogLife.EditorTools
{
    // 새 FBX의 관절 위치를 반영하되 컨트롤러와 게임플레이 참조의 GUID는 유지합니다.
    public static class SpringMonsterModelRefresh
    {
        public static string RefreshAll()
        {
            int count=0;
            foreach(var name in new[]{"BlossomSkeleton","FloralDeer","SakuraRabbit","BlossomSpirit"})
            {
                string modelsFolder="Assets/Models/Monster/Spring/"+name+"/";
                string animFolder="Assets/Animations/Monster/Spring/"+name+"/";
                var clips=AssetDatabase.LoadAllAssetsAtPath(modelsFolder+name+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                foreach(var source in clips)
                {
                    string clipName=source.name.Split('|').Last();
                    string suffix=clipName.Contains("_")?clipName.Substring(clipName.LastIndexOf('_')+1):clipName;
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(animFolder+suffix+".anim");
                    if(!clip)continue;
                    var settings=AnimationUtility.GetAnimationClipSettings(clip);
                    var events=AnimationUtility.GetAnimationEvents(clip);
                    EditorUtility.CopySerialized(source,clip);clip.name=clipName;
                    var updated=AnimationUtility.GetAnimationClipSettings(clip);updated.loopTime=settings.loopTime;updated.loopBlend=settings.loopBlend;
                    AnimationUtility.SetAnimationClipSettings(clip,updated);AnimationUtility.SetAnimationEvents(clip,events);
                    EditorUtility.SetDirty(clip);count++;
                }
                string prefabPath="Assets/Prefabs/Monster/Spring/"+name+"/"+name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var animator=root.GetComponentInChildren<Animator>(true);
                    if(!animator||!animator.runtimeAnimatorController)throw new InvalidOperationException(name+": Animator missing");
                    foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if(!skin.sharedMesh||skin.bones.Any(b=>!b))throw new InvalidOperationException(name+": broken skin references");
                        var mesh=new Mesh();var bounds=new Bounds();bool first=true;
                        var basis=skin.rootBone?skin.rootBone:skin.transform;
                        try
                        {
                            foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                                for(int frame=0;frame<=24;frame++)
                                {
                                    clip.SampleAnimation(animator.gameObject,clip.length*frame/24f);
                                    skin.BakeMesh(mesh);var scale=skin.transform.lossyScale;var b=mesh.bounds;
                                    for(int corner=0;corner<8;corner++)
                                    {
                                        var p=new Vector3((corner&1)==0?b.min.x:b.max.x,(corner&2)==0?b.min.y:b.max.y,(corner&4)==0?b.min.z:b.max.z);
                                        p=basis.InverseTransformPoint(skin.transform.TransformPoint(new Vector3(p.x/scale.x,p.y/scale.y,p.z/scale.z)));
                                        if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                                    }
                                }
                            bounds.Expand(bounds.size*.06f);skin.localBounds=bounds;
                        }
                        finally { UnityEngine.Object.DestroyImmediate(mesh); }
                    }
                    // Sampling is only for culling bounds; restore imported bind transforms before saving.
                    var imported=AssetDatabase.LoadAssetAtPath<GameObject>(modelsFolder+name+".fbx");
                    foreach(var t in animator.GetComponentsInChildren<Transform>(true))
                    {
                        if(t==animator.transform)continue;
                        var path=AnimationUtility.CalculateTransformPath(t,animator.transform);var original=imported.transform.Find(path);
                        if(original){t.localPosition=original.localPosition;t.localRotation=original.localRotation;t.localScale=original.localScale;}
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            BlossomSkeletonVfxBuilder.Build();
            return "Updated "+count+" animation clips and four monster prefabs.";
        }
    }
}
