using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // 유물 3장 중 1장을 고르는 전체 화면 카드. RelicShrine이 상호작용 시점에 띄웁니다.
    // 전부 코드로 만든 화면이라 씬/프리팹 작업 없이 동작합니다.
    public static class RelicDraftUI
    {
        static bool open;
        public static bool IsOpen => open;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset(){open=false;}
        // 전시회 빌드 기준: 발자국 해금 없이 전체 유물 목록에서 바로 뽑습니다.
        public static WeaponRelic[] PrepareChoices(GameObject player, RelicLoadout loadout)
        {

            var pool = Eligible(loadout);
            return pool.Count==0?null:PickChoices(pool,player.GetComponent<PlayerInventory>(),loadout,Mathf.Min(3,pool.Count));
        }
        static List<WeaponRelic> Eligible(RelicLoadout loadout){
            var pool=new List<WeaponRelic>();
            foreach(var relic in RelicCatalog.All)if(!loadout.Has(relic.id)||loadout.CanUpgrade(relic))pool.Add(relic);
            return pool;
        }
        // A failed preference roll chooses the complementary group, not the full pool.
        // If either group is empty, use the available group without losing a card.
        static List<WeaponRelic> Prefer(List<WeaponRelic> pool,System.Predicate<WeaponRelic> match,out bool preferred){
            var yes=pool.FindAll(match);var no=pool.FindAll(r=>!match(r));
            preferred=yes.Count>0&&(no.Count==0||Random.value<.7f);
            return preferred?yes:no;
        }
        static bool Matches(WeaponRelic relic,HashSet<InventoryWeapon.WeaponCategory> categories){
            foreach(var category in categories)if(relic.AppliesTo(category))return true;return false;
        }
        static WeaponRelic[] PickChoices(List<WeaponRelic> pool,PlayerInventory inventory,RelicLoadout loadout,int count){
            var owned=new HashSet<InventoryWeapon.WeaponCategory>();var synth=new HashSet<InventoryWeapon.WeaponCategory>();var learned=new HashSet<InventoryWeapon.WeaponCategory>();
            if(inventory)foreach(var weapon in inventory.OwnedWeapons)if(weapon){owned.Add(weapon.weaponCategory);if(weapon.weaponForm==InventoryWeapon.WeaponForm.Synthesized)synth.Add(weapon.weaponCategory);}
            foreach(var relic in loadout.Active)if(relic.appliesTo!=null)foreach(var category in relic.appliesTo)learned.Add(category);
            var result=new List<WeaponRelic>();
            while(result.Count<count&&pool.Count>0){
                bool preferOwned,preferSynth,preferLearned;
                var candidates=Prefer(pool,r=>Matches(r,owned),out preferOwned);
                if(preferOwned){candidates=Prefer(candidates,r=>Matches(r,synth),out preferSynth);if(preferSynth)candidates=Prefer(candidates,r=>Matches(r,learned),out preferLearned);}
                var selected=candidates[Random.Range(0,candidates.Count)];result.Add(selected);pool.Remove(selected);
            }
            return result.ToArray();
        }
        public static WeaponRelic RerollChoice(GameObject player,RelicLoadout loadout,WeaponRelic[] shown){
            var pool=Eligible(loadout);pool.RemoveAll(r=>System.Array.Exists(shown,c=>c!=null&&c.id==r.id));
            return pool.Count==0?null:PickChoices(pool,player.GetComponent<PlayerInventory>(),loadout,1)[0];
        }
        static void ShowReplacement(GameObject root,RelicLoadout loadout,WeaponRelic incoming,Font font)
        {
            var hidden=new List<GameObject>();
            foreach(Transform child in root.transform)if(child.name!="Shade"){hidden.Add(child.gameObject);child.gameObject.SetActive(false);}
            var panel=AdventureUI.Rect("Replacement",root.transform,Vector2.zero,new Vector2(1450,980));
            AdventureUI.Label(panel,"Heading","기억이 가득 찼습니다 · 내려놓을 기억을 선택하세요",new Vector2(0,425),new Vector2(1400,70),34,font);
            var next=AdventureUI.Rect("Incoming",panel,new Vector2(0,285),new Vector2(1290,180));AdventureUI.Surface(next.gameObject,true);
            var image=AdventureUI.Rect("Artwork",next,new Vector2(-540,0),new Vector2(140,140)).gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("UI/RelicArt/"+incoming.id);image.preserveAspect=true;
            AdventureUI.Label(next,"New name","새로 받을 기억 · "+incoming.displayName+" Lv.1  /  "+incoming.WeaponLabel,new Vector2(90,55),new Vector2(1000,45),26,font);
            AdventureUI.Label(next,"New description",incoming.DescriptionAt(1),new Vector2(90,-25),new Vector2(1000,90),23,font);
            int selected=-1;var surfaces=new StoryPanelGraphic[loadout.Active.Count];
            var summary=AdventureUI.Label(panel,"Comparison","아래 보유 기억 중 하나를 선택하면 교체 내용을 확인할 수 있습니다.",new Vector2(0,-290),new Vector2(1350,85),23,font);
            var confirm=AdventureUI.Button(panel,"ConfirmReplace","내려놓을 기억을 먼저 선택하세요",new Vector2(0,-385),new Vector2(650,65),font,()=>{
                if(selected>=0&&loadout.Replace(selected,incoming))Object.Destroy(root);
            },true);confirm.interactable=false;
            for(int i=0;i<loadout.Active.Count;i++){
                int index=i;var relic=loadout.Active[i];
                var card=AdventureUI.Rect("Owned"+i,panel,new Vector2((i-1.5f)*330,-30),new Vector2(310,420));surfaces[i]=AdventureUI.Surface(card.gameObject);
                AdventureUI.Label(card,"Weapon",relic.WeaponLabel,new Vector2(0,178),new Vector2(285,32),20,font);
                AdventureUI.Label(card,"Name",loadout.DisplayName(relic),new Vector2(0,140),new Vector2(285,40),24,font);
                var art=AdventureUI.Rect("Artwork",card,new Vector2(0,52),new Vector2(120,120)).gameObject.AddComponent<Image>();art.sprite=Resources.Load<Sprite>("UI/RelicArt/"+relic.id);art.preserveAspect=true;
                AdventureUI.Label(card,"Description",relic.DescriptionAt(loadout.Level(relic.id)),new Vector2(0,-74),new Vector2(280,122),19,font);
                AdventureUI.Button(card,"Replace"+i,"이 기억 선택",new Vector2(0,-172),new Vector2(260,48),font,()=>{
                    selected=index;for(int n=0;n<surfaces.Length;n++){surfaces[n].accent=n==index;surfaces[n].SetAllDirty();}
                    summary.text="내려놓기: "+loadout.DisplayName(relic)+"  →  이어받기: "+incoming.displayName+" Lv.1\n기존 기억의 효과와 강화 레벨이 사라집니다. 다른 3개 기억은 유지됩니다.";
                    confirm.GetComponentInChildren<Text>().text="선택한 기억을 내려놓고 이어받기";confirm.interactable=true;
                });
            }
            AdventureUI.Button(panel,"BackToChoices","취소 · 카드 선택으로 돌아가기",new Vector2(0,-465),new Vector2(520,55),font,()=>{
                Object.Destroy(panel.gameObject);foreach(var go in hidden)if(go)go.SetActive(true);
            });
            AdventureUI.Theme(panel.gameObject);
        }
        public static void Show(GameObject player, RelicLoadout loadout, WeaponRelic[] choices, System.Action onClosed, bool introduce=false)
        {
            if (open || choices == null || choices.Length == 0) { onClosed?.Invoke(); return; }
            CombatFeedback.CancelGlobalHitSlow();open = true;
            var motor = player.GetComponent<PlayerMotor>();
            var weapons = player.GetComponent<PlayerWeapons>();
            var camera = Camera.main ? Camera.main.GetComponent<PlayerCamera>() : null;
            bool motorInput = motor && motor.readInput, weaponInput = weapons && weapons.readInput, cameraInput = camera && camera.readInput;
            float previousScale = Time.timeScale;
            var cursorMode = Cursor.lockState; bool cursorVisible = Cursor.visible;
            if (motor) motor.readInput = false;
            if (weapons) weapons.readInput = false;
            if (camera) camera.readInput = false;
            Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var font = StoryBook.Instance ? StoryBook.Instance.Library.font : Font.CreateDynamicFontFromOSFont("Malgun Gothic", 28);
            var root = new GameObject("Relic Draft", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(player.transform, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30500;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var shade = AdventureUI.Rect("Shade", root.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.gameObject.AddComponent<Image>().color = new Color(.035f, .022f, .016f, .72f);
            AdventureUI.Label(root.transform, "Heading", "이어받을 기억을 선택하세요", new Vector2(0, 405), new Vector2(900, 70), 38, font).color = AdventureUI.Gold;
            var lifetime=root.AddComponent<RelicDraftLifetime>();
            lifetime.closed=()=>{open=false;Time.timeScale=previousScale;Cursor.lockState=cursorMode;Cursor.visible=cursorVisible;
                if(motor)motor.readInput=motorInput;if(weapons)weapons.readInput=weaponInput;if(camera)camera.readInput=cameraInput;onClosed?.Invoke();};
            var status=AdventureUI.Label(root.transform,"Slots","장착 "+loadout.Active.Count+" / "+RelicLoadout.MaxSlots+" · 중복 선택은 강화 · 새 기억은 가득 찼을 때 교체",new Vector2(0,-335),new Vector2(1300,40),22,font);
            AdventureUI.Button(root.transform,"Skip","선택하지 않고 닫기",new Vector2(0,-480),new Vector2(350,60),font,()=>Object.Destroy(root));
            choices=(WeaponRelic[])choices.Clone();
            var rerolled=new bool[choices.Length];var cards=new RectTransform[choices.Length];
            float[] xs = { -380, 0, 380 };
            System.Action<int> drawCard=null;
            drawCard=i=>
            {
                var relic = choices[i];
                bool upgrading=loadout.Has(relic.id);int nextLevel=Mathf.Min(relic.MaxLevel,loadout.Level(relic.id)+1);
                if(cards[i])Object.Destroy(cards[i].gameObject);
                var card = AdventureUI.Rect("Card" + i, root.transform, new Vector2(xs[i], 15), new Vector2(350, 610));cards[i]=card;
                AdventureUI.Surface(card.gameObject);
                AdventureUI.Label(card,"Weapon",relic.WeaponLabel+(relic.MaxLevel>1?" · Lv."+nextLevel+" / 3":""),new Vector2(0,266),new Vector2(300,34),21,font).fontStyle=FontStyle.Bold;
                AdventureUI.Label(card, "Name", relic.displayName, new Vector2(0, 222), new Vector2(310, 48), 27, font).color = AdventureUI.Gold;
                var picture=AdventureUI.Rect("Skill artwork",card,new Vector2(0,65),new Vector2(256,256)).gameObject.AddComponent<Image>();
                picture.sprite=Resources.Load<Sprite>("UI/RelicArt/"+relic.id);picture.preserveAspect=true;picture.raycastTarget=false;
                var desc = AdventureUI.Label(card, "Description", relic.DescriptionAt(nextLevel), new Vector2(0, -126), new Vector2(304, 108), 21, font);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap; desc.verticalOverflow = VerticalWrapMode.Truncate;
                var reroll=AdventureUI.Button(card,"Reroll",rerolled[i]?"다시 뽑기 0/1":"다시 뽑기 1/1",new Vector2(0,-200),new Vector2(220,30),font,()=>{
                    if(rerolled[i])return;
                    var replacement=RerollChoice(player,loadout,choices);if(replacement==null)return;
                    rerolled[i]=true;choices[i]=replacement;
                    status.text="장착 "+loadout.Active.Count+" / "+RelicLoadout.MaxSlots+" · 중복 선택 시 강화 · 새 기억은 교체";
                    drawCard(i);
                });
                reroll.GetComponentInChildren<Text>().fontSize=19; reroll.interactable=!rerolled[i];
                AdventureUI.Button(card, "Pick", upgrading?"Lv."+loadout.Level(relic.id)+" → Lv."+nextLevel+" 강화":"이어받기", new Vector2(0, -250), new Vector2(240, 64), font, () =>
                {
                    if(loadout.HasRoom||loadout.Has(relic.id)){if(loadout.TryAdd(relic))Object.Destroy(root);}
                    else ShowReplacement(root,loadout,relic,font);
                });
            };
            for(int i=0;i<choices.Length;i++)drawCard(i);
            AdventureUI.Label(root.transform,"Compatibility","일반형 · 합성형 모두 적용  |  그림은 일반형 동물 뼈 무기 기준",new Vector2(0,353),new Vector2(1100,34),20,font);
            AdventureUI.Theme(root);
            if(introduce){
                var hidden=new List<GameObject>();
                foreach(Transform child in root.transform)if(child.name!="Shade"){hidden.Add(child.gameObject);child.gameObject.SetActive(false);}
                var intro=AdventureUI.Rect("Memory introduction",root.transform,Vector2.zero,new Vector2(900,510));AdventureUI.Surface(intro.gameObject);
                AdventureUI.Label(intro,"Heading","이름을 잃은 목줄",new Vector2(0,165),new Vector2(800,65),36,font);
                var copy=AdventureUI.Label(intro,"Story","먼저 이 길을 걸었던 영혼이 남긴 목줄입니다.\n\n주인을 향해 나아갈 수 있도록,\n그들이 살아남기 위해 익힌 기억 하나를 이어받으세요.\n\n계절을 건너면 영혼은 안식을 얻고, 빌려준 힘도 돌아갑니다.",new Vector2(0,0),new Vector2(800,235),24,font);copy.lineSpacing=1.15f;
                AdventureUI.Button(intro,"Continue","기억에 손을 내밀기",new Vector2(0,-170),new Vector2(390,65),font,()=>{Object.Destroy(intro.gameObject);foreach(var go in hidden)if(go)go.SetActive(true);});
                AdventureUI.Theme(intro.gameObject);
            }
        }
    }
    public sealed class RelicDraftLifetime : MonoBehaviour
    {
        public System.Action closed;
        void OnDestroy(){var callback=closed;closed=null;callback?.Invoke();}
    }
}
