using UnityEngine;
using Unity.Cinemachine;

namespace DogLife
{
    // Only the transformation clock slows down: pause menus and physics keep their existing time ownership.
    [DefaultExecutionOrder(1100)]
    public sealed class SpringBoarCinematic : MonoBehaviour
    {
        [Header("변신 속도 / 1 = 원래 속도")]
        [SerializeField] AnimationCurve playbackSpeed=new AnimationCurve(new Keyframe(0,.72f),new Keyframe(.34f,.55f),new Keyframe(.52f,.3f),new Keyframe(.68f,.42f),new Keyframe(.84f,.7f),new Keyframe(1,.9f));
        [Header("보스 기준 촬영 위치 (월드 미터)")]
        [SerializeField] Vector3 opening=new Vector3(15,8,22),reveal=new Vector3(12,6,18),finale=new Vector3(10,5.8f,21);
        [SerializeField] Vector3 openingLook=new Vector3(0,3.8f,0),revealLook=new Vector3(0,4.5f,1),finalLook=new Vector3(0,4,0);
        [SerializeField,Range(25,70)] float openingFov=42,revealFov=38,finalFov=44;
        [SerializeField,Min(.1f)] float enterBlend=1.05f,returnBlend=1.1f;
        [SerializeField,Range(0,.1f)] float letterbox=.065f;
        [SerializeField] LayerMask sceneryMask=1<<8;
        [Header("사망 연출")]
        [SerializeField] Vector3 deathOpening=new Vector3(13,7,18),deathFinal=new Vector3(11,4.8f,16);
        [SerializeField] Vector3 deathLookStart=new Vector3(0,3.4f,1),deathLookEnd=new Vector3(0,1.8f,1);
        [SerializeField,Range(25,70)] float deathFov=43;
        bool deathShot;
        Camera view;PlayerCamera follow;CinemachineBrain brain;CinemachineCamera shot;
        PlayerMotor motor;PlayerWeapons weapons;CombatHealth playerHealth;
        bool motorEnabled,weaponsEnabled,followEnabled,invulnerable,brainEnabled,createdBrain,ignoreTimeScale;
        CinemachineBrain.UpdateMethods updateMethod;
        CinemachineBlendDefinition defaultBlend;
        CursorLockMode cursor;bool cursorVisible;
        Vector3 fromPosition;Quaternion fromRotation;float oldFov,elapsed,progress,side;
        Canvas overlay;RectTransform top,bottom;
        readonly RaycastHit[] hits=new RaycastHit[64];
        public bool Active {get;private set;}
        public float Speed(float p)=>Mathf.Clamp(playbackSpeed.Evaluate(p),.15f,1.5f);
        public void BeginDeath()=>Begin(true);
        public void Begin(bool dying=false)
        {
            if(Active)return;view=Camera.main;if(!view)return;
            deathShot=dying;
            Active=true;progress=elapsed=0;
            fromPosition=view.transform.position;fromRotation=view.transform.rotation;oldFov=view.fieldOfView;
            cursor=Cursor.lockState;cursorVisible=Cursor.visible;
            follow=view.GetComponent<PlayerCamera>();followEnabled=follow&&follow.enabled;
            motor=follow&&follow.target?follow.target.GetComponentInParent<PlayerMotor>():FindFirstObjectByType<PlayerMotor>();
            weapons=motor?motor.GetComponent<PlayerWeapons>():null;playerHealth=motor?motor.GetComponent<CombatHealth>():null;
            motorEnabled=motor&&motor.enabled;weaponsEnabled=weapons&&weapons.enabled;invulnerable=playerHealth&&playerHealth.invulnerable;
            if(motor){var combat=motor.GetComponent<BasicWeaponCombat>();if(combat)combat.Cancel();motor.StopForAttack();motor.enabled=false;}
            if(weapons)weapons.enabled=false;if(playerHealth)playerHealth.invulnerable=true;
            if(follow)follow.enabled=false;
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            brain=view.GetComponent<CinemachineBrain>();createdBrain=!brain;
            if(!brain)brain=view.gameObject.AddComponent<CinemachineBrain>();
            brainEnabled=brain.enabled;updateMethod=brain.UpdateMethod;ignoreTimeScale=brain.IgnoreTimeScale;defaultBlend=brain.DefaultBlend;
            brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;brain.IgnoreTimeScale=true;
            brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);brain.enabled=true;
            shot=new GameObject("Spring Boss / Metamorphosis Cinemachine").AddComponent<CinemachineCamera>();
            shot.Priority=1000;shot.Lens.FieldOfView=oldFov;shot.Lens.NearClipPlane=view.nearClipPlane;shot.Lens.FarClipPlane=view.farClipPlane;
            shot.transform.SetPositionAndRotation(fromPosition,fromRotation);
            side=Vector3.Dot(fromPosition-transform.position,transform.right)<0?-1:1;
            BuildBars();
        }
        void BuildBars()
        {
            var go=new GameObject("Metamorphosis Letterbox",typeof(Canvas));overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=80;
            for(int i=0;i<2;i++)
            {
                var bar=new GameObject(i==0?"Top":"Bottom",typeof(UnityEngine.UI.Image));bar.transform.SetParent(go.transform,false);
                var image=bar.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.018f,.012f,.02f,1);image.raycastTarget=false;
                var rect=(RectTransform)bar.transform;if(i==0)top=rect;else bottom=rect;
            }
        }
        public void SetProgress(float value){progress=value;}
        void LateUpdate()
        {
            if(!Active)return;
            if(!view||!shot||!brain){End();return;}
            if(Time.timeScale==0)return;
            elapsed+=Time.unscaledDeltaTime;
            float a=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.58f,progress));
            float b=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.94f,progress));
            Vector3 offset=Vector3.Lerp(Vector3.Lerp(opening,reveal,a),finale,b);offset.x*=side;
            var yaw=Quaternion.Euler(0,transform.eulerAngles.y,0);
            Vector3 look=transform.position+yaw*Vector3.Lerp(Vector3.Lerp(openingLook,revealLook,a),finalLook,b);
            Vector3 position=transform.position+yaw*offset;
            if(deathShot)
            {
                float sink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.87f,progress));
                var deathOffset=Vector3.Lerp(deathOpening,deathFinal,sink);deathOffset.x*=side;
                position=transform.position+yaw*deathOffset;
                look=transform.position+yaw*Vector3.Lerp(deathLookStart,deathLookEnd,sink);
            }
            position=AvoidScenery(look,position);
            float entrance=Mathf.SmoothStep(0,1,elapsed/enterBlend);
            var rotation=Quaternion.LookRotation(look-position);
            // Brief restrained recoil at the roar and landing, tied to the same slowed animation clock.
            float pulse=Mathf.Max(0,1-Mathf.Abs(progress-.535f)/.025f)*.24f+Mathf.Max(0,1-Mathf.Abs(progress-.8f)/.018f)*.35f;
            if(deathShot)pulse=Mathf.Max(0,1-Mathf.Abs(progress-.58f)/.035f)*.18f+Mathf.Max(0,1-Mathf.Abs(progress-.77f)/.03f)*.12f;
            rotation*=Quaternion.Euler(Mathf.Sin(elapsed*43)*pulse,0,Mathf.Sin(elapsed*31)*pulse*.5f);
            shot.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition,position,entrance),Quaternion.Slerp(fromRotation,rotation,entrance));
            shot.Lens.FieldOfView=Mathf.Lerp(oldFov,Mathf.Lerp(Mathf.Lerp(openingFov,revealFov,a),finalFov,b),entrance);
            if(deathShot)shot.Lens.FieldOfView=Mathf.Lerp(oldFov,deathFov,entrance);
            brain.ManualUpdate();
            float height=letterbox*entrance*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1,progress)));
            top.anchorMin=new Vector2(0,1-height);top.anchorMax=Vector2.one;top.offsetMin=top.offsetMax=Vector2.zero;
            bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,height);bottom.offsetMin=bottom.offsetMax=Vector2.zero;
        }
        Vector3 AvoidScenery(Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;float distance=delta.magnitude;
            int count=Physics.SphereCastNonAlloc(from,.35f,delta.normalized,hits,distance,sceneryMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!hits[i].collider.transform.IsChildOf(transform)&&!hits[i].collider.GetComponentInParent<CombatHealth>())distance=Mathf.Min(distance,Mathf.Max(1,hits[i].distance-.4f));
            return from+delta.normalized*distance;
        }
        public void End()
        {
            if(!Active)return;Active=false;
            if(shot){shot.gameObject.SetActive(false);Destroy(shot.gameObject);}
            if(brain){brain.enabled=false;if(createdBrain)Destroy(brain);else{brain.UpdateMethod=updateMethod;brain.IgnoreTimeScale=ignoreTimeScale;brain.DefaultBlend=defaultBlend;brain.enabled=brainEnabled;}}
            if(motor)motor.enabled=motorEnabled;if(weapons)weapons.enabled=weaponsEnabled;if(playerHealth)playerHealth.invulnerable=invulnerable;
            if(follow&&followEnabled){follow.BlendFromCinematic(returnBlend,oldFov);follow.enabled=true;}
            else if(view){view.transform.SetPositionAndRotation(fromPosition,fromRotation);view.fieldOfView=oldFov;}
            Cursor.lockState=cursor;Cursor.visible=cursorVisible;
            if(overlay)Destroy(overlay.gameObject);
        }
        void OnDisable(){End();}
    }
}
