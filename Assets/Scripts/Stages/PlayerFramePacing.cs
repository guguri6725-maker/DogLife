using UnityEngine;
namespace DogLife
{
    public static class PlayerFramePacing
    {
        const string PreferenceKey="DogLife.FrameRateLimit";
        public static readonly int[] Limits={60,120,144,165,240,-1};
        public static int SelectedLimit { get; private set; }
        public static bool VSync { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            VSync=false;
            int saved=PlayerPrefs.GetInt(PreferenceKey,60);
            if(System.Array.IndexOf(Limits,saved)<0)saved=60;
            Apply(saved,false);
        }
        public static void SetVSync(bool enabled)
        { VSync=enabled; QualitySettings.vSyncCount=enabled?1:0; Application.targetFrameRate=VSync?-1:SelectedLimit; }
        public static void Apply(int limit,bool save=true)
        {
            if(System.Array.IndexOf(Limits,limit)<0)return;
            SelectedLimit=limit;SetVSync(VSync);
            if(save){PlayerPrefs.SetInt(PreferenceKey,limit);PlayerPrefs.Save();}
        }
    }
}
