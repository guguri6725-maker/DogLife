#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // Explicit play-mode regression check. Does not write saves or change weapon assets.
    public sealed class RelicPatternValidation:MonoBehaviour
    {
        public string Status {get;private set;}
        readonly List<string> results=new List<string>();readonly List<GameObject> targets=new List<GameObject>();
        PlayerMotor player;PlayerInventory inventory;BasicWeaponCombat combat;PlayerWeapons weapons;RelicLoadout loadout;RelicCombatEffects effects;
        Vector3 origin;
        void Check(string name,bool pass){results.Add((pass?"PASS ":"FAIL ")+name);System.IO.Directory.CreateDirectory("outputs/relic");System.IO.File.WriteAllLines("outputs/relic/checks.txt",results);}
        void Invoke(string method,params object[] args)=>typeof(BasicWeaponCombat).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,args);
        void Set(string field,object value)=>typeof(BasicWeaponCombat).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(combat,value);
        InventoryWeapon Equip(InventoryWeapon.WeaponCategory category)
        {
            combat.Cancel();loadout.Clear();foreach(var p in FindObjectsByType<BoneWeaponProjectile>(FindObjectsSortMode.None))Destroy(p.gameObject);
            var item=Resources.Load<RunWeaponCatalog>("RunWeaponCatalog").GetType();
            var weapon=UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryWeapon>("Assets/Data/Weapons/Dog/Basic/"+category+".asset");
            inventory.AddWeapon(weapon);inventory.Equip(weapon);return weapon;
        }
        BasicWeaponMoveset Move(InventoryWeapon w)=>combat.movesets.First(m=>m.category==w.weaponCategory);
        void Relics(params string[] ids){loadout.Clear();foreach(var id in ids)loadout.TryAdd(RelicCatalog.Find(id));}
        CombatHealth Target(Vector3 p,bool multiple=false)
        {
            var go=new GameObject("Relic validation target");go.transform.position=p;var c=go.AddComponent<BoxCollider>();c.size=Vector3.one*.7f;
            var hp=go.AddComponent<CombatHealth>();hp.practiceTarget=true;hp.RestoreCheckpoint(10000,10000);
            if(multiple){var child=new GameObject("Second hurtbox");child.transform.SetParent(go.transform,false);child.AddComponent<BoxCollider>().size=Vector3.one*.6f;}
            targets.Add(go);Physics.SyncTransforms();return hp;
        }
        void ClearTargets(){foreach(var t in targets)if(t)DestroyImmediate(t);targets.Clear();Physics.SyncTransforms();}
        IEnumerator Start()
        {
            Status="Running";yield return null;
            player=FindFirstObjectByType<PlayerMotor>();inventory=player.GetComponent<PlayerInventory>();combat=player.GetComponent<BasicWeaponCombat>();weapons=player.GetComponent<PlayerWeapons>();loadout=RelicLoadout.Get(player.gameObject);effects=player.GetComponent<RelicCombatEffects>();
            player.readInput=false;player.enabled=false;weapons.readInput=false;player.GetComponent<CombatHealth>().invulnerable=true;
            player.GetComponent<Rigidbody>().isKinematic=true;player.transform.position=new Vector3(100,20,100);Physics.SyncTransforms();origin=player.GetComponent<CapsuleCollider>().bounds.center;weapons.AimAt(origin+Vector3.forward*20);
            var feedback=player.GetComponent<CombatFeedback>();if(feedback)typeof(CombatFeedback).GetField("hitSlowEnabled",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(feedback,false);
            Check("catalog 12 weapon-only unique",RelicCatalog.All.Length==12&&RelicCatalog.All.Select(r=>r.id).Distinct().Count()==12&&RelicCatalog.All.All(r=>r.appliesTo.Length==1));
            bool randomValid=true;for(int n=0;n<100;n++){var choices=RelicDraftUI.PrepareChoices(player.gameObject,loadout);randomValid&=choices.Length==3&&choices.Select(c=>c.id).Distinct().Count()==3;}Check("100 drafts contain 3 distinct choices",randomValid);
            Relics("sword_spin","sword_finisher_surge","bow_pierce");Check("full slots still offer reward",RelicDraftUI.PrepareChoices(player.gameObject,loadout).Length==3);Check("replace one of 3 slots",loadout.Replace(1,RelicCatalog.Find("axe_whirlwind"))&&loadout.Active.Count==3);Check("duplicate rejected",!loadout.TryAdd(RelicCatalog.Find("sword_spin")));
            var sword=Equip(InventoryWeapon.WeaponCategory.Sword);Relics("sword_spin","sword_finisher_surge");var a=Target(origin+Vector3.forward);var b=Target(origin-Vector3.forward,true);
            effects.ReplaceStrike(sword,Move(sword),0,origin,Vector3.forward,2,1,Color.cyan);
            Check("sword 360 front and back, collider dedup",Mathf.Approximately(a.Current,10000-sword.damage)&&Mathf.Approximately(b.Current,10000-sword.damage));
            ClearTargets();a=Target(origin+Vector3.forward*1.2f);effects.ReplaceStrike(sword,Move(sword),2,origin,Vector3.forward,2,1,Color.cyan);yield return new WaitForSeconds(.35f);Check("finisher melee and wave hit same enemy",a.Current<=10000-sword.damage*2+.01f);ClearTargets();loadout.Clear();
            var spear=Equip(InventoryWeapon.WeaponCategory.Spear);Relics("spear_storm");a=Target(origin+new Vector3(1,0,1.5f));b=Target(origin+new Vector3(-1,0,1.5f));var rear=Target(origin-Vector3.forward);
            for(int i=0;i<12;i++)effects.ReplaceStrike(spear,Move(spear),2,origin,Vector3.forward,3,.15f,Color.cyan);
            Check("spear 12 fan ticks hit both, spare rear",a.Current<9999&&b.Current<9999&&rear.Current==10000&&loadout.SpearFinisherThreshold()==12);ClearTargets();
            var dagger=Equip(InventoryWeapon.WeaponCategory.Dagger);Relics("dagger_phantom","dagger_chain");a=Target(origin+Vector3.forward*2,true);effects.AfterStrike(dagger,Move(dagger),0,origin,Vector3.forward,3,1,Color.cyan,true);yield return new WaitForSeconds(.5f);Check("one phantom per hit",Mathf.Abs(a.Current-(10000-dagger.damage*.35f))<.02f);
            int sideBefore=(int)typeof(RelicCombatEffects).GetField("phantomSide",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effects);effects.AfterStrike(dagger,Move(dagger),2,origin,Vector3.forward,3,1,Color.cyan,true);int sideAfter=(int)typeof(RelicCombatEffects).GetField("phantomSide",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effects);Check("phantom advances eight-way side on next hit",sideAfter==(sideBefore+1)%8);Relics("dagger_chain");
            a.RestoreCheckpoint(10000,10000);effects.AfterStrike(dagger,Move(dagger),1,origin,Vector3.forward,3,1,Color.cyan,true);yield return new WaitForSeconds(.25f);Check("dagger haste replaces extra damage",a.Current==10000&&Mathf.Approximately(effects.MovementMultiplier,1.2f));
            effects.AfterStrike(dagger,Move(dagger),0,origin,Vector3.forward,3,1,Color.cyan,false);Check("missed dagger attack refreshes haste without stacking",Mathf.Approximately(effects.MovementMultiplier,1.2f));yield return new WaitForSeconds(2.1f);Check("haste expires",Mathf.Approximately(effects.MovementMultiplier,1f));ClearTargets();
            var axe=Equip(InventoryWeapon.WeaponCategory.Axe);Relics("axe_shockwave","axe_whirlwind");a=Target(origin+Vector3.forward);effects.AfterStrike(axe,Move(axe),0,origin,Vector3.forward,2,1,Color.cyan,false);Check("shockwave requires a hit",a.Current==10000);effects.AfterStrike(axe,Move(axe),0,origin,Vector3.forward,2,1,Color.cyan,true);Check("axe early hit triggers shockwave",a.Current<10000);
            combat.PressAttack();Invoke("Begin",2);combat.CancelForDash();Check("whirlwind allows movement and survives dash cancel",combat.ComboStep==3&&!combat.LocksMovement);combat.ReleaseAttack();yield return new WaitForSeconds(Move(axe).strikeTimes[2]+.3f);Check("automatic whirlwind survives mouse release",combat.ComboStep==3);combat.PressAttack();Check("whirlwind rejects another attack",combat.ComboStep==3);yield return new WaitForSeconds(2.3f);Check("whirlwind exits after fixed duration",!combat.IsAttacking);combat.Cancel();ClearTargets();
            spear=Equip(InventoryWeapon.WeaponCategory.Spear);Relics("spear_ripple");a=Target(origin+Vector3.forward);a.gameObject.AddComponent<RelicPracticeTarget>();b=Target(origin+Vector3.forward*4);
            var shape=BoneWeaponVfx.Line(new[]{origin,origin+Vector3.forward*1.4f},Color.cyan,Move(spear).effectMaterial,.2f);
            weapons.ApplyVisibleMelee(spear,origin,shape.HitShape,false,1);Check("practice dummy reports successful hit before restoration",weapons.LastMeleeHit&&a.Current<10000);
            effects.AfterStrike(spear,Move(spear),0,origin,Vector3.forward,3,1,Color.cyan,weapons.LastMeleeHit);yield return new WaitForSeconds(.45f);Check("spear ripple travels from practice hit to distant enemy",b.Current<10000);Check("practice dummy restores after damage result",a.Current==10000);ClearTargets();
            var bow=Equip(InventoryWeapon.WeaponCategory.Bow);Relics("bow_split_shot","bow_pierce");a=Target(origin+Vector3.forward*2,true);a.GetComponent<BoxCollider>().size=new Vector3(4,2,.7f);b=Target(origin+Vector3.forward*4);b.GetComponent<BoxCollider>().size=new Vector3(4,2,.7f);Physics.SyncTransforms();
            foreach(float angle in new[]{-8f,0,8f})BoneWeaponProjectile.Launch(origin,Quaternion.Euler(0,angle,0)*Vector3.forward,player.transform,bow,Move(bow),Color.cyan,8,1);
            yield return new WaitForSeconds(1);float expected=bow.damage*Move(bow).bowFullChargeMultiplier*3;Check("three arrows each pierce both targets",Mathf.Abs(a.Current-(10000-expected))<.03f&&Mathf.Abs(b.Current-(10000-expected))<.03f);ClearTargets();
            var arrowTarget=Target(origin+Vector3.forward*2,true);var arrow=BoneWeaponProjectile.Launch(origin,Vector3.forward,player.transform,bow,Move(bow),Color.cyan,30,1);
            yield return new WaitForSeconds(.15f);Check("piercing arrow keeps launch direction and axis after impact",arrow&&arrowTarget.Current<10000&&Vector3.Angle(arrow.transform.forward,Vector3.forward)<.01f&&Mathf.Abs(arrow.transform.position.x-origin.x)<.001f);if(arrow)Destroy(arrow.gameObject);ClearTargets();
            var boom=Equip(InventoryWeapon.WeaponCategory.Boomerang);Relics("boomerang_chase");var seeking=BoneWeaponProjectile.Launch(origin,Vector3.forward,player.transform,boom,Move(boom),Color.cyan,12);
            yield return new WaitForSeconds(.1f);Check("targetless homing boomerang flies forward",seeking&&seeking.transform.position.z>origin.z+.1f);
            a=Target(seeking?seeking.transform.position+new Vector3(2,0,2):origin+Vector3.forward*2);Physics.SyncTransforms();yield return new WaitForSeconds(.35f);Check("boomerang acquires enemy entering range",a.Current<10000);if(seeking)Destroy(seeking.gameObject);ClearTargets();
            a=Target(origin+Vector3.forward*2,true);BoneWeaponProjectile.Launch(origin,Vector3.forward,player.transform,boom,Move(boom),Color.cyan,8);yield return new WaitForSeconds(2.5f);Check("chase exactly 4 hits then returns hp="+a.Current+" damage="+boom.damage+" projectiles="+FindObjectsByType<BoneWeaponProjectile>(FindObjectsSortMode.None).Length,Mathf.Abs(a.Current-(10000-boom.damage*4))<.03f&&FindObjectsByType<BoneWeaponProjectile>(FindObjectsSortMode.None).Length==0);ClearTargets();
            Relics("boomerang_orbit");yield return new WaitForSeconds(.2f);Check("one permanent orbit",GameObject.Find("Relic orbit boomerang"));loadout.Clear();yield return null;Check("orbit removed with relic",!GameObject.Find("Relic orbit boomerang"));
            // Level progression is independent of slot count and survives checkpoint serialization.
            Relics("sword_finisher_surge","bow_split_shot","boomerang_orbit");
            foreach(var r in loadout.Active.ToArray()){Check("upgrade to level 2 "+r.id,loadout.TryAdd(r)&&loadout.Level(r.id)==2);Check("upgrade to level 3 "+r.id,loadout.TryAdd(r)&&loadout.Level(r.id)==3);Check("level cap "+r.id,!loadout.TryAdd(r));}
            var saved=loadout.Ids();loadout.Clear();loadout.RestoreIds(saved);Check("checkpoint restores levels and three slots",loadout.Active.Count==3&&loadout.Active.All(r=>loadout.Level(r.id)==3));
            bool cappedExcluded=true;for(int i=0;i<60;i++)cappedExcluded&=RelicDraftUI.PrepareChoices(player.gameObject,loadout).All(r=>!loadout.Has(r.id));Check("max level cards excluded",cappedExcluded);
            loadout.RestoreIds(new[]{"bow_split_shot"});Check("legacy checkpoint becomes level 1",loadout.Level("bow_split_shot")==1);
            dagger=Equip(InventoryWeapon.WeaponCategory.Dagger);Relics("dagger_phantom","dagger_phantom","dagger_phantom");a=Target(origin+Vector3.forward*2,true);int shadows=FindObjectsByType<RelicShadowEcho>(FindObjectsSortMode.None).Length;effects.AfterStrike(dagger,Move(dagger),0,origin,Vector3.forward,3,1,Color.cyan,true);
            Check("level 3 phantom creates three echoes and three hits",FindObjectsByType<RelicShadowEcho>(FindObjectsSortMode.None).Length-shadows==3&&Mathf.Abs(a.Current-(10000-dagger.damage*1.05f))<.03f);ClearTargets();
            spear=Equip(InventoryWeapon.WeaponCategory.Spear);Relics("spear_ripple","spear_ripple","spear_ripple");a=Target(origin+Vector3.forward*4);effects.AfterStrike(spear,Move(spear),0,origin,Vector3.forward,3,1,Color.cyan,true);yield return new WaitForSeconds(1);Check("level 3 ripple three independent waves",Mathf.Abs(a.Current-(10000-spear.damage*1.5f))<.03f);ClearTargets();
            sword=Equip(InventoryWeapon.WeaponCategory.Sword);Relics("sword_finisher_surge","sword_finisher_surge","sword_finisher_surge");a=Target(origin+Vector3.forward*5);effects.ReplaceStrike(sword,Move(sword),2,origin,Vector3.forward,2,1,Color.cyan);yield return new WaitForSeconds(1);Check("level 3 sword three independent waves",Mathf.Abs(a.Current-(10000-sword.damage*3))<.03f);ClearTargets();
            axe=Equip(InventoryWeapon.WeaponCategory.Axe);Relics("axe_whirlwind","axe_whirlwind","axe_whirlwind");combat.PressAttack();Invoke("Begin",2);
            float starts=(float)typeof(BasicWeaponCombat).GetField("whirlwindStarts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(combat),ends=(float)typeof(BasicWeaponCombat).GetField("whirlwindEnds",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(combat);Check("level 3 whirlwind lasts four seconds",Mathf.Abs(ends-starts-4)<.01f);combat.Cancel();
            bow=Equip(InventoryWeapon.WeaponCategory.Bow);Relics("bow_split_shot","bow_split_shot","bow_split_shot");combat.PressAttack();Invoke("Begin",0);Set("shotCharge",1f);Invoke("Strike");Check("level 3 full charge launches seven arrows",FindObjectsByType<BoneWeaponProjectile>(FindObjectsSortMode.None).Length==7);combat.Cancel();
            boom=Equip(InventoryWeapon.WeaponCategory.Boomerang);yield return null;Relics("boomerang_orbit","boomerang_orbit","boomerang_orbit");yield return null;Check("level 3 orbit has three blades",GameObject.Find("Relic orbit boomerang").transform.childCount==3);loadout.Clear();yield return null;Check("clearing mastery removes orbit blades",!GameObject.Find("Relic orbit boomerang"));
            spear=Equip(InventoryWeapon.WeaponCategory.Spear);Relics("spear_storm");foreach(var effect in FindObjectsByType<BoneWeaponVfx>(FindObjectsSortMode.None))DestroyImmediate(effect.gameObject);effects.ReplaceStrike(spear,Move(spear),2,origin,Vector3.forward,3,.15f,Color.cyan);var thrusts=FindObjectsByType<BoneWeaponVfx>(FindObjectsSortMode.None);Check("spear storm uses three straight thrust paths, no sweep",thrusts.Count(f=>f.HitShape.positionCount==2)==3&&thrusts.All(f=>f.HitShape.positionCount!=65));
            Relics("axe_whirlwind","sword_spin","bow_pierce","boomerang_chase");RelicDraftUI.Show(player.gameObject,loadout,new[]{RelicCatalog.Find("axe_whirlwind")},()=>{});yield return null;GameObject.Find("Relic Draft").GetComponentsInChildren<Button>().First(x=>x.name=="Pick").onClick.Invoke();yield return null;Check("full inventory upgrades in place without replacement",loadout.Active.Count==4&&loadout.Level("axe_whirlwind")==2&&!RelicDraftUI.IsOpen);
            Relics("sword_spin","bow_pierce","axe_whirlwind","boomerang_chase");var options=new[]{RelicCatalog.Find("spear_ripple"),RelicCatalog.Find("dagger_phantom"),RelicCatalog.Find("boomerang_orbit")};int close=0;RelicDraftUI.Show(player.gameObject,loadout,options,()=>close++);yield return null;
            GameObject.Find("Relic Draft").GetComponentsInChildren<Button>().First(x=>x.name=="Pick").onClick.Invoke();Check("full draft waits for replacement",RelicDraftUI.IsOpen);
            GameObject.Find("Relic Draft").GetComponentsInChildren<Button>().First(x=>x.name=="Replace0").onClick.Invoke();GameObject.Find("Relic Draft").GetComponentsInChildren<Button>().First(x=>x.name=="ConfirmReplace").onClick.Invoke();yield return null;Check("draft restores time and closes once",!RelicDraftUI.IsOpen&&Time.timeScale==1&&close==1);
            RelicDraftUI.Show(player.gameObject,loadout,RelicDraftUI.PrepareChoices(player.gameObject,loadout),()=>close++);yield return null;GameObject.Find("Relic Draft").GetComponentsInChildren<Button>().First(x=>x.name=="Skip").onClick.Invoke();yield return null;Check("draft can reopen repeatedly",close==2&&!RelicDraftUI.IsOpen);
            loadout.Clear();Check("clear removes all abilities",loadout.Active.Count==0);Check("MonsterTest has repeat station",FindObjectsByType<RelicShrine>(FindObjectsSortMode.None).Any(s=>s.repeatable));Status="Complete";
        }
    }
}
#endif
