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
        [SerializeField] Shader foliageFadeShader;
        SpringCinematicFoliage foliage;
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
        float shotYaw;
        Quaternion subjectYaw;
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
            BossCinematicUI.KeepVisible(overlay);
            BossCinematicUI.Begin(this);
            foliage?.Dispose();
            foliage=new SpringCinematicFoliage(transform,foliageFadeShader);
            subjectYaw=Quaternion.Euler(0,transform.eulerAngles.y,0);
            shotYaw=ChooseShotYaw();
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
            // 복귀 중인 실제 카메라에서도 식물 가림을 유지한 뒤 원래 재질로 돌립니다.
            if(!Active){UpdateFoliage();return;}
            if(!view||!shot||!brain){End();return;}
            if(Time.timeScale==0)return;
            elapsed+=Time.unscaledDeltaTime;
            float a=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.58f,progress));
            float b=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.94f,progress));
            Vector3 offset=Vector3.Lerp(Vector3.Lerp(opening,reveal,a),finale,b);offset.x*=side;
            var yaw=subjectYaw;
            var cameraYaw=yaw*Quaternion.Euler(0,shotYaw,0);
            Vector3 look=transform.position+yaw*Vector3.Lerp(Vector3.Lerp(openingLook,revealLook,a),finalLook,b);
            Vector3 position=transform.position+cameraYaw*offset;
            if(deathShot)
            {
                float sink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.28f,.87f,progress));
                var deathOffset=Vector3.Lerp(deathOpening,deathFinal,sink);deathOffset.x*=side;
                position=transform.position+cameraYaw*deathOffset;
                look=transform.position+yaw*Vector3.Lerp(deathLookStart,deathLookEnd,sink);
            }
            position=AvoidScenery(look,position);
            float entrance=Mathf.SmoothStep(0,1,elapsed/enterBlend);
            var rotation=Quaternion.LookRotation(look-position);
            // Brief restrained recoil at the roar and landing, tied to the same slowed animation clock.
            float pulse=Mathf.Max(0,1-Mathf.Abs(progress-.535f)/.025f)*.24f+Mathf.Max(0,1-Mathf.Abs(progress-.8f)/.018f)*.35f;
            if(deathShot)pulse=Mathf.Max(0,1-Mathf.Abs(progress-.58f)/.035f)*.18f+Mathf.Max(0,1-Mathf.Abs(progress-.77f)/.03f)*.12f;
            rotation*=Quaternion.Euler(Mathf.Sin(elapsed*43)*pulse,0,Mathf.Sin(elapsed*31)*pulse*.5f);
            // 진입 보간 경로도 벽을 통과하지 않도록 매 프레임 확인합니다.
            var blended=AvoidScenery(look,Vector3.Lerp(fromPosition,position,entrance));
            var blendedRotation=Quaternion.Slerp(fromRotation,rotation,entrance);
            if((blended-Vector3.Lerp(fromPosition,position,entrance)).sqrMagnitude>.01f)
                blendedRotation=Quaternion.LookRotation(look-blended);
            shot.transform.SetPositionAndRotation(blended,blendedRotation);
            shot.Lens.FieldOfView=Mathf.Lerp(oldFov,Mathf.Lerp(Mathf.Lerp(openingFov,revealFov,a),finalFov,b),entrance);
            if(deathShot)shot.Lens.FieldOfView=Mathf.Lerp(oldFov,deathFov,entrance);
            brain.ManualUpdate();
            UpdateFoliage();
            float height=letterbox*entrance*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1,progress)));
            top.anchorMin=new Vector2(0,1-height);top.anchorMax=Vector2.one;top.offsetMin=top.offsetMax=Vector2.zero;
            bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,height);bottom.offsetMin=bottom.offsetMax=Vector2.zero;
        }
        // 벽 옆에서도 연출 전체가 보이는 방향을 한 번 선택해 촬영 중 좌우 전환을 막습니다.
        float ChooseShotYaw()
        {
            float bestAngle=0,bestScore=float.NegativeInfinity;
            foreach(float angle in new[]{0f,45f,-45f,90f,-90f,135f,-135f,180f})
            {
                var cameraYaw=subjectYaw*Quaternion.Euler(0,angle,0);
                float score=1;
                for(int i=0;i<5;i++)
                {
                    float t=i/4f;
                    Vector3 offset=deathShot?Vector3.Lerp(deathOpening,deathFinal,t):
                        (t<.5f?Vector3.Lerp(opening,reveal,t*2):Vector3.Lerp(reveal,finale,(t-.5f)*2));
                    Vector3 focus=deathShot?Vector3.Lerp(deathLookStart,deathLookEnd,t):
                        (t<.5f?Vector3.Lerp(openingLook,revealLook,t*2):Vector3.Lerp(revealLook,finalLook,(t-.5f)*2));
                    offset.x*=side;
                    var look=transform.position+subjectYaw*focus;
                    var desired=transform.position+cameraYaw*offset;
                    float clearance=Vector3.Distance(look,AvoidScenery(look,desired));
                    score=Mathf.Min(score,clearance/Mathf.Max(.1f,Vector3.Distance(look,desired)));
                }
                score-=Mathf.Abs(angle)*.0005f;
                if(score>bestScore){bestScore=score;bestAngle=angle;}
            }
            return bestAngle;
        }
        Vector3 AvoidScenery(Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;float distance=delta.magnitude;
            int count=Physics.SphereCastNonAlloc(from,.35f,delta.normalized,hits,distance,sceneryMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!hits[i].collider.transform.IsChildOf(transform)&&!hits[i].collider.GetComponentInParent<CombatHealth>()&&!(foliage?.Covers(hits[i])??false))distance=Mathf.Min(distance,Mathf.Max(.05f,hits[i].distance-.4f));
            return from+delta.normalized*distance;
        }
        void UpdateFoliage()
        {
            if(foliage==null)return;
            if(!foliage.Tick(view)){foliage.Dispose();foliage=null;}
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
            BossCinematicUI.End(this,follow&&followEnabled?returnBlend:0);
            foliage?.Release(follow&&followEnabled?returnBlend:0);
        }
        void OnDisable(){End();BossCinematicUI.End(this);foliage?.Dispose();foliage=null;}
    }
}
