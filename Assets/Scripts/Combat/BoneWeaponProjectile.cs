using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    public sealed class BoneWeaponProjectile : MonoBehaviour
    {
        Transform owner;
        InventoryWeapon item;
        BasicWeaponMoveset move;
        Vector3 forward, launchDirection;
        bool hasChaseTarget;
        Color tint;
        float distance,reach,age,visualScale=1f;
        bool returning;
        bool fullyCharged;
        bool piercing,chasing;int chaseHits;float nextChaseHit;CombatHealth lastChased;
        GameAudio.Handle flightSound;
        readonly HashSet<CombatHealth> outward=new HashSet<CombatHealth>(),inward=new HashSet<CombatHealth>();
        LineRenderer visual;
        Material effectMaterial;
        BowFlightVisual bowVisual;
        SynthesizedComboVfx surroundVisual;
        float power=1,speedScale=1;
        // 발사 이후 처치나 무기 교체가 생겨도 왕복 전체에 같은 피해 배율을 사용합니다.
        float karmaDamageMultiplier=1;
        RelicLoadout relics;
        PlayerKarma karma;
        // 화살의 판정은 충전 오라/합성 장식 크기와 분리해 바닥까지 늘어나지 않게 합니다.
        float HitRadius=>item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?move.ProjectileHitRadius:.51f*visualScale;
        Vector3 BoomerangHalfExtents=>new Vector3(HitRadius,move.BoomerangHalfThickness*visualScale,HitRadius);
        public event System.Action Returned;
        void OnDestroy(){flightSound.Stop();if(bowVisual&&Application.isPlaying)bowVisual.Finish();if(surroundVisual)Destroy(surroundVisual.gameObject);Returned?.Invoke();}
        public static BoneWeaponProjectile Launch(Vector3 position,Vector3 direction,Transform owner,InventoryWeapon item,BasicWeaponMoveset move,Color color,float reach,float charge=0,float karmaDamageMultiplier=1)
        {
            var go=new GameObject(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?"Bone arrow":"Bone boomerang");go.transform.position=position;
            var p=go.AddComponent<BoneWeaponProjectile>();p.owner=owner;p.item=item;p.move=move;p.forward=direction.normalized;p.launchDirection=p.forward;p.tint=color;p.reach=reach;p.visualScale=BoneWeaponVfx.SizeFor(item);
            p.karmaDamageMultiplier=karmaDamageMultiplier;
            if(owner){p.relics=owner.GetComponent<RelicLoadout>();p.karma=owner.GetComponent<PlayerKarma>();}
            p.piercing=p.relics&&p.relics.Has(RelicEffect.BowPierce,item.weaponCategory);
            p.chasing=item.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang&&p.relics&&p.relics.Has(RelicEffect.BoomerangChase,item.weaponCategory);
            // 활/부메랑의 비행 거리도 근접 공격과 동일한 일반 1배, 합성 1.5배를 사용합니다.
            // 발사·비행 연출은 공용으로 쓰고 판정 배율은 위의 실제 무기 형태를 따릅니다.
            bool synthesized=true;
            p.effectMaterial=synthesized&&item.strikeAccentPrefab&&item.strikeAccentPrefab.surfaceMaterial
                ?item.strikeAccentPrefab.surfaceMaterial:move.effectMaterial;
            p.reach*=BoneWeaponVfx.SizeFor(item);
            p.visual=go.AddComponent<LineRenderer>();p.visual.sharedMaterial=p.effectMaterial;p.visual.useWorldSpace=false;p.visual.startWidth=.07f*p.visualScale;p.visual.endWidth=.025f*p.visualScale;p.visual.startColor=p.visual.endColor=color;p.visual.numCapVertices=4;
            bool bow=item.weaponCategory==InventoryWeapon.WeaponCategory.Bow;
            p.fullyCharged=bow&&charge>=.99f;
            // 투사체와 함께 움직이는 합성형 금빛 장식은 충돌 판정과 분리합니다.
            var combat=owner?owner.GetComponent<BasicWeaponCombat>():null;
            if(item.weaponForm==InventoryWeapon.WeaponForm.Synthesized&&combat&&combat.synthesizedSurroundVfx)
            {
                p.surroundVisual=Instantiate(combat.synthesizedSurroundVfx);
                p.surroundVisual.InitializeProjectileSurround(go.transform,bow,direction);
            }
            if(!bow)p.flightSound=GameAudio.Play("BoomerangLoop",position,.22f,GameAudio.WeaponPitch(item),follow:go.transform,loop:true);
            if(bow){p.power=Mathf.Lerp(1,move.bowFullChargeMultiplier,Mathf.Clamp01(charge));p.speedScale=Mathf.Lerp(.8f,1.4f,charge);p.visualScale*=Mathf.Lerp(1,1.5f,charge);}
            go.transform.rotation=Quaternion.LookRotation(direction);
            if(!bow&&item.visualPrefab)
            {
                var model=Instantiate(item.visualPrefab,go.transform,false);
                model.transform.localRotation=Quaternion.Euler(0,-90,0);
                var renderer=model.GetComponentInChildren<Renderer>();
                if(renderer)model.transform.position+=go.transform.position-renderer.bounds.center;
                p.visual.enabled=false;
            }
            p.visual.positionCount=bow?5:3;p.visual.SetPositions(bow?new[]{new Vector3(0,0,-.45f),new Vector3(0,0,.3f),new Vector3(-.12f,0,.1f),new Vector3(0,0,.3f),new Vector3(.12f,0,.1f)}:new[]{new Vector3(-.3f,0,-.18f),new Vector3(0,0,.17f),new Vector3(.3f,0,-.18f)});
            for(int i=0;i<p.visual.positionCount;i++)p.visual.SetPosition(i,p.visual.GetPosition(i)*p.visualScale);
            p.visual.startWidth=(bow?.14f:.2f)*p.visualScale;
            if(!bow)
            {
                var haloGo=new GameObject("Boomerang energy halo");haloGo.transform.SetParent(go.transform,false);
                var halo=haloGo.AddComponent<LineRenderer>();halo.useWorldSpace=false;halo.sharedMaterial=p.effectMaterial;halo.positionCount=33;
                for(int i=0;i<33;i++){float angle=i*Mathf.PI/16;halo.SetPosition(i,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.45f*p.visualScale);}
                halo.startWidth=halo.endWidth=.12f*p.visualScale;halo.startColor=halo.endColor=new Color(color.r,color.g,color.b,.9f);
            }
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=p.effectMaterial;trail.time=bow?.18f:.3f;trail.startWidth=(bow?.16f:.35f)*p.visualScale;trail.endWidth=0;trail.startColor=color;trail.endColor=new Color(color.r,color.g,color.b,0);trail.minVertexDistance=.04f;
            if(synthesized)
            {
                trail.time*=1.8f;trail.numCornerVertices=4;trail.numCapVertices=4;
                var gradient=new Gradient();
                gradient.SetKeys(new[]{new GradientColorKey(Color.Lerp(color,Color.white,.65f),0),new GradientColorKey(color,.25f),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.65f,.35f),new GradientAlphaKey(0,1)});
                trail.colorGradient=gradient;
            }
            if(bow&&move.bowFlightVisual)
            {
                // 장식 프리팹은 피해 판정과 별도로 비행을 표현합니다.
                p.visual.enabled=false;trail.enabled=false;
                p.bowVisual=Instantiate(move.bowFlightVisual,go.transform,false);
                p.bowVisual.Initialize(item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid,synthesized,charge);
                if(p.fullyCharged&&p.relics&&p.relics.Level(RelicEffect.BowSplitShot)==3){
                    trail.enabled=true;trail.time=.35f;trail.startWidth=.22f*p.visualScale;
                    trail.startColor=item.weaponForm==InventoryWeapon.WeaponForm.Synthesized?new Color(1,.8f,.35f,.9f):Color.Lerp(color,Color.white,.7f);
                }
            }
            if(bow&&p.fullyCharged&&p.relics&&p.relics.Level(RelicEffect.BowSplitShot)==3)
                RelicMasteryVfx.Create(item,RelicMasteryVfx.Style.Arrow,position,direction,.2f,0,go.transform);
            if(bow&&move.bowReleaseVisual)
                Instantiate(move.bowReleaseVisual,position,Quaternion.LookRotation(direction)).Initialize(item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid,synthesized,charge);
            else p.Burst(position,Vector3.Cross(Vector3.up,direction),Vector3.up,.2f*p.visualScale);
            return p;
        }
        void Burst(Vector3 position,Vector3 x,Vector3 y,float radius)
        {
            if(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow&&move.bowReleaseVisual)
            {
                Instantiate(move.bowReleaseVisual,position,Quaternion.LookRotation(forward)).Initialize(item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid,true,0,true);return;
            }
            var ring=BoneWeaponVfx.Ring(position,x,y,radius,tint,effectMaterial,visualScale);
            if(item.strikeAccentPrefab)Instantiate(item.strikeAccentPrefab).Initialize(ring.HitShape,tint);
        }
        bool HitEnemy(Collider collider,Vector3 point)
        {
            if(!MonsterHurtbox.Accepts(collider)||collider.transform.IsChildOf(owner))return false;
            var health=collider.GetComponentInParent<CombatHealth>();
            if(!health||health.player||health.Dead)return false;
            if(chasing&&(returning||Time.time<nextChaseHit))return false;
            if((returning?inward:outward).Add(health))
            {
                Vector3 contact=collider.ClosestPoint(transform.position);
                float relicBonus=relics?relics.DamageMultiplier(item.weaponCategory,item.weaponForm==InventoryWeapon.WeaponForm.Synthesized,false,karma?Mathf.Clamp01(-karma.Value/karma.Maximum):0):1f;
                if(CombatFeedback.DealDamage(health,(returning?item.returnDamage:item.damage)*power*karmaDamageMultiplier*relicBonus,contact,forward,power,owner,tint,emphasized:fullyCharged,rangedHit:true))
                {
                    GameAudio.WeaponHit(item,contact);
                    // 근접과 동일한 공통 명중 경로에서 카메라와 짧은 슬로를 처리합니다.
                    if(health.Dead&&owner){var karma=owner.GetComponent<PlayerKarma>();if(karma)karma.RecordDefeat(health,item);}
                }
                Burst(point,Vector3.right,Vector3.up,.25f*visualScale);
                if(chasing){chaseHits++;lastChased=health;nextChaseHit=Time.time+.22f;if(chaseHits>=4)returning=true;}
            }
            return true;
        }
        void CheckBoomerangOverlap()
        {
            // Sweep casts can miss colliders already overlapping the turnaround point.
            // 넓고 얇은 부메랑을 구로 검사하면 발사 직후 바닥까지 닿습니다.
            foreach(var collider in Physics.OverlapBox(transform.position,BoomerangHalfExtents,transform.rotation,~0,QueryTriggerInteraction.Collide))
            {
                if(!MonsterHurtbox.Accepts(collider)||collider.transform.IsChildOf(owner)||!collider.GetComponentInParent<CombatHealth>())continue;
                var point=collider.ClosestPoint(transform.position);var offset=point-transform.position;
                bool blocked=false;
                if(!returning&&offset.sqrMagnitude>.000001f)
                    foreach(var obstruction in Physics.RaycastAll(transform.position,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Collide))
                        if(MonsterHurtbox.Accepts(obstruction.collider)&&!obstruction.transform.IsChildOf(owner)&&!obstruction.collider.GetComponentInParent<CombatHealth>()){blocked=true;break;}
                if(!blocked)HitEnemy(collider,point);
            }
        }
        void AimChase()
        {
            if(!chasing||returning||Time.time<nextChaseHit)return;
            hasChaseTarget=false;
            outward.Clear();CombatHealth chosen=null;Vector3 point=Vector3.zero;float best=float.MaxValue;
            foreach(var c in Physics.OverlapSphere(transform.position,reach,~0,QueryTriggerInteraction.Collide)){
                if(!MonsterHurtbox.Accepts(c))continue;var h=c.GetComponentInParent<CombatHealth>();if(!h||h.player||h.Dead)continue;
                var d=c.bounds.center-transform.position;
                if(Physics.Raycast(transform.position,d.normalized,out var wall,d.magnitude,1<<8,QueryTriggerInteraction.Ignore)&&wall.collider.GetComponentInParent<CombatHealth>()!=h)continue;
                float rank=d.sqrMagnitude+(h==lastChased?reach*reach:0);if(rank<best){best=rank;chosen=h;point=c.bounds.center;}
            }
            if(chosen){hasChaseTarget=true;forward=(point-transform.position).normalized;}
        }
        void FixedUpdate()
        {
            age+=Time.fixedDeltaTime;
            if(!owner||age>8||(owner.GetComponent<CombatHealth>()&&owner.GetComponent<CombatHealth>().Dead)){Destroy(gameObject);return;}
            bool bow=item.weaponCategory==InventoryWeapon.WeaponCategory.Bow;
            AimChase();
            if(!bow)CheckBoomerangOverlap();
            Vector3 target=owner.GetComponent<CapsuleCollider>()?owner.GetComponent<CapsuleCollider>().bounds.center:owner.position+Vector3.up;
            // Piercing arrows retain the launch axis, including after impact feedback.
            var direction=bow?launchDirection:(returning?(target-transform.position).normalized:forward);
            float length=move.projectileSpeed*speedScale*Time.fixedDeltaTime;
            if(returning&&Vector3.Distance(target,transform.position)<=length+.25f){GameAudio.Play("BoomerangReturn",target,.65f,GameAudio.WeaponPitch(item));Destroy(gameObject);return;}
            if(!returning&&(!chasing||!hasChaseTarget))length=Mathf.Min(length,Mathf.Max(0,reach-distance));
            // 화살 중심의 작은 구를 이동 구간 전체로 검사합니다. 뒤쪽 잔상은 충돌하지 않습니다.
            var hits=bow
                ?Physics.SphereCastAll(transform.position,HitRadius,direction,length,~0,QueryTriggerInteraction.Collide)
                :Physics.BoxCastAll(transform.position,BoomerangHalfExtents,direction,Quaternion.LookRotation(direction,Vector3.up),length,~0,QueryTriggerInteraction.Collide);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(!MonsterHurtbox.Accepts(hit.collider)||hit.transform.IsChildOf(owner))continue;
                var health=hit.collider.GetComponentInParent<CombatHealth>();
                if(health&&health.player)continue;
                if(health)
                {
                    HitEnemy(hit.collider,transform.position+direction*hit.distance);
                    if(bow&&!piercing){Destroy(gameObject);return;}
                }
                else if(!returning){Burst(transform.position+direction*hit.distance,Vector3.right,Vector3.up,.2f*visualScale);if(bow){Destroy(gameObject);return;}returning=true;return;}
            }
            transform.position+=direction*length;distance+=length;
            transform.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,bow?0:age*850,0);
            if(!bow)CheckBoomerangOverlap();
            if(!returning&&(!chasing||!hasChaseTarget)&&distance>=reach){if(bow)Destroy(gameObject);else returning=true;}
        }
    }
}
