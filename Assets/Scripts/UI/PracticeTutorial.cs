using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // 모든 오브젝트·UI·무기 목록과 버튼 연결은 전용 씬 Hierarchy/Inspector에서 제작합니다.
    public sealed class PracticeTutorial : MonoBehaviour
    {
        public const string ScenePath="Assets/Scenes/PracticeTutorial.unity";
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerWeapons weapons;
        [SerializeField] PlayerCamera playerCamera;
        [SerializeField] BasicWeaponCombat combo;
        [SerializeField] CombatHealth playerHealth, dummy;
        [SerializeField] InventoryWeapon[] basicWeapons;
        [SerializeField] Transform moveGoal, attacker;
        [SerializeField] Transform[] attackArms;
        [SerializeField] Transform dummyModel;
        [SerializeField] EconomyStation practiceShop, practiceFusion;
        bool boughtWeapon, fusedWeapon;
        Quaternion[] armHome;
        Quaternion dummyHome;
        float lastDummyHit=-10;
        [SerializeField] Renderer attackRange;
        [SerializeField] Text stepLabel, heading, instruction, feedback, attackLabel;
        [SerializeField] Image attackFill;
        [SerializeField] GameObject attackPanel, completionPanel;
        [SerializeField] GameObject karmaPanel;
        [SerializeField] GameObject entryPrompt, tutorialCard;
        public static bool ChoosingEntry { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEntryChoice(){ChoosingEntry=false;}
        [SerializeField] RectTransform focusRoot, focusFrame, focusCallout;
        [SerializeField] CanvasGroup focusOpacity;
        [SerializeField] Text focusText;
        [SerializeField] RectTransform dashFocus, inventoryFocus, slotOneFocus, slotTwoFocus, karmaFocus, timingFocus;
        readonly Vector3[] focusCorners=new Vector3[4];
        const int KarmaStep=10, PotionStep=11, ShopStep=12, FusionStep=13, CompletionStep=14, FreePracticeStep=15;
        int potionPracticeCount;
        [SerializeField, Min(.5f)] float windup=1.1f;
        // 공격 후 긴 대쉬 쿨타임 전체를 다시 기다리지 않고 일정한 박자로 반복합니다.
        [SerializeField, Min(.1f)] float repeatDelay=.65f;
        [SerializeField, Min(1)] float attackRadius=3.2f;
        int step, swapMask;
        bool advancing, sawJump, sawInventory, warning, struck, returning;
        float advanceAt,crouchTime, attackStarted, nextAttack;
        Vector3 spawn, lockedCenter;
        MaterialPropertyBlock rangeBlock;
        public int Step=>step;
        public bool Warning=>warning;
        public float AttackProgress=>warning?Mathf.Clamp01((Time.time-attackStarted)/windup):0;
        static readonly string[] titles={"주변을 둘러보고 이동하기","점프하기","허수아비를 공격하기","콤보 이어가기","대쉬하기","몸을 낮추기","인벤토리 열기","무기 바꿔 들기","공격 범위 밖으로 피하기","대쉬 무적으로 통과하기","카르마와 전투 성향","회복 물약 사용하기","상점에서 무기 구매하기","무기 합성하기"};
        static readonly string[] hints={
            "W A S D  이동  ·  마우스  시점\n앞의 푸른 원 안으로 이동하세요.",
            "Space  점프\n한 번 뛰고 바닥에 착지하세요.",
            "마우스 왼쪽 버튼  공격\n정면의 허수아비에게 검을 맞혀 보세요.",
            "왼쪽 버튼을 박자에 맞춰 이어 누르세요.\n검의 3번째 공격을 허수아비에게 맞히세요.",
            "Shift 또는 마우스 오른쪽 버튼  대쉬\n이동 방향을 누르며 빠르게 이동해 보세요.",
            "Ctrl 또는 C 누르기  앉기\n1초간 몸을 낮춰 보세요.",
            "E  인벤토리 열기 / 닫기\n일반형 무기 12종을 확인한 뒤 E로 닫으세요.",
            "1 / 2  장착 무기 교체\n두 칸을 한 번씩 선택하세요. 다른 무기는 E에서 장착할 수 있습니다.",
            "발밑에 공격 범위가 표시됩니다.\n노란 원이 차오르면 붉게 변하기 전에 범위 밖으로 피하세요.",
            "이번에는 원 안에서 발동 직전에 대쉬하세요.\n노란 막대가 끝에 닿는 순간의 무적 시간으로 공격을 통과하세요.",
            "하단 중앙 구슬의 안개 색을 살펴보세요.\n카르마에 따라 공격이 빠르고 가볍게,\n또는 느리고 묵직하게 바뀝니다.",
            "Q로 체력 1칸 회복 · 연습용 물약 지급\n상점 50G · 사용 후 재사용 대기 5초\n체력이 가득 차면 소모되지 않아요.",
            "벚꽃 천막 상점 가까이에서 F\n무기 1개 구매 → F / Esc로 닫기\n연습용 150G · 물약 50G / 재고 2개",
            "꽃나무 합성대에서 F → 재료 2개 선택\n합성 → 결과 확인 → F로 창 닫기\n같은 뼈는 유지 · 혼합은 각각 50%\n무료 · 방당 2회 · 일반 무기만 재료"};
        IEnumerator Start()
        {
            Time.timeScale=1;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            spawn=motor.transform.position;motor.SetRespawnPoint(spawn);
            rangeBlock=new MaterialPropertyBlock();
            armHome=new Quaternion[attackArms.Length];
            for(int i=0;i<attackArms.Length;i++)armHome[i]=attackArms[i].localRotation;
            if(dummyModel)dummyHome=dummyModel.localRotation;
            playerHealth.minimumHealth=1;
            dummy.minimumHealth=1;
            dummy.Damaged+=OnDummyHit;
            motor.Dashed+=OnDash;
            inventory.QuickSlotSelected+=OnQuickSlotSelected;
            if(practiceShop){practiceShop.interactionLocked=true;practiceShop.WeaponPurchased+=OnPracticePurchase;}
            if(practiceFusion){practiceFusion.interactionLocked=true;practiceFusion.WeaponFused+=OnPracticeFusion;}
            // 프리팹 초기화가 끝난 후 일반형 목록만 지급하고 검/창을 두 칸에 배치합니다.
            yield return null;
            inventory.RestoreRunInventory(basicWeapons,0,new[]{0,1},0);
            SetStep(0);
            // 선택 화면은 씬에 제작한 UI를 표시하며, 선택 전에는 연습 입력을 막습니다.
            ChoosingEntry=entryPrompt;
            if(entryPrompt){entryPrompt.SetActive(true);tutorialCard.SetActive(false);SetControl(false);}
        }
        void OnDestroy(){ChoosingEntry=false;if(dummy)dummy.Damaged-=OnDummyHit;if(motor)motor.Dashed-=OnDash;if(inventory)inventory.QuickSlotSelected-=OnQuickSlotSelected;if(practiceShop)practiceShop.WeaponPurchased-=OnPracticePurchase;if(practiceFusion)practiceFusion.WeaponFused-=OnPracticeFusion;}
        public void BeginTutorial()
        {
            if(returning||SeasonLoadingScreen.IsLoading)return;
            ChoosingEntry=false;entryPrompt.SetActive(false);tutorialCard.SetActive(true);SetStep(0);
        }
        public void ContinueToAdventure()
        {
            if(returning||SeasonLoadingScreen.IsLoading)return;
            if(SkillWindow.Instance)SkillWindow.Instance.Close();
            if(EconomyWindow.Instance)EconomyWindow.Instance.Close();
            // 연습용 무기·골드가 본편으로 넘어가지 않도록 정리합니다.
            RunEconomy.ClearCarry();
            EnemyIntroduction.Prepare("Assets/Scenes/Stage1_Spring.unity",false);
            returning=SeasonLoadingScreen.Load(EnemyIntroduction.ScenePath);
            if(returning){ChoosingEntry=false;SetControl(false);}
            else feedback.text="몬스터 설명을 불러오지 못했습니다. 다시 시도해 주세요.";
        }
        void OnPracticePurchase(){if(step==ShopStep)boughtWeapon=true;}
        void OnPracticeFusion(){if(step==FusionStep)fusedWeapon=true;}
        void OnQuickSlotSelected(int slot)
        {
            if(step!=7||advancing||slot<0||slot>1)return;
            swapMask|=1<<slot;
            if(swapMask==3)CompleteStep("1번·2번 장착칸 선택을 모두 확인했습니다!");
            else feedback.text=slot==0?"1번 확인! 이제 2번 키를 누르세요.":"2번 확인! 이제 1번 키를 누르세요.";
        }
        void PrepareSwapPractice()
        {
            // 앞 단계에서 장착을 해제했어도 연습을 시작할 수 있게 빈 칸만 보충합니다.
            int selected=inventory.ActiveSlot;
            for(int slot=0;slot<2;slot++)
            {
                if(inventory.FindInstance(inventory.SlotInstanceId(slot)))continue;
                for(int i=0;i<inventory.OwnedWeapons.Count;i++)
                {
                    string id=inventory.GetInstanceId(i);
                    if(id==inventory.SlotInstanceId(1-slot))continue;
                    inventory.AssignSlot(slot,id);break;
                }
            }
            inventory.SelectSlot(selected);
        }
        void OnDash(){if(step==4)CompleteStep("대쉬 성공! 재사용 대기시간도 확인하세요.");}
        void OnDummyHit(float damage)
        {
            lastDummyHit=Time.time;
            if(step==2)CompleteStep("명중! 실제 공격이 허수아비에게 닿았습니다.");
            if(step==3&&combo.ComboStep>=3)CompleteStep("3타 콤보 성공!");
        }
        void SetStep(int value)
        {
            step=value;advancing=false;sawJump=false;sawInventory=false;crouchTime=0;swapMask=0;
            warning=false;struck=false;nextAttack=Time.time+.4f;attackRange.enabled=false;
            ResetArms();
            moveGoal.gameObject.SetActive(step==0);completionPanel.SetActive(step==CompletionStep);
            // 완료 화면에서는 이전 단계 안내와 겹치지 않게 합니다.
            if(tutorialCard)tutorialCard.SetActive(step!=CompletionStep);
            if(karmaPanel)karmaPanel.SetActive(step==KarmaStep);
            attackPanel.SetActive(step==8||step==9||step==FreePracticeStep);
            feedback.text="";
            if(practiceShop)practiceShop.interactionLocked=step<ShopStep;
            if(practiceFusion)practiceFusion.interactionLocked=step<FusionStep;
            if(step==ShopStep){boughtWeapon=false;var wallet=RunEconomy.Instance;if(wallet)wallet.SetGold(Mathf.Max(wallet.Gold,150));}
            if(step==FusionStep){fusedWeapon=false;int count=0;foreach(var item in inventory.OwnedWeapons)if(item&&item.weaponForm==InventoryWeapon.WeaponForm.Basic)count++;for(int i=count;i<2;i++)inventory.AddWeapon(basicWeapons[i%basicWeapons.Length]);}
            if(step==7){PrepareSwapPractice();feedback.text="1번과 2번 키를 각각 한 번씩 누르세요.";}
            if(step<CompletionStep){stepLabel.text=(step+1).ToString("00")+" / "+titles.Length+"  ·  조작 연습";heading.text=titles[step];instruction.text=hints[step];}
            else {stepLabel.text="조작 연습 완료";heading.text=step==CompletionStep?"이제 모험을 시작할 준비가 됐어요":"자유롭게 연습하세요";instruction.text="E 인벤토리 · 1 / 2 무기 교체\n상점 구매와 합성형 무기도 연습해 보세요";}
            if(step==8||step==9)attackLabel.text="발밑의 범위를 보고 회피하세요";
            else if(step==FreePracticeStep)attackLabel.text="훈련 허수아비 가까이에서 회피 연습";
            if(step==3){inventory.SelectSlot(0);if(inventory.EquippedWeapon!=basicWeapons[0])inventory.Equip(basicWeapons[0]);}
            // 회복을 직접 연습할 수 있게 한 칸만 비우고 연습용 물약을 보충합니다.
            if(step==PotionStep&&RunEconomy.Instance)
            {
                var wallet=RunEconomy.Instance;
                if(wallet.HealingPotions<1)wallet.SetHealingPotions(1);
                potionPracticeCount=wallet.HealingPotions;
                playerHealth.RestoreCheckpoint(playerHealth.maxHealth-1,playerHealth.maxHealth);
            }
            if(step==KarmaStep||step==CompletionStep){combo.Cancel();motor.MoveInput=Vector2.zero;}
            SetControl(step!=KarmaStep&&step!=CompletionStep);
        }
        void SetControl(bool value)
        {motor.readInput=value;weapons.readInput=value;playerCamera.readInput=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;}
        void CompleteStep(string message)
        {
            // 조작 성공은 자동 진행하고, 별도 설명 페이지에서만 확인 입력을 기다립니다.
            if(advancing)return;advancing=true;advanceAt=Time.time+1.2f;feedback.text=message;
            warning=false;attackRange.enabled=false;
        }
        void Update()
        {
            if(!rangeBlockInitialized||returning||ChoosingEntry)return;
            if(motor.transform.position.y < -5){motor.Respawn();playerHealth.Restore();}
            if(dummy.Current<dummy.maxHealth*.5f)dummy.Restore();
            // Blender에서 제작한 고정 허수아비도 명중하면 짧게 흔들려 손맛을 전달합니다.
            if(dummyModel){float age=Time.time-lastDummyHit;float angle=age<.65f?Mathf.Sin(age*24)*5*(1-age/.65f):0;dummyModel.localRotation=dummyHome*Quaternion.Euler(0,angle,0);}
            // 인벤토리는 시간을 멈추므로 열림/닫힘 확인을 전투 입력 차단보다 먼저 처리합니다.
            if(step==6){sawInventory|=SkillWindow.IsOpen;if(sawInventory&&!SkillWindow.IsOpen)CompleteStep("인벤토리 확인 완료!");}
            // 메뉴가 시간을 멈춰도 거래 성공을 읽고, 닫은 뒤 자동으로 다음 단계로 진행합니다.
            if(step==ShopStep&&boughtWeapon&&!advancing){feedback.text="구매 성공! F / Esc로 창을 닫으세요.";if(!EconomyWindow.IsOpen)CompleteStep("구매 완료! E 인벤토리에서 확인하세요.");}
            if(step==FusionStep&&fusedWeapon&&!advancing){feedback.text="합성 성공! 결과 확인 후 F로 닫으세요.";if(!EconomyWindow.IsOpen)CompleteStep("합성 완료! E에서 장착해 보세요.");}
            if(SkillWindow.BlocksGameplayInput||StoryBook.BlocksInput||Time.timeScale<=0)return;
            if(advancing){if(Time.time>=advanceAt)SetStep(step+1);return;}
            if(step==KarmaStep&&(Input.GetKeyDown(KeyCode.Space)||Input.GetMouseButtonDown(0))){ConfirmKarma();return;}
            if(step==PotionStep&&RunEconomy.Instance&&RunEconomy.Instance.HealingPotions<potionPracticeCount&&playerHealth.Current>=playerHealth.maxHealth)CompleteStep("회복 성공! 물약 1개로 체력 1칸을 회복했어요.");
            if(step==3)feedback.text=combo.ComboInputWindow?"지금 왼쪽 버튼! 다음 타격으로 연결하세요.":combo.IsAttacking?combo.ComboStep+"타 진행 중 — 다음 클릭을 준비하세요.":"허수아비를 향해 검 공격을 시작하세요.";
            if(step==0&&FlatDistance(motor.transform.position,moveGoal.position)<1.2f)CompleteStep("이동 성공!");
            else if(step==1){if(!motor.Grounded)sawJump=true;if(sawJump&&motor.Grounded)CompleteStep("점프와 착지 성공!");}
            else if(step==4&&motor.IsDashing)CompleteStep("대쉬 성공! 재사용 대기시간도 확인하세요.");
            else if(step==5){crouchTime=motor.Crouching?crouchTime+Time.deltaTime:0;if(crouchTime>=1)CompleteStep("앉기 성공!");}
            else if(step==7&&(!inventory.FindInstance(inventory.SlotInstanceId(0))||!inventory.FindInstance(inventory.SlotInstanceId(1))))
                feedback.text="빈 장착칸이 있어요. E를 열어 두 칸에 무기를 장착하세요.";
            if(step==8||step==9||step==FreePracticeStep)UpdateAttack();
        }
        bool rangeBlockInitialized=>rangeBlock!=null;
        // 강조 틀과 화살표는 Hierarchy에서 제작하고, 해상도와 대상 UI 위치에 맞춰 이동만 합니다.
        void LateUpdate()
        {
            if(!focusRoot||!rangeBlockInitialized)return;
            RectTransform target=null,second=null;string caption="";
            if(!returning&&!advancing&&!GameSettingsWindow.BlocksGameplayInput&&!StoryBook.BlocksInput)
            {
                if(step==4){target=dashFocus;caption="대쉬 · Shift / 우클릭\n아이콘에서 재사용 대기시간 확인";}
                else if(step==6||step==7)
                {
                    if(SkillWindow.IsOpen){target=slotOneFocus;second=slotTwoFocus;caption="무기 장착칸 1 / 2\n여기에 장착한 무기를 교체합니다";}
                    else{target=inventoryFocus;caption=step==6?"인벤토리 · E\n무기 목록과 장착칸을 확인하세요":"무기 교체 · 1 / 2\n장착칸은 E로 확인할 수 있어요";}
                }
                else if(step==8||step==9){target=timingFocus;caption="공격 발동 타이밍\n노란 막대가 끝에 닿으면 공격!";}
                else if(step==KarmaStep){target=karmaFocus;caption="카르마 구슬\n안개 색과 양으로 현재 상태 확인";}
                else if(step==PotionStep&&EconomyWindow.Instance&&EconomyWindow.Instance.potionHud){target=EconomyWindow.Instance.potionHud.transform as RectTransform;caption="회복 물약 · 남은 수량\nQ로 사용 · 체력 1칸 회복";}
                else if((step==ShopStep||step==FusionStep)&&EconomyWindow.Instance)
                {
                    var ui=EconomyWindow.Instance;
                    if(!EconomyWindow.IsOpen){target=ui.prompt.rectTransform;caption=step==ShopStep?"벚꽃 천막 상점 · F로 열기":"꽃나무 합성대 · F로 열기";}
                    else if(step==ShopStep&&!boughtWeapon){foreach(var button in ui.buyButtons)if(button&&button.gameObject.activeInHierarchy&&button.interactable){target=button.transform as RectTransform;break;}caption="구매 버튼 · 연습용 150G\n무기 1개를 구매해 보세요";}
                    else if(step==FusionStep&&!fusedWeapon){target=ui.fusionPanel.transform as RectTransform;caption="양쪽 화살표로 재료 선택 → 합성\n같은 무기 종류는 그 종류의 합성형";}
                }
            }
            bool show=target&&target.gameObject.activeInHierarchy;
            focusRoot.gameObject.SetActive(show);
            if(!show)return;
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            FocusBounds(target,ref min,ref max);if(second)FocusBounds(second,ref min,ref max);
            // 구슬 셰이더의 실제 원은 RectTransform의 약 75% 영역에 그려집니다.
            if(target==karmaFocus){Vector2 center=(min+max)*.5f,half=(max-min)*.38f;min=center-half;max=center+half;}
            focusFrame.anchoredPosition=(min+max)*.5f;focusFrame.sizeDelta=max-min+Vector2.one*16;
            Vector2 label=new Vector2((min.x+max.x)*.5f,max.y+78);
            Rect bounds=focusRoot.rect;
            label.x=Mathf.Clamp(label.x,bounds.xMin+190,bounds.xMax-190);
            label.y=Mathf.Min(label.y,bounds.yMax-65);
            focusCallout.anchoredPosition=label;focusText.text=caption;
            focusOpacity.alpha=.88f+.12f*Mathf.Sin(Time.unscaledTime*2.5f);
        }
        void FocusBounds(RectTransform rect,ref Vector2 min,ref Vector2 max)
        {
            rect.GetWorldCorners(focusCorners);
            var canvas=rect.GetComponentInParent<Canvas>();var cam=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            for(int i=0;i<4;i++)
            {
                Vector2 point;RectTransformUtility.ScreenPointToLocalPointInRectangle(focusRoot,RectTransformUtility.WorldToScreenPoint(cam,focusCorners[i]),null,out point);
                min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        void UpdateAttack()
        {
            if(!warning)
            {
                if(Time.time<nextAttack)return;
                // 필수 회피 단계는 이동 방향·허수아비 거리와 무관하게 반복합니다.
                // 자유 연습에서만 허수아비 근처에 있을 때 공격합니다.
                if(step==FreePracticeStep&&FlatDistance(motor.transform.position,attacker.position)>7)return;
                lockedCenter=motor.transform.position;lockedCenter.y=.035f;
                attackRange.transform.position=lockedCenter;attackRange.transform.localScale=new Vector3(attackRadius*2,attackRadius*2,1);
                warning=true;struck=false;attackStarted=Time.time;attackRange.enabled=true;
            }
            float p=AttackProgress;
            float elapsed=Time.time-attackStarted;
            float swing=p<.8f?Mathf.Lerp(0,-35,Mathf.SmoothStep(0,1,p/.8f)):Mathf.Lerp(-35,70,Mathf.SmoothStep(0,1,(p-.8f)/.2f));
            if(elapsed>windup)swing=Mathf.Lerp(70,0,Mathf.SmoothStep(0,1,(elapsed-windup)/.35f));
            for(int i=0;i<attackArms.Length;i++)if(attackArms[i])attackArms[i].localRotation=armHome[i]*Quaternion.Euler(0,0,swing);
            attackFill.fillAmount=p;
            attackFill.rectTransform.localScale=new Vector3(p,1,1);
            var color=p<1?new Color(1,.65f,.12f,.8f):new Color(1,.15f,.07f,.9f);
            attackFill.color=color;rangeBlock.SetColor("_Color",color);rangeBlock.SetFloat("_Progress",p);attackRange.SetPropertyBlock(rangeBlock);
            attackLabel.text=p<.78f?"공격 준비 — 노란 범위가 차오릅니다":p<1?"곧 발동!": "공격 발동";
            if(p>=1&&!struck)
            {
                struck=true;
                bool inside=FlatDistance(motor.transform.position,lockedCenter)<=attackRadius;
                bool immune=motor.IsDashInvulnerable;
                // 성공 안내와 진행 조건을 통일합니다. 대쉬로 원 밖까지 이동해도 타이밍 성공입니다.
                if(step==8&&(!inside||immune)){CompleteStep(immune?"무적 회피 성공!":"범위 회피 성공!");return;}
                if(step==9&&immune){CompleteStep("타이밍 성공! 대쉬 무적으로 통과했습니다.");return;}
                if(inside&&!immune){playerHealth.Damage(1);feedback.text="조금 빨랐거나 늦었어요. 다시 연습해 보세요.";}
                else feedback.text=immune?"무적 회피 성공!":"범위 회피 성공!";
                if(step==9&&!inside)feedback.text="이번에는 원 안에서 발동 직전에 대쉬해 보세요.";
            }
            if(Time.time-attackStarted>windup+.35f)
            {warning=false;attackRange.enabled=false;ResetArms();playerHealth.Restore();nextAttack=Time.time+repeatDelay;}
        }
        void ResetArms(){if(armHome==null)return;for(int i=0;i<attackArms.Length;i++)if(attackArms[i])attackArms[i].localRotation=armHome[i];}
        // 설명은 자동으로 넘기지 않고 Hierarchy 버튼의 확인 입력을 기다립니다.
        public void ConfirmKarma(){if(step==KarmaStep)SetStep(PotionStep);}
        public void FreePractice(){SetStep(FreePracticeStep);}
        public void Restart()
        {if(returning)return;if(SkillWindow.Instance)SkillWindow.Instance.Close();if(EconomyWindow.Instance)EconomyWindow.Instance.Close();RunEconomy.ClearCarry();returning=SeasonLoadingScreen.Load(ScenePath);}
        public void ReturnToTitle()
        {
            if(returning)return;
            if(SkillWindow.Instance)SkillWindow.Instance.Close();
            // 연습 결과나 카르마를 저장/계승하지 않고 타이틀만 로드합니다.
            returning=SeasonLoadingScreen.Load("Assets/Scenes/Title.unity");
        }
    }
}

