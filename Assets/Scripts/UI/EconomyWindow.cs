using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // 미리 배치한 UI의 내용만 갱신합니다. 버튼 이벤트는 Inspector에 직접 연결합니다.
    public sealed class EconomyWindow : MonoBehaviour
    {
        public RunEconomy wallet;
        public GameObject panel,shopPanel,fusionPanel;
        public Text goldLabel,prompt,title,message,firstLabel,secondLabel;
        public Image firstIcon,secondIcon;
        public Text[] itemLabels;
        public Image[] itemIcons;
        public Button[] buyButtons;
        public Button fuseButton;
        public WeaponFusionSequence fusionSequencePrefab;
        public GameObject fusionPresentation;
        public Text fusionStatus,fusionActionLabel;
        WeaponFusionSequence sequence;
        bool opened;
        public static EconomyWindow Instance {get;private set;}
        public static bool IsOpen=>Instance&&Instance.opened;
        static int consumed=-1;
        public static bool BlocksInput=>IsOpen||consumed==Time.frameCount;
        EconomyStation station,nearest;
        string first,second;
        float previousScale;CursorLockMode previousCursor;bool previousVisible;
        float scanAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState(){Instance=null;consumed=-1;}
        void Awake(){Instance=this;panel.SetActive(false);if(fusionPresentation)fusionPresentation.SetActive(false);}
        void OnDisable(){if(opened)Close();}
        void OnDestroy(){if(Instance!=this)return;if(IsOpen)Close();Instance=null;}
        void Update()
        {
            if(!wallet||!wallet.inventory)return;
            var health=wallet.inventory.GetComponent<CombatHealth>();
            if(health&&health.Dead){if(IsOpen)Close();prompt.text="";return;}
            goldLabel.text="G  "+wallet.Gold;
            if(IsOpen)
            {
                if(sequence){fusionStatus.text=sequence.Status;fusionActionLabel.text=sequence.Completed?"확인":"건너뛰기";if(Input.GetKeyDown(KeyCode.Escape))FusionAction();}
                else if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.F))Close();
                return;
            }
            if(SkillWindow.IsOpen||GameSettingsWindow.IsOpen||StoryBook.BlocksInput){prompt.text="";return;}
            if(Time.unscaledTime>=scanAt)
            {
                scanAt=Time.unscaledTime+.2f;nearest=null;float best=float.MaxValue;
                foreach(var s in FindObjectsByType<EconomyStation>(FindObjectsSortMode.None))
                {float d=Vector3.Distance(s.transform.position,wallet.inventory.transform.position);if(s.InReach(wallet.inventory)&&d<best){best=d;nearest=s;}}
            }
            prompt.text=nearest?(nearest.kind==EconomyStation.StationKind.Shop?"[F] 뼈 상점":"[F] 무기 합성 · 방당 1회"):"";
            if(nearest&&Input.GetKeyDown(KeyCode.F))Open(nearest);
        }
        public void Open(EconomyStation target)
        {
            if(IsOpen||SkillWindow.IsOpen||GameSettingsWindow.IsOpen||StoryBook.BlocksInput||!target||!target.InReach(wallet.inventory))return;
            station=target;previousScale=Time.timeScale;previousCursor=Cursor.lockState;previousVisible=Cursor.visible;
            opened=true;
            GameAudio.EnterEconomyMenu();
            Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;consumed=Time.frameCount;
            panel.SetActive(true);prompt.text="";bool shop=station.kind==EconomyStation.StationKind.Shop;
            shopPanel.SetActive(shop);fusionPanel.SetActive(!shop);title.text=shop?"뼈 상점":"영혼의 합성";message.text=shop?"구매한 무기는 가방에 들어갑니다.":"같은 계열의 일반 무기 2개 · 비용 없음 · 방당 1회";
            first=null;second=null;Cycle(true,1);Cycle(false,1);Refresh();
            GameAudio.Play("UI_Window_Open",Vector3.zero,.6f,ui:true);
            for(int i=0;i<buyButtons.Length;i++)if(!buyButtons[i].GetComponent<EconomyItemAudio>())buyButtons[i].gameObject.AddComponent<EconomyItemAudio>();
            if(fuseButton&&!fuseButton.GetComponent<EconomyItemAudio>())fuseButton.gameObject.AddComponent<EconomyItemAudio>().shopPreview=false;
        }
        public void Close()
        {if(!opened)return;opened=false;if(sequence){sequence.Release();Destroy(sequence.gameObject);sequence=null;}if(fusionPresentation)fusionPresentation.SetActive(false);panel.SetActive(false);Time.timeScale=previousScale;Cursor.lockState=previousCursor;Cursor.visible=previousVisible;consumed=Time.frameCount;station=null;GameAudio.Play("UI_Window_Close",Vector3.zero,.55f,ui:true);}
        public void Buy(int index){if(!station)return;string text;bool success=station.Buy(index,wallet,out text);message.text=text;Refresh();GameAudio.Play(success?"Shop_Purchase":"UI_Action_Denied",Vector3.zero,.7f,ui:true);}
        public void Fuse()
        {
            if(!station||sequence)return;
            var a=wallet.inventory.FindInstance(first);var b=wallet.inventory.FindInstance(second);
            string text;InventoryWeapon result;
            // 재료/결과 교환은 한 번만 확정하고 연출은 확정된 결과를 보여줍니다.
            bool success=station.Fuse(first,second,wallet,out text,out result);message.text=text;Refresh();
            if(!success){GameAudio.Play("UI_Action_Denied",Vector3.zero,.6f,ui:true);return;}
            if(!fusionSequencePrefab||!fusionPresentation){GameAudio.Play("Synthesis_Result",Vector3.zero,.8f,ui:true);return;}
            var focus=station.fusionFocus;
            sequence=Instantiate(fusionSequencePrefab,focus?focus.position:station.transform.position+Vector3.up*1.2f,Quaternion.identity);
            sequence.Begin(a,b,result);panel.SetActive(false);fusionPresentation.SetActive(true);
        }
        public void FusionAction()
        {
            if(!sequence)return;
            GameAudio.Play("UI_Button_Click",Vector3.zero,.45f,ui:true);
            if(!sequence.Completed){sequence.Skip();return;}
            sequence.Release();Destroy(sequence.gameObject);sequence=null;
            fusionPresentation.SetActive(false);panel.SetActive(true);Refresh();
        }
        public void NextFirst(){SelectMaterial(true,1);}public void PreviousFirst(){SelectMaterial(true,-1);}
        public void NextSecond(){SelectMaterial(false,1);}public void PreviousSecond(){SelectMaterial(false,-1);}
        void SelectMaterial(bool a,int direction)
        {
            string before=a?first:second;Cycle(a,direction);string after=a?first:second;Refresh();
            GameAudio.Play(before==after?"UI_Action_Denied":string.IsNullOrEmpty(after)?"Synthesis_Material_Remove":"Synthesis_Material_Add",Vector3.zero,.55f,ui:true);
        }
        void Cycle(bool a,int direction)
        {
            var inv=wallet.inventory;int count=inv.OwnedWeapons.Count,index=direction>0?-1:0;string current=a?first:second,other=a?second:first;
            for(int i=0;i<count;i++)if(inv.GetInstanceId(i)==current)index=i;
            for(int n=0;n<count;n++){index=(index+direction+count)%count;var w=inv.OwnedWeapons[index];string id=inv.GetInstanceId(index);if(w&&w.weaponForm==InventoryWeapon.WeaponForm.Basic&&id!=other){if(a)first=id;else second=id;return;}}
            if(a)first=null;else second=null;
        }
        void Refresh()
        {
            if(!station)return;
            for(int i=0;i<buyButtons.Length;i++)
            {
                bool exists=i<station.Items.Length;buyButtons[i].gameObject.SetActive(exists);if(!exists)continue;
                var w=station.Items[i];itemLabels[i].text=w?w.displayName+"\n"+station.price+" G":"판매 완료";itemIcons[i].sprite=w?w.icon:null;itemIcons[i].enabled=w&&w.icon;
                buyButtons[i].interactable=w&&wallet.Gold>=station.price;
            }
            var a=wallet.inventory.FindInstance(first);var b=wallet.inventory.FindInstance(second);
            firstLabel.text=a?a.displayName:"재료 없음";secondLabel.text=b?b.displayName:"재료 없음";firstIcon.sprite=a?a.icon:null;secondIcon.sprite=b?b.icon:null;firstIcon.enabled=a&&a.icon;secondIcon.enabled=b&&b.icon;
            fuseButton.interactable=!station.Used&&a&&b&&a.boneOrigin==b.boneOrigin&&first!=second;
        }
    }
}
