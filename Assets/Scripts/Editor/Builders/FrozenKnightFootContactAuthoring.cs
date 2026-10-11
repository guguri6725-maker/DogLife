#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class FrozenKnightFootContactAuthoring
    {
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");
            const string prefab="Assets/Prefabs/monster/FrozenKnight.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefab);
            var report=new System.Text.StringBuilder();
            try
            {
                var animator=root.GetComponentsInChildren<Animator>(true).First(a=>a.name=="Knight");
                if(!animator.GetComponent<FrozenKnightFootstepRelay>())animator.gameObject.AddComponent<FrozenKnightFootstepRelay>();
                PrefabUtility.SaveAsPrefabAsset(root,prefab);
                foreach(string name in new[]{"Walk","UnarmedWalk"})
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/FrozenKnight/Animations/"+name+".anim");
                    var events=AnimationUtility.GetAnimationEvents(clip).Where(e=>e.functionName!="KnightFootContact").ToList();
                    for(int side=0;side<2;side++)
                    {
                        var foot=animator.GetComponentsInChildren<Transform>(true).First(t=>t.name==(side==0?"LeftFoot":"RightFoot"));
                        int count=Mathf.CeilToInt(clip.length*120);var height=new float[count];
                        for(int i=0;i<count;i++){clip.SampleAnimation(animator.gameObject,i*clip.length/count);height[i]=animator.transform.InverseTransformPoint(foot.position).y;}
                        float low=height.Min(),range=height.Max()-low;
                        // 낮은 구간에서 재발동하지 않고, 충분히 들어 올렸다 내려올 때만 접지합니다.
                        bool raised=height[count-1]>low+range*.35f;
                        for(int i=0;i<count;i++)
                        {
                            if(height[i]>low+range*.5f)raised=true;
                            if(!raised||height[i]>low+range*.12f)continue;
                            raised=false;float time=i*clip.length/count;
                            events.Add(new AnimationEvent{functionName="KnightFootContact",intParameter=side,time=time});
                            report.AppendLine(name+" "+foot.name+" "+time.ToString("F3"));
                        }
                    }
                    AnimationUtility.SetAnimationEvents(clip,events.OrderBy(e=>e.time).ToArray());EditorUtility.SetDirty(clip);
                }
                FrozenKnightAnimationAuthoring.BakeArmorMesh();AssetDatabase.SaveAssets();
                return report.ToString();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
#endif
