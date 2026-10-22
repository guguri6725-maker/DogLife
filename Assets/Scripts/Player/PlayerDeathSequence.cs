using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    public sealed class PlayerDeathSequence : MonoBehaviour
    {
        static PlayerDeathSequence active;
        public static bool IsActive=>active;
        public bool MenuVisible {get;private set;}
        [SerializeField,Range(0,1)] float deathSoundMoment=.7f;
        [SerializeField,Range(0,1)] float deathVolume=.75f,resultVolume=.5f;
        GameAudio.Handle deathSound,resultSound;
        bool deathSoundPlayed;
        void PlayDeathSound(){if(deathSoundPlayed)return;deathSoundPlayed=true;deathSound=GameAudio.Play("Player_Death",transform.position,deathVolume,ui:true,follow:transform,minimumInterval:0);}
        public static void Begin(CombatHealth health)
        {
            if(active)return;
            active=health.gameObject.AddComponent<PlayerDeathSequence>();
            active.StartCoroutine(active.Present());
        }
        IEnumerator Present()
        {
            if(EconomyWindow.IsOpen)EconomyWindow.Instance.Close();
            if(SkillWindow.IsOpen)SkillWindow.Instance.SetOpen(false);
            if(GameSettingsWindow.IsOpen)GameSettingsWindow.Instance.Close();
            var combat=GetComponent<BasicWeaponCombat>();if(combat){combat.CancelForDash();combat.enabled=false;}
            var inventory=GetComponent<PlayerInventory>();if(inventory)inventory.SetHeldVisible(false);
            var animator=GetComponentInChildren<Animator>();
            var clip=Resources.Load<AnimationClip>("PlayerDeath");
            Vector3 start=transform.position,grounded=start;
            var bodyCollider=GetComponent<Collider>();
            float footOffset=bodyCollider?start.y-bodyCollider.bounds.min.y:1;
            float nearest=float.MaxValue;
            foreach(var hit in Physics.RaycastAll(start+Vector3.up*.1f,Vector3.down,30,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.transform.IsChildOf(transform)||hit.collider.GetComponentInParent<CombatHealth>()||hit.normal.y<.6f)continue;
                if(hit.distance<nearest){nearest=hit.distance;grounded.y=Mathf.Min(start.y,hit.point.y+footOffset+.02f);}
            }
            CombatFeedback.CancelGlobalHitSlow();
            Time.timeScale=0;
            if(animator&&clip)
            {
                var bones=animator.GetComponentsInChildren<Transform>();
                var rotations=new Quaternion[bones.Length];var positions=new Vector3[bones.Length];
                for(int i=0;i<bones.Length;i++){rotations[i]=bones[i].localRotation;positions[i]=bones[i].localPosition;}
                animator.enabled=false;
                for(float time=0;time<clip.length;time+=Time.unscaledDeltaTime)
                {
                    if(time>=clip.length*deathSoundMoment)PlayDeathSound();
                    transform.position=Vector3.Lerp(start,grounded,Mathf.SmoothStep(0,1,time/.45f));
                    clip.SampleAnimation(animator.gameObject,time);
                    float blend=Mathf.SmoothStep(0,1,time/.22f);
                    if(blend<1)for(int i=0;i<bones.Length;i++){bones[i].localRotation=Quaternion.Slerp(rotations[i],bones[i].localRotation,blend);bones[i].localPosition=Vector3.Lerp(positions[i],bones[i].localPosition,blend);}
                    yield return null;
                }
                clip.SampleAnimation(animator.gameObject,clip.length);
            }
            else {PlayDeathSound();Debug.LogError("Player death animation is missing.",this);yield return new WaitForSecondsRealtime(.7f);}
            yield return new WaitForSecondsRealtime(.35f);
            ShowMenu();
        }
        void ShowMenu()
        {
            if(MenuVisible)return;
            resultSound=GameAudio.Play("Death_Result_Show",Vector3.zero,resultVolume,ui:true,follow:transform,minimumInterval:0);
            MenuVisible=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            var root=new GameObject("Journey Retry",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            var c=root.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=31000;
            var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
            var shade=AdventureUI.Rect("Shade",root.transform,Vector2.zero,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.gameObject.AddComponent<Image>().color=new Color(.035f,.022f,.016f,.64f);
            var card=AdventureUI.Rect("Retry Card",root.transform,Vector2.zero,new Vector2(800,390));AdventureUI.Surface(card.gameObject);
            var font=StoryBook.Instance?StoryBook.Instance.Library.font:Font.CreateDynamicFontFromOSFont("Malgun Gothic",28);
            AdventureUI.Label(card,"Heading","아직, 여정은 끝나지 않았어",new Vector2(0,110),new Vector2(720,70),36,font).color=AdventureUI.Gold;
            AdventureUI.Label(card,"Description","이 계절의 시작에서 다시 일어날 수 있어요.",new Vector2(0,28),new Vector2(720,60),24,font);
            AdventureUI.Button(card,"Retry","재도전",new Vector2(-177,-94),new Vector2(300,70),font,()=>SeasonRunSave.RestartSeason(gameObject.scene.path),true);
            AdventureUI.Button(card,"Title","타이틀",new Vector2(177,-94),new Vector2(300,70),font,()=>SeasonLoadingScreen.Load("Assets/Scenes/Title.unity"));
            AdventureUI.Theme(card.gameObject);
        }
        void OnDisable(){deathSound.Stop();resultSound.Stop();}
        void OnDestroy(){deathSound.Stop();resultSound.Stop();if(active==this)active=null;}
    }
}
