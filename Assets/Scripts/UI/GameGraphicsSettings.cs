using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DogLife
{
    [Serializable]
    public sealed class GraphicsPreferences
    {
        public string monitor = "";
        public int width = 1920, height = 1080, mode = 0, quality = 1, fps = 60, scale = 100;
        public bool vsync;
        public GraphicsPreferences Copy() { return (GraphicsPreferences)MemberwiseClone(); }
    }

    public static class GameGraphicsSettings
    {
        const string Key = "DogLife.Graphics.v1";
        public static GraphicsPreferences Current { get; private set; }
        public static readonly int[] Scales = { 50, 67, 75, 85, 100 };
        public static List<DisplayInfo> Displays()
        {
            var list = new List<DisplayInfo>();
#if UNITY_STANDALONE || UNITY_EDITOR
            Screen.GetDisplayLayout(list);
#endif
            return list;
        }
        public static int MonitorIndex(GraphicsPreferences value, List<DisplayInfo> list)
        {
            int index = list.FindIndex(d => d.name == value.monitor); return Mathf.Max(0, index);
        }
        public static List<Vector2Int> Resolutions(GraphicsPreferences value)
        {
            var displays = Displays();
            int w = displays.Count > 0 ? displays[MonitorIndex(value, displays)].width : 1920;
            int h = displays.Count > 0 ? displays[MonitorIndex(value, displays)].height : 1080;
            var result = new List<Vector2Int>();
            var common = new[] { new Vector2Int(1280,720), new Vector2Int(1600,900), new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(3440,1440), new Vector2Int(3840,2160) };
            foreach (var size in common) if (size.x <= w && size.y <= h) result.Add(size);
            var native = new Vector2Int(w,h); if (!result.Contains(native)) result.Add(native);
            result.Sort((a,b) => (a.x*a.y).CompareTo(b.x*b.y)); return result;
        }
        public static GraphicsPreferences Load()
        {
            GraphicsPreferences p = null;
            try { if (PlayerPrefs.HasKey(Key)) p = JsonUtility.FromJson<GraphicsPreferences>(PlayerPrefs.GetString(Key)); } catch (Exception) { }
            if (p == null) p = new GraphicsPreferences { fps = PlayerFramePacing.SelectedLimit };
            p.mode = Mathf.Clamp(p.mode,0,2); p.quality = Mathf.Clamp(p.quality,0,2);
            if (Array.IndexOf(PlayerFramePacing.Limits,p.fps)<0) p.fps=60;
            if (Array.IndexOf(Scales,p.scale)<0) p.scale=100;
            var displays=Displays(); if(displays.Count>0)p.monitor=displays[MonitorIndex(p,displays)].name;
            var resolutions=Resolutions(p);
            if(!resolutions.Contains(new Vector2Int(p.width,p.height))) { var size=resolutions[0]; foreach(var r in resolutions)if(r.x<=1920&&r.y<=1080)size=r; p.width=size.x;p.height=size.y; }
            return p;
        }
        public static void ApplyGraphics(GraphicsPreferences p)
        {
            // Keep the project's materials and shaders; scale the expensive quality features.
            QualitySettings.SetQualityLevel(p.quality == 0 ? 0 : p.quality == 1 ? 2 : QualitySettings.names.Length-1, true);
            QualitySettings.shadowDistance = p.quality == 0 ? 20 : p.quality == 1 ? 35 : 45;
            QualitySettings.shadowCascades = p.quality == 0 ? 0 : 2;
            QualitySettings.lodBias = p.quality == 0 ? .6f : p.quality == 1 ? .85f : 1f;
            QualitySettings.antiAliasing = p.quality == 0 ? 0 : p.quality == 1 ? 2 : 4;
            PlayerFramePacing.Apply(p.fps,false);
            PlayerFramePacing.SetVSync(p.vsync);
            WorldRenderScale.Scale=p.scale/100f;
        }
        public static IEnumerator ApplyDisplay(GraphicsPreferences p)
        {
            if(Application.isEditor)yield break;
            var displays=Displays();
            if(displays.Count>0)
            {
                var display=displays[MonitorIndex(p,displays)];
                if(Screen.mainWindowDisplayInfo.name!=display.name)
                {
                    Screen.SetResolution(Mathf.Min(p.width,1280),Mathf.Min(p.height,720),FullScreenMode.Windowed);
                    yield return null;
                    var move=Screen.MoveMainWindowTo(display,new Vector2Int(40,40));
                    if(move!=null)yield return move;
                }
                var mode=p.mode==0?FullScreenMode.Windowed:p.mode==1?FullScreenMode.FullScreenWindow:FullScreenMode.ExclusiveFullScreen;
                Screen.SetResolution(p.width,p.height,mode,display.refreshRate);
                yield return null;
            }
        }
        public static void Commit(GraphicsPreferences p)
        {
            Current=p.Copy(); PlayerPrefs.SetString(Key,JsonUtility.ToJson(Current));
            PlayerPrefs.SetInt("DogLife.FrameRateLimit",p.fps); PlayerPrefs.Save();
        }
        public static void SetCurrent(GraphicsPreferences p) { Current=p.Copy(); }
        public static bool DisplayChanged(GraphicsPreferences a,GraphicsPreferences b)
        { return a.monitor!=b.monitor||a.width!=b.width||a.height!=b.height||a.mode!=b.mode; }
    }
}

