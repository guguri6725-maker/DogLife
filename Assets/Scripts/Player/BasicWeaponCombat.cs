using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(PlayerInventory),typeof(PlayerWeapons),typeof(PlayerMotor))]
    public sealed class BasicWeaponCombat : MonoBehaviour
    {
        public BasicWeaponMoveset[] movesets;
        public Animator animator;
        [SerializeField] float continuationGrace=.15f;
        PlayerInventory inventory;
        PlayerMotor motor;
        PlayerWeapons weapons;
        CombatHealth health;
        BasicWeaponMoveset move;
        InventoryWeapon item;
        int step=-1;
        float started,endsAt;
        bool queued,struck,secondStruck;
        Vector3 direction;
        [SerializeField,Range(0,.15f),Tooltip("실제 타격 직후에만 방향을 고정하는 시간")] private float strikeFacingLock=.045f;
        float facingLockedUntil;
        public bool LocksFacing=>step>=0&&(Time.time<facingLockedUntil||CombatFeedback.Paused(this));
        const string DaggerLayer="Blade Upper Body";
        bool upperBodyAttack;
        [SerializeField] private float bowChargeSeconds=1.2f;
        [SerializeField] private float spearThrustInterval=.08f;
        bool charging;
        float chargeStarted,shotCharge;
        int thrustCount;
        BoneWeaponProjectile activeBoomerang;
        LineRenderer chargeArrow;
        public bool IsCharging=>charging;
        // 활 클립은 옆으로 서는 자세이므로 모델 전체의 방향으로 조준축을 맞춥니다.
        public float VisualYawOffset=>move&&move.bowDrawClip&&item&&item.weaponCategory==InventoryWeapon.WeaponCategory.Bow?move.bowVisualYawOffset:0;
        public bool BoomerangInFlight=>activeBoomerang;
        public float ChargeAmount=>charging?Mathf.Clamp01((Time.time-chargeStarted)/Mathf.Max(.1f,bowChargeSeconds)):shotCharge;
        public int ComboStep=>step+1;
        public bool IsAttacking=>charging||(step>=0&&Time.time<endsAt);
        bool UsesUpperBody=>item&&(item.weaponCategory==InventoryWeapon.WeaponCategory.Dagger||item.weaponCategory==InventoryWeapon.WeaponCategory.Bow||item.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang||((item.weaponCategory==InventoryWeapon.WeaponCategory.Sword||item.weaponCategory==InventoryWeapon.WeaponCategory.Spear)&&step<2));
        bool SingleAttack=>item&&(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow||item.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang);
        public bool LocksMovement=>IsAttacking&&!UsesUpperBody;
        public bool Ready=>isActiveAndEnabled&&animator&&inventory&&inventory.EquippedWeapon&&inventory.EquippedWeapon.weaponForm==InventoryWeapon.WeaponForm.Basic;
        void Awake(){inventory=GetComponent<PlayerInventory>();motor=GetComponent<PlayerMotor>();weapons=GetComponent<PlayerWeapons>();health=GetComponent<CombatHealth>();}
        bool Interrupted=>(health&&health.Dead)||!weapons.enabled||SkillWindow.BlocksGameplayInput||(motor&&(motor.IsKnockedBack||motor.IsDashing));
        public void PressAttack()
        {
            if(!Ready||Interrupted)return;
            if(charging)return;
            if(inventory.EquippedWeapon.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang&&activeBoomerang)return;
            if(SingleAttack&&IsAttacking)return;
            if(step<0){StartCombo();return;}
            if(SingleAttack){Cancel();StartCombo();return;}
            if(Time.time<endsAt){if(step<2)queued=true;return;}
            if(step<2&&Time.time<=endsAt+continuationGrace)Begin(step+1);else StartCombo();
        }
        void StartCombo()
        {
            item=inventory.EquippedWeapon;move=null;
            foreach(var candidate in movesets)if(candidate&&candidate.category==item.weaponCategory){move=candidate;break;}
            if(!move||move.attacks==null||move.attacks.Length==0)return;
            Begin(0);
            if(item&&item.weaponCategory==InventoryWeapon.WeaponCategory.Bow){charging=true;chargeStarted=Time.time;shotCharge=0;}
        }
        public void ReleaseAttack()
        {
            if(!charging)return;
            if(Interrupted||inventory.EquippedWeapon!=item){Cancel();return;}
            shotCharge=ChargeAmount;charging=false;
            weapons.RefreshAimDirection();direction=weapons.AimDirection;if(motor)motor.FaceForAttack(direction);
            // 분리된 발사 클립은 처음부터 재생하고 해당 클립의 타격 시간에 화살을 발사합니다.
            float releaseOffset=move.bowDrawClip?0:move.attacks[0].length*.45f;
            started=Time.time-releaseOffset;endsAt=started+move.attacks[0].length;
            if(move.bowDrawClip)animator.CrossFadeInFixedTime(DaggerLayer+"."+move.attacks[0].name,.06f,animator.GetLayerIndex(DaggerLayer),0);
            else animator.Play(DaggerLayer+"."+move.attacks[0].name,animator.GetLayerIndex(DaggerLayer),.45f);
            ClearChargeVisual();
        }
        void Begin(int index)
        {
            facingLockedUntil=0;
            // Sample at execution, not at the buffered click or combo start.
            weapons.RefreshAimDirection();
            direction=weapons.AimDirection;
            if(motor)motor.FaceForAttack(direction);
            step=index;queued=false;struck=false;secondStruck=false;thrustCount=0;started=Time.time;endsAt=started+move.attacks[index].length;
            if(motor&&LocksMovement)motor.StopForAttack();
            upperBodyAttack=UsesUpperBody;
            int layer=upperBodyAttack?animator.GetLayerIndex(DaggerLayer):0;
            if(layer<0){Cancel();return;}
            if(upperBodyAttack)animator.SetLayerWeight(layer,1);
            else
            {
                int upperLayer=animator.GetLayerIndex(DaggerLayer);
                if(upperLayer>=0)animator.SetLayerWeight(upperLayer,0);
            }
            var openingClip=item.weaponCategory==InventoryWeapon.WeaponCategory.Bow&&move.bowDrawClip?move.bowDrawClip:move.attacks[index];
            animator.CrossFadeInFixedTime((upperBodyAttack?DaggerLayer:"Base Layer")+"."+openingClip.name,.09f,layer,0);
        }
        void Update()
        {
            if(CombatFeedback.Paused(this))
            {
                if(step>=0){started+=Time.deltaTime;endsAt+=Time.deltaTime;}
                if(charging)chargeStarted+=Time.deltaTime;
                if(Interrupted)Cancel();return;
            }
            int upperLayer=animator?animator.GetLayerIndex(DaggerLayer):-1;
            if(upperLayer>=0&&!upperBodyAttack)animator.SetLayerWeight(upperLayer,Mathf.MoveTowards(animator.GetLayerWeight(upperLayer),0,Time.deltaTime/.14f));
            if(step<0)return;
            if(Interrupted||inventory.EquippedWeapon!=item){Cancel();return;}
            if(!LocksFacing)
            {
                weapons.RefreshAimDirection();direction=weapons.AimDirection;
                if(motor)motor.FaceForAttack(direction);
            }
            if(charging)
            {
                weapons.RefreshAimDirection();direction=weapons.AimDirection;if(motor)motor.FaceForAttack(direction);
                // 완충 뒤에는 당기기 클립의 마지막 자세만 유지하며 발사 구간으로 넘어가지 않습니다.
                if(move.bowDrawClip)animator.Play(DaggerLayer+"."+move.bowDrawClip.name,upperLayer,Mathf.Min(.9999f,ChargeAmount));
                else animator.Play(DaggerLayer+"."+move.attacks[0].name,upperLayer,Mathf.Lerp(.02f,.42f,ChargeAmount));
                UpdateChargeVisual();return;
            }
            bool flurry=item.weaponCategory==InventoryWeapon.WeaponCategory.Spear&&step==2;
            if(flurry)
            {
                while(thrustCount<12&&Time.time-started>=move.strikeTimes[step]+thrustCount*spearThrustInterval){thrustCount++;Strike();}
            }
            else if(!struck&&Time.time-started>=move.strikeTimes[step]){struck=true;Strike();}
            if(!secondStruck&&move.secondaryStrikeTimes!=null&&step<move.secondaryStrikeTimes.Length&&move.secondaryStrikeTimes[step]>0&&Time.time-started>=move.secondaryStrikeTimes[step]){secondStruck=true;Strike();}
            if(queued&&!SingleAttack&&Time.time-started>=move.attacks[step].length*move.comboLinkNormalized){Begin(step+1);return;}
            if(Time.time>endsAt+continuationGrace)Cancel();
        }
        void Strike()
        {
            // 각 타격 직전에 최신 조준을 읽고, 판정과 이펙트에 같은 방향을 사용합니다.
            weapons.RefreshAimDirection();direction=weapons.AimDirection;
            if(motor)motor.FaceForAttack(direction);
            facingLockedUntil=Time.time+strikeFacingLock;
            var capsule=GetComponent<CapsuleCollider>();
            Vector3 origin=capsule?capsule.bounds.center:transform.position+Vector3.up;
            Color color=item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?new Color(.65f,.20f,.25f,.55f):new Color(.50f,.82f,1,.55f);
            float reach=item.rangeDefined?item.meleeRange:move.reach;
            if(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow||item.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang)
            {
                bool boomerang=item.weaponCategory==InventoryWeapon.WeaponCategory.Boomerang;
                if(boomerang&&activeBoomerang)return;
                if(!boomerang&&weapons.GetWeaponSocket(item))origin=weapons.GetWeaponSocket(item).position;
                if(boomerang&&inventory.EquippedVisual)origin=inventory.EquippedVisual.GetComponentInChildren<Renderer>().bounds.center;
                origin=weapons.SafeLaunchOrigin(origin);
                var shotDirection=weapons.GetProjectileDirection(origin);
                if(motor)motor.FaceForAttack(shotDirection);
                var projectile=BoneWeaponProjectile.Launch(origin,shotDirection,transform,item,move,color,reach,boomerang?0:shotCharge);
                if(boomerang){activeBoomerang=projectile;inventory.SetHeldVisible(false);projectile.Returned+=()=>{if(inventory)inventory.SetHeldVisible(true);};}
            }
            else
            {
                bool thrust=(item.weaponCategory==InventoryWeapon.WeaponCategory.Spear&&step==2)||item.weaponCategory==InventoryWeapon.WeaponCategory.Dagger;
                if(item.weaponCategory==InventoryWeapon.WeaponCategory.Dagger)
                    origin.y=weapons.ThrustHeight(origin,direction,reach*BoneWeaponVfx.SizeFor(item)*(step==2?1.35f:1));
                float power=item.weaponCategory==InventoryWeapon.WeaponCategory.Axe?(step==2?1.8f:step==1?1.3f:1):1;
                var effect=BoneWeaponVfx.Strike(origin,direction,reach,item.weaponCategory,step,color,move.effectMaterial,BoneWeaponVfx.SizeFor(item),secondStruck);
                weapons.ApplyVisibleMelee(item,origin,effect.HitShape,!thrust,power,effect.ImpactShape);
            }
        }
        public void Cancel()
        {
            bool restoreBowFacing=move&&move.bowDrawClip&&item&&item.weaponCategory==InventoryWeapon.WeaponCategory.Bow;
            facingLockedUntil=0;
            var bow=inventory&&inventory.EquippedVisual?inventory.EquippedVisual.GetComponent<EquippedBowString>():null;
            if(bow)bow.Draw=0;
            charging=false;ClearChargeVisual();
            bool active=step>=0,wasUpper=upperBodyAttack;step=-1;queued=false;item=null;move=null;upperBodyAttack=false;
            if(restoreBowFacing&&motor)motor.FaceForAttack(motor.Facing);
            if(active&&!wasUpper&&animator&&animator.isInitialized&&animator.isActiveAndEnabled)animator.CrossFadeInFixedTime("Base Layer.Locomotion",.12f,0);
        }
        public void CancelForDash()
        {
            Cancel();
            int layer=animator?animator.GetLayerIndex(DaggerLayer):-1;
            if(layer>=0)animator.SetLayerWeight(layer,0);
        }
        void ClearChargeVisual(){if(chargeArrow)Destroy(chargeArrow.gameObject);chargeArrow=null;}
        void UpdateChargeVisual()
        {
            if(!chargeArrow){var go=new GameObject("Charged bone arrow");chargeArrow=go.AddComponent<LineRenderer>();chargeArrow.sharedMaterial=move.effectMaterial;chargeArrow.positionCount=5;}
            var pos=weapons.SafeLaunchOrigin(weapons.GetWeaponSocket(item).position);
            var shotDirection=weapons.GetProjectileDirection(pos);var right=Vector3.Cross(Vector3.up,shotDirection).normalized;
            float size=BoneWeaponVfx.SizeFor(item),length=Mathf.Lerp(.5f,1.1f,ChargeAmount)*size;
            var tip=pos+shotDirection*length;
            chargeArrow.SetPositions(new[]{pos-shotDirection*.3f,tip,tip-shotDirection*.25f+right*.16f,tip,tip-shotDirection*.25f-right*.16f});
            chargeArrow.startWidth=Mathf.Lerp(.06f,.17f,ChargeAmount)*size;chargeArrow.endWidth=.025f*size;
            var color=item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?new Color(.8f,.16f,.24f,.85f):new Color(.4f,.85f,1,.85f);chargeArrow.startColor=chargeArrow.endColor=color;
            var bow=inventory.EquippedVisual?inventory.EquippedVisual.GetComponent<EquippedBowString>():null;if(bow)bow.Draw=ChargeAmount;
        }
        void OnDisable(){Cancel();if(activeBoomerang)Destroy(activeBoomerang.gameObject);}
    }
}
