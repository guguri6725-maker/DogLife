using UnityEngine;
namespace DogLife
{
    // PlayerMotor remains the only velocity writer. This component supplies water resistance.
    public sealed class PlayerWaterMovement : MonoBehaviour
    {
        [SerializeField] float horizontalMultiplier=.82f,acceleration=9,deceleration=6;
        [SerializeField] float ascentSpeed=3,verticalAcceleration=4,verticalDeceleration=5,sinkSpeed=.45f,sinkDelay=.65f,lookDownSpeed=2.8f;
        [SerializeField] float surfaceClearance=1.1f,floorClearance=.15f,cinematicDepth=3.4f;
        public bool LiftWithSurface{get;set;}
        public bool Submerged{get;private set;}
        public bool AscendHeld{get;set;}
        public float Surface{get;private set;}
        Vector3 externalCurrent;
        bool arenaLimits=true;
        public void SetCurrent(Vector3 current){externalCurrent=current;}
        public void ReleaseArenaLimits(){arenaLimits=false;}
        public Vector3 ConstrainCamera(Vector3 position)
        {
            // Near the surface the camera may emerge normally; deep swimming keeps a readable underwater view.
            if(Submerged&&transform.position.y<Surface-1.6f)position.y=Mathf.Min(position.y,Surface-.25f);
            return position;
        }
        Rigidbody body;PlayerMotor motor;CombatHealth health;bool oldGravity;float releasedAt;Bounds bounds;
        void Awake(){body=GetComponent<Rigidbody>();motor=GetComponent<PlayerMotor>();health=GetComponent<CombatHealth>();if(health)health.Died+=Leave;}
        public void SetWater(float surface,Bounds volume)
        {
            Surface=surface;bounds=volume;arenaLimits=true;
            if(!Submerged && surface>transform.position.y+.8f){oldGravity=body.useGravity;body.useGravity=false;Submerged=true;releasedAt=Time.time;motor.ReleaseJump();}
        }
        public Vector3 ResolveVelocity(Vector3 velocity)
        {
            if(!Submerged)return velocity;
            if(!arenaLimits&&(transform.position.x<bounds.min.x-2||transform.position.x>bounds.max.x+2||transform.position.z<bounds.min.z-2||transform.position.z>bounds.max.z+2)){Leave();return velocity;}
            float dt=Time.fixedDeltaTime;
            if(!motor.IsDashing&&!motor.IsKnockedBack)
            {
                var desired=new Vector3(velocity.x,0,velocity.z)*horizontalMultiplier+Vector3.ProjectOnPlane(externalCurrent,Vector3.up);
                var horizontal=Vector3.MoveTowards(Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up),desired,(desired.sqrMagnitude>.01f?acceleration:deceleration)*dt);
                velocity.x=horizontal.x;velocity.z=horizontal.z;
            }
            if(!motor.IsKnockedBack)
            {
                bool ascend=AscendHeld&&motor.readInput&&!SkillWindow.BlocksGameplayInput&&!motor.AttackMovementLocked;
                if(ascend)releasedAt=Time.time;
                float target=ascend?ascentSpeed:Time.time-releasedAt>sinkDelay?-sinkSpeed:0;
                if(LiftWithSurface){float depth=Mathf.Lerp(.8f,cinematicDepth,Mathf.InverseLerp(bounds.min.y+1,bounds.min.y+6,Surface));target=Mathf.Clamp((Surface-depth-body.position.y)*2,0,ascentSpeed);releasedAt=Time.time;}
                if(!ascend&&motor.movementCamera&&motor.MoveInput.y>0)target-=Mathf.Max(0,-motor.movementCamera.forward.y)*motor.MoveInput.y*lookDownSpeed;
                target+=externalCurrent.y;
                velocity.y=Mathf.MoveTowards(body.linearVelocity.y,target,(ascend?verticalAcceleration:verticalDeceleration)*dt);
            }
            Vector3 p=body.position;
            if(arenaLimits){velocity.x=Mathf.Clamp(velocity.x,(bounds.min.x-p.x)/dt,(bounds.max.x-p.x)/dt);velocity.z=Mathf.Clamp(velocity.z,(bounds.min.z-p.z)/dt,(bounds.max.z-p.z)/dt);}
            velocity.y=Mathf.Clamp(velocity.y,(bounds.min.y+floorClearance-p.y)/dt,Mathf.Max(0,Surface-surfaceClearance-p.y)/dt);
            // Predict all axes with the existing rigidbody shape, including ceilings and the floor.
            if(velocity.sqrMagnitude>.001f&&body.SweepTest(velocity.normalized,out var hit,velocity.magnitude*dt+.03f,QueryTriggerInteraction.Ignore))
            {
                var kraken=hit.collider.GetComponentInParent<KrakenPhaseTwo>();
                if(!hit.collider.GetComponentInParent<CombatHealth>()||(kraken&&kraken.BodyBarrier==hit.collider))
                    velocity=Vector3.ProjectOnPlane(velocity,hit.normal);
            }
            return velocity;
        }
        public void Leave(){if(Submerged&&body)body.useGravity=oldGravity;Submerged=false;AscendHeld=false;LiftWithSurface=false;externalCurrent=Vector3.zero;}
        void OnDisable(){Leave();}
        void OnDestroy(){if(health)health.Died-=Leave;}
    }
}
