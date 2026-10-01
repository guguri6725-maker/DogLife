using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    public sealed class BoneWeaponProjectile : MonoBehaviour
    {
        Transform owner;
        InventoryWeapon item;
        BasicWeaponMoveset move;
        Vector3 forward;
        Color tint;
        float distance,reach,age,visualScale=1f;
        bool returning;
        readonly HashSet<CombatHealth> outward=new HashSet<CombatHealth>(),inward=new HashSet<CombatHealth>();
        LineRenderer visual;
        Material effectMaterial;
        float power=1,speedScale=1;
        float HitRadius=>item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?.19f*visualScale:.51f*visualScale;
        Vector3 BoomerangHalfExtents=>new Vector3(HitRadius,move.BoomerangHalfThickness*visualScale,HitRadius);
        public event System.Action Returned;
        void OnDestroy(){Returned?.Invoke();}
        public static BoneWeaponProjectile Launch(Vector3 position,Vector3 direction,Transform owner,InventoryWeapon item,BasicWeaponMoveset move,Color color,float reach,float charge=0)
        {
            var go=new GameObject(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?"Bone arrow":"Bone boomerang");go.transform.position=position;
            var p=go.AddComponent<BoneWeaponProjectile>();p.owner=owner;p.item=item;p.move=move;p.forward=direction.normalized;p.tint=color;p.reach=reach;p.visualScale=BoneWeaponVfx.SizeFor(item);
            // 투사체도 합성형의 최대 비행 거리만 두 배로 늘립니다.
            bool synthesized=item.weaponForm==InventoryWeapon.WeaponForm.Synthesized;
            p.effectMaterial=synthesized&&item.strikeAccentPrefab&&item.strikeAccentPrefab.surfaceMaterial
                ?item.strikeAccentPrefab.surfaceMaterial:move.effectMaterial;
            if(synthesized)p.reach*=2f;
            p.visual=go.AddComponent<LineRenderer>();p.visual.sharedMaterial=p.effectMaterial;p.visual.useWorldSpace=false;p.visual.startWidth=.07f*p.visualScale;p.visual.endWidth=.025f*p.visualScale;p.visual.startColor=p.visual.endColor=color;p.visual.numCapVertices=4;
            bool bow=item.weaponCategory==InventoryWeapon.WeaponCategory.Bow;
            if(bow){p.power=1+charge;p.speedScale=Mathf.Lerp(.8f,1.4f,charge);p.visualScale*=Mathf.Lerp(1,1.5f,charge);}
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
            p.Burst(position,Vector3.Cross(Vector3.up,direction),Vector3.up,.2f*p.visualScale);
            return p;
        }
        void Burst(Vector3 position,Vector3 x,Vector3 y,float radius)
        {
            var ring=BoneWeaponVfx.Ring(position,x,y,radius,tint,effectMaterial,visualScale);
            if(item.strikeAccentPrefab)Instantiate(item.strikeAccentPrefab).Initialize(ring.HitShape,tint);
        }
        bool HitEnemy(Collider collider,Vector3 point)
        {
            if(collider.transform.IsChildOf(owner))return false;
            var health=collider.GetComponentInParent<CombatHealth>();
            if(!health||health.player)return false;
            if((returning?inward:outward).Add(health))
            {
                Vector3 contact=collider.ClosestPoint(transform.position);
                if(CombatFeedback.DealDamage(health,(returning?item.returnDamage:item.damage)*power,contact,forward,power,null,tint)&&health.Dead&&owner)
                {
                    var karma=owner.GetComponent<PlayerKarma>();if(karma)karma.RecordDefeat(health,item);
                }
                Burst(point,Vector3.right,Vector3.up,.25f*visualScale);
            }
            return true;
        }
        void CheckBoomerangOverlap()
        {
            // Sweep casts can miss colliders already overlapping the turnaround point.
            // 넓고 얇은 부메랑을 구로 검사하면 발사 직후 바닥까지 닿습니다.
            foreach(var collider in Physics.OverlapBox(transform.position,BoomerangHalfExtents,transform.rotation,~0,QueryTriggerInteraction.Ignore))
            {
                if(collider.transform.IsChildOf(owner)||!collider.GetComponentInParent<CombatHealth>())continue;
                var point=collider.ClosestPoint(transform.position);var offset=point-transform.position;
                bool blocked=false;
                if(!returning&&offset.sqrMagnitude>.000001f)
                    foreach(var obstruction in Physics.RaycastAll(transform.position,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore))
                        if(!obstruction.transform.IsChildOf(owner)&&!obstruction.collider.GetComponentInParent<CombatHealth>()){blocked=true;break;}
                if(!blocked)HitEnemy(collider,point);
            }
        }
        void FixedUpdate()
        {
            age+=Time.fixedDeltaTime;
            if(!owner||age>8||(owner.GetComponent<CombatHealth>()&&owner.GetComponent<CombatHealth>().Dead)){Destroy(gameObject);return;}
            bool bow=item.weaponCategory==InventoryWeapon.WeaponCategory.Bow;
            if(!bow)CheckBoomerangOverlap();
            Vector3 target=owner.GetComponent<CapsuleCollider>()?owner.GetComponent<CapsuleCollider>().bounds.center:owner.position+Vector3.up;
            var direction=returning?(target-transform.position).normalized:forward;
            float length=move.projectileSpeed*speedScale*Time.fixedDeltaTime;
            if(returning&&Vector3.Distance(target,transform.position)<=length+.25f){Destroy(gameObject);return;}
            if(!returning)length=Mathf.Min(length,Mathf.Max(0,reach-distance));
            // 화살은 캡슐, 부메랑은 비행면에 맞춘 얇은 상자로 벽과 적을 검사합니다.
            var hits=bow
                ?Physics.CapsuleCastAll(transform.position-direction*.45f*visualScale,transform.position+direction*.3f*visualScale,HitRadius,direction,length,~0,QueryTriggerInteraction.Ignore)
                :Physics.BoxCastAll(transform.position,BoomerangHalfExtents,direction,Quaternion.LookRotation(direction,Vector3.up),length,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(owner))continue;
                var health=hit.collider.GetComponentInParent<CombatHealth>();
                if(health&&health.player)continue;
                if(health)
                {
                    HitEnemy(hit.collider,transform.position+direction*hit.distance);
                    if(bow){Destroy(gameObject);return;}
                }
                else if(!returning){Burst(transform.position+direction*hit.distance,Vector3.right,Vector3.up,.2f*visualScale);if(bow){Destroy(gameObject);return;}returning=true;return;}
            }
            transform.position+=direction*length;distance+=length;
            transform.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,bow?0:age*850,0);
            if(!bow)CheckBoomerangOverlap();
            if(!returning&&distance>=reach){if(bow)Destroy(gameObject);else returning=true;}
        }
    }
}
