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
        float power=1,speedScale=1;
        float HitRadius=>item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?.19f*visualScale:.51f*visualScale;
        public event System.Action Returned;
        void OnDestroy(){Returned?.Invoke();}
        public static BoneWeaponProjectile Launch(Vector3 position,Vector3 direction,Transform owner,InventoryWeapon item,BasicWeaponMoveset move,Color color,float reach,float charge=0)
        {
            var go=new GameObject(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?"Bone arrow":"Bone boomerang");go.transform.position=position;
            var p=go.AddComponent<BoneWeaponProjectile>();p.owner=owner;p.item=item;p.move=move;p.forward=direction.normalized;p.tint=color;p.reach=reach;p.visualScale=BoneWeaponVfx.SizeFor(item);
            p.visual=go.AddComponent<LineRenderer>();p.visual.sharedMaterial=move.effectMaterial;p.visual.useWorldSpace=false;p.visual.startWidth=.07f*p.visualScale;p.visual.endWidth=.025f*p.visualScale;p.visual.startColor=p.visual.endColor=color;p.visual.numCapVertices=4;
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
                var halo=haloGo.AddComponent<LineRenderer>();halo.useWorldSpace=false;halo.sharedMaterial=move.effectMaterial;halo.positionCount=33;
                for(int i=0;i<33;i++){float angle=i*Mathf.PI/16;halo.SetPosition(i,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.45f*p.visualScale);}
                halo.startWidth=halo.endWidth=.12f*p.visualScale;halo.startColor=halo.endColor=new Color(color.r,color.g,color.b,.9f);
            }
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=move.effectMaterial;trail.time=bow?.18f:.3f;trail.startWidth=(bow?.16f:.35f)*p.visualScale;trail.endWidth=0;trail.startColor=color;trail.endColor=new Color(color.r,color.g,color.b,0);trail.minVertexDistance=.04f;
            BoneWeaponVfx.Ring(position,Vector3.Cross(Vector3.up,direction),Vector3.up,.2f*p.visualScale,color,move.effectMaterial,p.visualScale);
            return p;
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
                BoneWeaponVfx.Ring(point,Vector3.right,Vector3.up,.25f*visualScale,tint,move.effectMaterial,visualScale);
            }
            return true;
        }
        void CheckBoomerangOverlap()
        {
            // Sweep casts can miss colliders already overlapping the turnaround point.
            foreach(var collider in Physics.OverlapSphere(transform.position,HitRadius,~0,QueryTriggerInteraction.Ignore))
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
            // 화살은 길이까지 포함하고, 부메랑은 회전하는 원형 외곽을 검사합니다.
            var hits=bow
                ?Physics.CapsuleCastAll(transform.position-direction*.45f*visualScale,transform.position+direction*.3f*visualScale,HitRadius,direction,length,~0,QueryTriggerInteraction.Ignore)
                :Physics.SphereCastAll(transform.position,HitRadius,direction,length,~0,QueryTriggerInteraction.Ignore);
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
                else if(!returning){BoneWeaponVfx.Ring(transform.position+direction*hit.distance,Vector3.right,Vector3.up,.2f*visualScale,tint,move.effectMaterial,visualScale);if(bow){Destroy(gameObject);return;}returning=true;return;}
            }
            transform.position+=direction*length;distance+=length;
            transform.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,bow?0:age*850,0);
            if(!bow)CheckBoomerangOverlap();
            if(!returning&&distance>=reach){if(bow)Destroy(gameObject);else returning=true;}
        }
    }
}
