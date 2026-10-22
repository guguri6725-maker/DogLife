using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    // Pattern modifiers share the existing damage / karma path. No new animation clips.
    public sealed class RelicCombatEffects : MonoBehaviour
    {
        [SerializeField] float waveSpeed=12,waveDistance=8,waveAngle=70;
        [SerializeField] float shockRadius=2.4f,stormAngle=100;
        [SerializeField] float phantomRange=5;
        [SerializeField] float orbitRadius=1.8f,orbitSpeed=190,orbitHitInterval=.6f,orbitDamage=.5f;
        [SerializeField,Min(.1f)] float daggerHasteDuration=2f;
        float hasteUntil; InventoryWeapon hasteItem; GameObject hasteVisual;
        public float MovementMultiplier => hasteItem && Valid(hasteItem) && Time.time<hasteUntil && Has(RelicEffect.DaggerHaste,hasteItem) ? 1f+relics.Value(RelicEffect.DaggerHaste,hasteItem.weaponCategory) : 1f;
        PlayerInventory inventory; PlayerWeapons weapons; CombatHealth health; PlayerKarma karma; RelicLoadout relics;
        GameObject orbit;InventoryWeapon orbitItem;
        float orbitAngle;
        int phantomSide; CombatHealth phantomTarget; Vector3 phantomAxis;
        readonly List<Dictionary<CombatHealth,float>> orbitHits=new List<Dictionary<CombatHealth,float>>();
        readonly List<Transform> orbitBlades=new List<Transform>();
        int stormVisualTick;
        void Awake(){inventory=GetComponent<PlayerInventory>();weapons=GetComponent<PlayerWeapons>();health=GetComponent<CombatHealth>();karma=GetComponent<PlayerKarma>();relics=RelicLoadout.Get(gameObject);}
        void OnEnable(){if(relics)relics.Changed+=ClearEffects;}
        bool Valid(InventoryWeapon item)=>isActiveAndEnabled&&health&&!health.Dead&&inventory&&inventory.EquippedWeapon==item;
        bool Has(RelicEffect e,InventoryWeapon item)=>relics&&relics.Has(e,item.weaponCategory);
        public bool ReplaceStrike(InventoryWeapon item,BasicWeaponMoveset move,int step,Vector3 origin,Vector3 forward,float reach,float power,Color tint)
        {
            bool spin=item.weaponCategory==InventoryWeapon.WeaponCategory.Sword&&Has(step==2?RelicEffect.SwordFinisherSurge:RelicEffect.SwordSpin,item);
            bool storm=item.weaponCategory==InventoryWeapon.WeaponCategory.Spear&&step==2&&Has(RelicEffect.SpearStorm,item);
            if(!spin&&!storm)return false;
            Area(item,origin,forward,reach,spin?360:stormAngle,power,tint,null,true);
            if(storm)StormThrusts(item,move,origin,forward,reach);else Arc(item,move,origin,forward,reach,360,step);
            if(spin&&step==2)StartCoroutine(WaveSeries(item,move,origin,forward,power,tint,RelicEffect.SwordFinisherSurge,reach,143.24f));
            return true;
        }
        public void AfterStrike(InventoryWeapon item,BasicWeaponMoveset move,int step,Vector3 origin,Vector3 forward,float reach,float power,Color tint,bool hit)
        {
            if(item.weaponCategory==InventoryWeapon.WeaponCategory.Axe&&step<2&&hit&&Has(RelicEffect.AxeShockwave,item)){
                Vector3 p=origin+forward*Mathf.Min(reach,1.5f);Area(item,p,forward,shockRadius,360,power*.5f,tint);Arc(item,move,p,forward,shockRadius,360,step);
            }
            if(item.weaponCategory==InventoryWeapon.WeaponCategory.Spear&&step<2&&hit&&Has(RelicEffect.SpearRipple,item))StartCoroutine(WaveSeries(item,move,origin,forward,power*.5f,tint,RelicEffect.SpearRipple));
            if(item.weaponCategory==InventoryWeapon.WeaponCategory.Dagger){
                if(hit&&Has(RelicEffect.DaggerPhantomStrike,item)){
                    var target=weapons.LastMeleeTarget;if(!target||target.Dead)target=FrontTarget(origin,forward,phantomRange);if(target)for(int i=0;i<relics.Level(RelicEffect.DaggerPhantomStrike);i++)Phantom(item,move,target,forward,power*.35f,tint);
                }
                if(Has(RelicEffect.DaggerHaste,item)) { hasteItem=item;hasteUntil=Time.time+daggerHasteDuration;if(!hasteVisual)CreateHasteVisual(item,move); }
            }
        }
        public void Whirlwind(InventoryWeapon item,BasicWeaponMoveset move,Vector3 origin,Vector3 forward,float reach,float power,Color tint)
        {Area(item,origin,forward,reach,360,power,tint,null,true);Arc(item,move,origin,forward,reach,360,2);if(relics.Level(RelicEffect.AxeWhirlwind)==3)MasteryAccent(item,move,origin,forward,reach);}
        public List<CombatHealth> Area(InventoryWeapon item,Vector3 origin,Vector3 forward,float radius,float angle,float power,Color tint,HashSet<CombatHealth> already=null,bool melee=false)
        {
            var hits=new List<CombatHealth>();var seen=already??new HashSet<CombatHealth>();
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            foreach(var c in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Collide)){
                if(!MonsterHurtbox.Accepts(c))continue;
                var target=c.GetComponentInParent<CombatHealth>();if(!target||target.player||target.Dead||seen.Contains(target))continue;
                var point=c.ClosestPoint(origin);var delta=point-origin;var flat=Vector3.ProjectOnPlane(delta,Vector3.up);
                if(angle<360&&flat.sqrMagnitude>.001f&&Vector3.Angle(forward,flat)>angle*.5f)continue;
                if(delta.sqrMagnitude>.001f&&Physics.Raycast(origin,delta.normalized,out var wall,delta.magnitude,1<<8,QueryTriggerInteraction.Ignore)&&wall.collider.GetComponentInParent<CombatHealth>()!=target)continue;
                seen.Add(target);if(Deal(item,target,point,delta,power,tint,melee))hits.Add(target);
            }
            return hits;
        }
        bool Deal(InventoryWeapon item,CombatHealth target,Vector3 point,Vector3 direction,float power,Color tint,bool melee=false)
        {
            if(!target||target.Dead)return false;
            if(!CombatFeedback.DealDamage(target,item.damage*power,point,direction,1,transform,tint))return false;
            if(melee){var combo=GetComponent<BasicWeaponCombat>();var response=target.GetComponent<CombatFeedback>();if(combo&&response)response.ApplyMeleeAdvantage(item.weaponCategory,combo.MeleeFinisher,combo.MeleePoise);}
            GameAudio.WeaponHit(item,point);if(target.Dead&&karma)karma.RecordDefeat(target,item);return true;
        }
        CombatHealth FrontTarget(Vector3 origin,Vector3 forward,float range)
        {
            CombatHealth best=null;float distance=float.MaxValue;
            foreach(var c in Physics.OverlapSphere(origin,range,~0,QueryTriggerInteraction.Collide)){
                if(!MonsterHurtbox.Accepts(c))continue;var h=c.GetComponentInParent<CombatHealth>();if(!h||h.player||h.Dead)continue;
                var delta=c.bounds.center-origin;if(Vector3.Angle(forward,delta)>65||delta.sqrMagnitude>=distance)continue;
                if(Physics.Raycast(origin,delta.normalized,out var wall,delta.magnitude,1<<8,QueryTriggerInteraction.Ignore)&&wall.collider.GetComponentInParent<CombatHealth>()!=h)continue;
                best=h;distance=delta.sqrMagnitude;
            }return best;
        }
        void StormThrusts(InventoryWeapon item,BasicWeaponMoveset move,Vector3 origin,Vector3 forward,float reach){
            var right=Vector3.Cross(Vector3.up,forward).normalized;
            for(int i=0;i<3;i++){
                float angle=-stormAngle*.4f+(i+((stormVisualTick%3)-1)*.14f)*stormAngle*.4f;
                Vector3 direction=Quaternion.AngleAxis(angle,Vector3.up)*forward;
                StrikeVisual(item,move,origin+right*((i-1)*.12f)+Vector3.up*((stormVisualTick%3-1)*.07f),direction,reach,2);
            }
            stormVisualTick++;
        }
        void MasteryAccent(InventoryWeapon item,BasicWeaponMoveset move,Vector3 origin,Vector3 forward,float radius){
            var style=item.weaponCategory==InventoryWeapon.WeaponCategory.Axe?RelicMasteryVfx.Style.Cyclone:RelicMasteryVfx.Style.Shadow;
            RelicMasteryVfx.Create(item,style,origin,forward,radius,style==RelicMasteryVfx.Style.Cyclone?.32f:.45f);
        }

        IEnumerator WaveSeries(InventoryWeapon item,BasicWeaponMoveset move,Vector3 origin,Vector3 forward,float power,Color tint,RelicEffect effect,float radius=1.4f,float angle=70){
            int count=Mathf.Max(1,relics.Level(effect));
            for(int i=0;i<count;i++){
                if(!Valid(item)||!Has(effect,item))yield break;

                StartCoroutine(Wave(item,move,origin,forward,power,tint,radius,angle,count==3));
                if(i<count-1)yield return new WaitForSeconds(.18f);
            }
        }
        IEnumerator Wave(InventoryWeapon item,BasicWeaponMoveset move,Vector3 origin,Vector3 forward,float power,Color tint,float radius=1.4f,float angle=70,bool mastery=false)
        {
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            var hit=new HashSet<CombatHealth>();float travelled=0;
            var masteryFx=mastery?RelicMasteryVfx.Create(item,item.weaponCategory==InventoryWeapon.WeaponCategory.Sword?RelicMasteryVfx.Style.Sword:RelicMasteryVfx.Style.Ripple,origin,forward,radius,waveDistance/Mathf.Max(.1f,waveSpeed)+.2f):null;
            while(Valid(item)&&travelled<waveDistance){
                Vector3 point=origin+forward*travelled;
                if(masteryFx)masteryFx.transform.position=point;
                Area(item,point,forward,radius,angle,power,tint,hit);Arc(item,move,point,forward,radius,angle,0);
                float step=Mathf.Min(.08f*waveSpeed,waveDistance-travelled);
                if(Physics.Raycast(point,forward,step,1<<8,QueryTriggerInteraction.Ignore))break;
                travelled+=step;yield return new WaitForSeconds(.08f);
            }
            if(masteryFx)Destroy(masteryFx.gameObject);
        }
        void CreateHasteVisual(InventoryWeapon item,BasicWeaponMoveset move)
        {
            hasteVisual=new GameObject("Dagger haste wind trails");hasteVisual.transform.SetParent(transform,false);
            var material=item.strikeAccentPrefab?item.strikeAccentPrefab.surfaceMaterial:move.effectMaterial;
            var color=WeaponTint(item);bool synth=item.weaponForm==InventoryWeapon.WeaponForm.Synthesized;
            for(int i=0;i<(synth?5:3);i++){
                var node=new GameObject("Wind ribbon "+i);node.transform.SetParent(hasteVisual.transform,false);
                node.transform.localPosition=new Vector3((i%2==0?-1:1)*(.23f+i*.025f),.2f+i*.16f,-.12f);
                var trail=node.AddComponent<TrailRenderer>();trail.sharedMaterial=material;trail.time=.24f;trail.minVertexDistance=.025f;
                trail.widthMultiplier=i>=3?.055f:.09f;trail.widthCurve=AnimationCurve.EaseInOut(0,1,1,0);trail.numCapVertices=4;trail.numCornerVertices=4;
                var c=i>=3?new Color(1,.76f,.32f):color;var g=new Gradient();
                g.SetKeys(new[]{new GradientColorKey(Color.Lerp(c,Color.white,.65f),0),new GradientColorKey(c,.35f),new GradientColorKey(c,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.15f),new GradientAlphaKey(0,1)});trail.colorGradient=g;
                trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
            }
            Arc(item,move,transform.position+Vector3.up*.12f,transform.forward,.6f,300,0);
        }
        void Phantom(InventoryWeapon item,BasicWeaponMoveset move,CombatHealth target,Vector3 forward,float power,Color tint)
        {
            if(!Valid(item)||!target||target.Dead)return;
            if(phantomTarget!=target){phantomTarget=target;phantomAxis=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;phantomSide=0;}
            var collider=target.GetComponentInChildren<Collider>();Vector3 center=collider?collider.bounds.center:target.transform.position+Vector3.up;
            // Alternate ground-level cardinal positions with elevated diagonals.
            bool elevated=(phantomSide&1)!=0;
            Vector3 horizontal=Quaternion.AngleAxis(phantomSide*45,Vector3.up)*phantomAxis;
            Vector3 offset=horizontal*1.4f+Vector3.up*(elevated?1.6f:0f);
            Vector3 side=offset.normalized;phantomSide=(phantomSide+1)%8;
            Vector3 from=center+offset;
            StrikeVisual(item,move,from,-side,1.5f,0);
            RelicShadowEcho.Create(transform,new Vector3(from.x,transform.position.y+offset.y,from.z),-side);
            Deal(item,target,center,-side,power,tint);
            if(relics.Level(RelicEffect.DaggerPhantomStrike)==3)MasteryAccent(item,move,from,-side,.45f);
        }
        static Color WeaponTint(InventoryWeapon item)=>item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?new Color(1,.12f,.18f,.85f):new Color(.12f,.55f,1,.85f);
        void Decorate(InventoryWeapon item,BoneWeaponVfx shape,Vector3 origin,Vector3 forward,int step)
        {
            var combat=GetComponent<BasicWeaponCombat>();
            if(combat&&combat.synthesizedComboVfx)Instantiate(combat.synthesizedComboVfx).Initialize(shape.HitShape,shape.ImpactShape,origin,forward,WeaponTint(item),item.weaponCategory,step,0);
            if(combat&&combat.synthesizedSurroundVfx&&item.weaponForm==InventoryWeapon.WeaponForm.Synthesized)
                Instantiate(combat.synthesizedSurroundVfx).InitializeSurround(shape.HitShape,shape.ImpactShape,origin,forward,item.weaponCategory,step,0);
        }
        void StrikeVisual(InventoryWeapon item,BasicWeaponMoveset move,Vector3 origin,Vector3 forward,float reach,int step)
        {
            bool previous=BoneWeaponVfx.HideLegacyVisuals;
            var combat=GetComponent<BasicWeaponCombat>();BoneWeaponVfx.HideLegacyVisuals=combat&&combat.synthesizedComboVfx;
            try {var shape=BoneWeaponVfx.Strike(origin,forward,reach,item.weaponCategory,step,WeaponTint(item),item.strikeAccentPrefab?item.strikeAccentPrefab.surfaceMaterial:move.effectMaterial);Decorate(item,shape,origin,forward,step);}
            finally {BoneWeaponVfx.HideLegacyVisuals=previous;}
        }
        void Arc(InventoryWeapon item,BasicWeaponMoveset move,Vector3 origin,Vector3 forward,float radius,float angle,int step)
        {
            var points=new Vector3[65];
            for(int i=0;i<points.Length;i++)points[i]=origin+Quaternion.AngleAxis(-angle*.5f+angle*i/(points.Length-1),Vector3.up)*forward*radius;
            bool previous=BoneWeaponVfx.HideLegacyVisuals;var combat=GetComponent<BasicWeaponCombat>();
            BoneWeaponVfx.HideLegacyVisuals=combat&&combat.synthesizedComboVfx;
            try {
                var shape=BoneWeaponVfx.Line(points,WeaponTint(item),item.strikeAccentPrefab?item.strikeAccentPrefab.surfaceMaterial:move.effectMaterial,.42f*BoneWeaponVfx.SizeFor(item),.27f);
                shape.HitShape.widthMultiplier=.42f*BoneWeaponVfx.SizeFor(item);
                Decorate(item,shape,origin,forward,step);
            } finally {BoneWeaponVfx.HideLegacyVisuals=previous;}
        }
        void Update()
        {
            if(MovementMultiplier<=1&&hasteVisual){Destroy(hasteVisual);hasteVisual=null;}
            var item=inventory?inventory.EquippedWeapon:null;
            bool enabledOrbit=item&&Valid(item)&&item.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang&&Has(RelicEffect.BoomerangOrbit,item);
            if(!enabledOrbit||orbitItem!=item){if(orbit)Destroy(orbit);orbit=null;orbitItem=null;orbitHits.Clear();orbitBlades.Clear();}
            if(!enabledOrbit)return;
            int count=relics.Level(RelicEffect.BoomerangOrbit);
            if(!orbit){
                orbit=new GameObject("Relic orbit boomerang");orbitItem=item;
                var combat=GetComponent<BasicWeaponCombat>();Material trailMaterial=null;
                if(combat)foreach(var moveset in combat.movesets)if(moveset&&moveset.category==item.weaponCategory)trailMaterial=moveset.effectMaterial;
                if(item.strikeAccentPrefab)trailMaterial=item.strikeAccentPrefab.surfaceMaterial;
                for(int index=0;index<count;index++){
                    var blade=new GameObject("Orbit blade "+index).transform;blade.SetParent(orbit.transform,false);orbitBlades.Add(blade);orbitHits.Add(new Dictionary<CombatHealth,float>());
                    if(item.visualPrefab){var model=Instantiate(item.visualPrefab,blade);foreach(var c in model.GetComponentsInChildren<Collider>())c.enabled=false;var r=model.GetComponentInChildren<Renderer>();if(r)model.transform.position+=blade.position-r.bounds.center;}
                    if(trailMaterial){var trail=blade.gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=trailMaterial;trail.time=count==3?.3f:.18f;trail.minVertexDistance=.04f;trail.widthMultiplier=(count==3?.24f:.16f)*BoneWeaponVfx.SizeFor(item);trail.widthCurve=AnimationCurve.EaseInOut(0,1,1,0);trail.startColor=Color.Lerp(WeaponTint(item),Color.white,.55f);var faded=WeaponTint(item);faded.a=0;trail.endColor=faded;trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
                    if(combat&&combat.synthesizedSurroundVfx&&item.weaponForm==InventoryWeapon.WeaponForm.Synthesized)Instantiate(combat.synthesizedSurroundVfx).InitializeProjectileSurround(blade,false,transform.forward);
                    if(count==3)RelicMasteryVfx.Create(item,RelicMasteryVfx.Style.Orbit,blade.position,Vector3.forward,.5f,0,blade);
                }
            }
            if(SkillWindow.BlocksGameplayInput||BossCinematicUI.Hidden)return;
            orbitAngle+=orbitSpeed*Time.deltaTime;var center=GetComponent<CapsuleCollider>().bounds.center;
            for(int index=0;index<orbitBlades.Count;index++){
                var blade=orbitBlades[index];float angle=orbitAngle+index*360f/orbitBlades.Count;
                blade.position=center+Quaternion.AngleAxis(angle,Vector3.up)*Vector3.forward*orbitRadius;blade.rotation=Quaternion.Euler(0,angle*3,0);
                var seen=new HashSet<CombatHealth>();foreach(var c in Physics.OverlapSphere(blade.position,.65f,~0,QueryTriggerInteraction.Collide)){
                    if(!MonsterHurtbox.Accepts(c))continue;var h=c.GetComponentInParent<CombatHealth>();if(!h||h.player||h.Dead||!seen.Add(h))continue;
                    if(orbitHits[index].TryGetValue(h,out float until)&&Time.time<until)continue;
                    Vector3 delta=c.ClosestPoint(center)-center;if(Physics.Raycast(center,delta.normalized,delta.magnitude,1<<8,QueryTriggerInteraction.Ignore))continue;
                    orbitHits[index][h]=Time.time+orbitHitInterval;Deal(item,h,c.ClosestPoint(blade.position),delta,orbitDamage/(karma?karma.AttackSpeedMultiplier:1),WeaponTint(item));
                }
            }
        }
        void ClearEffects(){hasteUntil=0;hasteItem=null;if(hasteVisual)Destroy(hasteVisual);hasteVisual=null;StopAllCoroutines();if(orbit)Destroy(orbit);orbit=null;orbitHits.Clear();orbitBlades.Clear();}
        void OnDisable(){if(relics)relics.Changed-=ClearEffects;ClearEffects();}
    }
}
