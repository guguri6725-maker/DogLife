using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // 선택창과 버튼 이벤트는 공통 계절 프리팹의 Hierarchy/Inspector에 저장합니다.
    public sealed class SeasonDemoWeaponChoice : MonoBehaviour
    {
        public GameObject panel;
        public InventoryWeapon[] choices;
        public StoryPanelGraphic[] cards;
        public Button confirmButton;
        public Text selectionLabel;
        static string pendingScene;
        public static bool IsOpen { get; private set; }
        PlayerInventory inventory;
        int selected=-1;
        bool opened;
        float savedScale;
        CursorLockMode savedCursor;
        bool savedCursorVisible;
        public static void Prepare(string scene) { pendingScene=scene; RunEconomy.ClearCarry(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState(){pendingScene=null;IsOpen=false;}
        void Awake(){if(panel)panel.SetActive(false);}
        IEnumerator Start()
        {
            if(pendingScene!=gameObject.scene.path)yield break;
            pendingScene=null;
            // 플레이어와 방의 초기화를 마친 뒤 시작 무기를 교체합니다.
            yield return null;
            while(SeasonLoadingScreen.IsLoading)yield return null;
            var director=FindObjectsByType<StageOneDirector>(FindObjectsSortMode.None).FirstOrDefault(d=>d.gameObject.scene==gameObject.scene);
            while(director&&director.streaming&&(!director.streaming.Ready||director.streaming.Busy))yield return null;
            inventory=FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None).FirstOrDefault(p=>p.gameObject.scene==gameObject.scene);
            if(!inventory||!panel||choices==null||choices.Length!=6){Debug.LogError("계절 체험 무기 선택 연결을 확인하세요.",this);yield break;}
            inventory.RestoreRunInventory(new InventoryWeapon[0],-1);
            CombatFeedback.CancelGlobalHitSlow();
            savedScale=Time.timeScale;savedCursor=Cursor.lockState;savedCursorVisible=Cursor.visible;
            opened=true;IsOpen=true;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            selected=-1;confirmButton.interactable=false;panel.SetActive(true);
        }
        public void Select(int index)
        {
            if(!opened||index<0||index>=choices.Length)return;
            var item=choices[index];
            if(!item||item.weaponForm!=InventoryWeapon.WeaponForm.Basic||item.boneOrigin!=InventoryWeapon.BoneOrigin.Dog)return;
            selected=index;
            for(int i=0;i<cards.Length;i++)if(cards[i]){cards[i].accent=i==index;cards[i].SetAllDirty();}
            selectionLabel.text=item.displayName+" · 이 무기로 시작합니다";
            confirmButton.interactable=true;
        }
        public void Confirm()
        {
            if(!opened||selected<0||!inventory)return;
            // 기본 지급 검을 포함한 기존 목록을 대체하고 선택한 한 개만 1번 칸에 장착합니다.
            inventory.RestoreRunInventory(new[]{choices[selected]},0,new[]{0,-1},0);
            var economy=RunEconomy.Instance;
            if(economy&&economy.inventory==inventory)SeasonRunSave.RecordEntry(economy);
            Close();
        }
        void Close()
        {
            if(!opened)return;
            opened=false;IsOpen=false;if(panel)panel.SetActive(false);
            if(Mathf.Approximately(Time.timeScale,0))Time.timeScale=savedScale;
            Cursor.lockState=savedCursor;Cursor.visible=savedCursorVisible;
        }
        void OnDisable(){Close();}
    }
}
