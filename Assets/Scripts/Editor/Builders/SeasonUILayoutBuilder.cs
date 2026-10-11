using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace DogLife.EditorTools
{
    public static class SeasonUILayoutBuilder
    {
        // Only layout properties are copied; scene-specific player references and content stay intact.
        public static string ApplyFallLayout(bool apply=true)
        {
            const string reference="Assets/Scenes/Stage3_Fall.unity";
            var source=SceneManager.GetSceneByPath(reference);bool opened=!source.isLoaded;
            if(opened)source=EditorSceneManager.OpenScene(reference,OpenSceneMode.Additive);
            var canvases=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).Where(c=>c.renderMode!=RenderMode.WorldSpace).ToArray();
            var report=new StringBuilder();
            try
            {
                foreach(var file in new[]{"Stage1_Spring","Stage3_Summer","Stage4_Winter","MonsterTest"})
                {
                    string path="Assets/Scenes/"+file+".unity";var scene=SceneManager.GetSceneByPath(path);bool temporary=!scene.isLoaded;
                    if(temporary)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try
                    {
                        if(scene.isDirty)throw new System.InvalidOperationException("Unsaved scene: "+path);
                        int changed=0;
                        var targets=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).ToArray();
                        foreach(var src in canvases)
                        {
                            var dst=targets.FirstOrDefault(c=>c.name==src.name);if(!dst)continue;
                            var a=src.GetComponent<CanvasScaler>();var b=dst.GetComponent<CanvasScaler>();
                            if(a&&b&&(a.referenceResolution!=b.referenceResolution||a.uiScaleMode!=b.uiScaleMode||a.matchWidthOrHeight!=b.matchWidthOrHeight||a.screenMatchMode!=b.screenMatchMode))
                            {changed++;if(apply){b.uiScaleMode=a.uiScaleMode;b.referenceResolution=a.referenceResolution;b.matchWidthOrHeight=a.matchWidthOrHeight;b.screenMatchMode=a.screenMatchMode;EditorUtility.SetDirty(b);}}
                            foreach(var r in src.GetComponentsInChildren<RectTransform>(true))
                            {
                                if(r==src.transform)continue;
                                string relative=AnimationUtility.CalculateTransformPath(r,src.transform);
                                var t=dst.transform.Find(relative) as RectTransform;if(!t)continue;
                                if(t.anchorMin!=r.anchorMin||t.anchorMax!=r.anchorMax||t.pivot!=r.pivot||t.sizeDelta!=r.sizeDelta||t.anchoredPosition!=r.anchoredPosition||t.localScale!=r.localScale)
                                {
                                    changed++;report.AppendLine(file+": "+src.name+"/"+relative);
                                    if(apply){t.anchorMin=r.anchorMin;t.anchorMax=r.anchorMax;t.pivot=r.pivot;t.sizeDelta=r.sizeDelta;t.anchoredPosition=r.anchoredPosition;t.localScale=r.localScale;EditorUtility.SetDirty(t);}
                                }
                                var st=r.GetComponent<Text>();var dt=t.GetComponent<Text>();
                                if(st&&dt&&(st.fontSize!=dt.fontSize||st.resizeTextForBestFit!=dt.resizeTextForBestFit||st.resizeTextMinSize!=dt.resizeTextMinSize||st.resizeTextMaxSize!=dt.resizeTextMaxSize))
                                {changed++;if(apply){dt.fontSize=st.fontSize;dt.resizeTextForBestFit=st.resizeTextForBestFit;dt.resizeTextMinSize=st.resizeTextMinSize;dt.resizeTextMaxSize=st.resizeTextMaxSize;EditorUtility.SetDirty(dt);}}
                            }
                        }
                        if(changed>0&&apply){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                        report.AppendLine(file+" differences: "+changed);
                    }
                    finally{if(temporary)EditorSceneManager.CloseScene(scene,true);}
                }
            }
            finally{if(opened)EditorSceneManager.CloseScene(source,true);}
            return report.ToString();
        }
    }
}
