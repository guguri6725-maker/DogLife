using System;
using UnityEngine;

namespace DogLife
{
    [Serializable]
    public sealed class StoryPage
    {
        public Texture2D artwork;
        [TextArea(2, 6)] public string text;
        public Vector2 focus = new Vector2(.5f, .5f);
        [Min(3)] public float motionSeconds = 18;
        [Range(-1,.12f)] public float zoomOverride = -1;
        [Min(-1)] public float narrationDelayOverride = -1;
    }
    [Serializable]
    public sealed class StoryChapter
    {
        public string id, title, subtitle, sceneName;
        public AudioClip music;
        [TextArea] public string entrance;
        public Color accent = new Color(.84f, .67f, .42f);
        public StoryPage[] pages;
    }
    [CreateAssetMenu(menuName="Dog Life/Story Library")]
    public sealed class StoryLibrary : ScriptableObject
    {
        public Font font;
        public StoryChapter[] chapters;
        [Min(.1f)] public float fadeDuration = .85f;
        [Min(3)] public float automaticPageSeconds = 9;
        [Range(0, .12f)] public float imageZoom = .045f;
        [Min(0)] public float clearPause = 2;
        [Header("추억 연출 — 초 단위 / 일시정지 중에도 재생")]
        [Min(.1f)] public float pictureTransition = 1.25f;
        [Min(0)] public float narrationDelay = 1.4f;
        [Min(5)] public float charactersPerSecond = 22;
        [Min(.1f)] public float closingFade = .8f;
        [Min(0)] public float readingHold = 3;
        [Header("마지막 만남 — 정규화된 카르마 (-1 밝음 / +1 어두움)")]
        [Range(-1,0)] public float lifeThreshold=-.34f;
        [Range(0,1)] public float revengeThreshold=.34f;
        public string Ending(float karma,float maximum)
        {
            float ratio=karma/Mathf.Max(1,maximum);
            return ratio<=lifeThreshold?"life":ratio>=revengeThreshold?"revenge":"farewell";
        }
        public StoryChapter Find(string id) => Array.Find(chapters, c => c.id == id);
        public StoryChapter ForScene(string name) => Array.Find(chapters, c => !string.IsNullOrEmpty(c.sceneName) && c.sceneName == name);
    }
    // Separate from run data: beginning a new run never erases collected memories.
    public static class StoryProgress
    {
        const string Prefix="DogLife.Memories.v1.";
        public static bool Unlocked(string id) => PlayerPrefs.GetInt(Prefix+id,0)==1;
        public static bool Unlock(string id)
        {
            if(Unlocked(id))return false;
            PlayerPrefs.SetInt(Prefix+id,1);PlayerPrefs.Save();return true;
        }
    }
}
