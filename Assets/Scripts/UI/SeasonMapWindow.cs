using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // UI는 Hierarchy에서 제작하며 지도는 게임 입력과 시간을 변경하지 않습니다.
    public sealed class SeasonMapWindow : MonoBehaviour
    {
        public GameObject panel;
        public ForestRoomStreaming streaming;
        public Text title, subtitle, location, progress, hint;
        public SeasonMapLayout[] layouts;
        static SeasonMapWindow instance;
        static bool mapDiscovered;
        public static bool IsOpen => instance && instance.panel && instance.panel.activeSelf;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { instance = null; mapDiscovered = false; }
        void Awake()
        {
            instance = this; if (panel) panel.SetActive(false);
            if(hint)
            {
                var r=hint.rectTransform;var canvas=hint.canvas;
                if(canvas)r.SetParent(canvas.transform,false);
                r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;
                r.anchoredPosition=new Vector2(28,28);r.sizeDelta=new Vector2(360,48);
                hint.alignment=TextAnchor.MiddleLeft;hint.fontSize=21;hint.color=AdventureUI.Cream;
                hint.raycastTarget=false;hint.resizeTextForBestFit=false;
                var shadow=hint.GetComponent<Shadow>()??hint.gameObject.AddComponent<Shadow>();
                shadow.effectColor=new Color(.06f,.035f,.02f,.95f);shadow.effectDistance=new Vector2(1,-2);
            }
        }
        // 다른 메뉴의 입력 처리가 끝난 뒤 누르고 있는 Tab 상태만 확인합니다.
        void LateUpdate()
        {
            bool available = streaming && streaming.Ready && !BossCinematicUI.Hidden &&
                !SkillWindow.BlocksGameplayInput && !StoryBook.IsOpen && !SeasonLoadingScreen.IsLoading && !PlayerDeathSequence.IsActive && Time.timeScale > 0 &&
                streaming.director && !streaming.director.hud.health.Dead;
            if (hint) {hint.text=mapDiscovered?"Tab · 지도":"Tab 길게 누르기 · 계절 지도";hint.gameObject.SetActive(available && !Input.GetKey(KeyCode.Tab));}
            SetOpen(available && Input.GetKey(KeyCode.Tab));
            if (IsOpen) Refresh();
        }
        public void SetOpen(bool value)
        {
            if (!panel || panel.activeSelf == value) return;
            panel.SetActive(value);
            if(value)mapDiscovered=true;
            if (value) Refresh();
        }
        void OnDisable() { SetOpen(false); }
        void OnDestroy() { if (instance == this) instance = null; }
        public void Refresh()
        {
            if (!streaming || streaming.roomScenePaths == null || streaming.roomScenePaths.Length == 0) return;
            string path = streaming.roomScenePaths[0];
            int season = path.Contains("Summer") ? 1 : path.Contains("Autumn") || path.Contains("Fall") ? 2 : path.Contains("Winter") ? 3 : 0;
            if (layouts == null || season >= layouts.Length || !layouts[season]) return;
            for (int i = 0; i < layouts.Length; i++) if (layouts[i]) layouts[i].gameObject.SetActive(i == season);
            var layout = layouts[season];
            title.text = layout.seasonTitle; title.color = layout.accent;
            subtitle.text = layout.seasonCode + "  /  SEASON MAP";
            int cleared = 0, count = streaming.roomScenePaths.Length;
            for (int i = 0; i < count; i++) if (streaming.WasCleared(i)) cleared++;
            progress.text = "전체 " + count + "개 방    /    클리어 " + cleared + "개";
            int passage = streaming.MapPassageIndex;
            location.text = passage >= 0 ? "현재 위치  ·  " + (passage + 1) + " — " + (passage + 2) + "번 방 사이 통로" :
                "현재 위치  ·  " + (streaming.CurrentRoom + 1) + "번 방";
            location.color = layout.accent;
            layout.Refresh(streaming);
        }
    }
}
