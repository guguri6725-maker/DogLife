using UnityEngine;
using Unity.Cinemachine;
namespace DogLife
{
    [DefaultExecutionOrder(1100)]
    public sealed class KrakenSequenceCamera:MonoBehaviour
    {
        [SerializeField] Vector3 entryOffset=new Vector3(-10,9,-15),floodOffset=new Vector3(-5,4,-8);
        [SerializeField] float fieldOfView=62,blendTime=1.4f,returnBlend=1;
        [Header("Flood tracking shot")]
        [SerializeField] Vector3 floodWideOffset=new Vector3(-8,7,-4),floodCloseOffset=new Vector3(-4,1,-5);
        [SerializeField] float floodFieldOfView=54,positionDamping=.3f;
        Transform floodPlayer;float floodProgress,waterHeight;Vector3 smoothedTarget,targetVelocity;
        [Header("Underwater seabed reveal")]
        [SerializeField] Vector3 revealWideOffset=new Vector3(-12,2,-9),revealCloseOffset=new Vector3(-9,1,-7);
        [SerializeField] float revealFieldOfView=60;
        bool revealing;float revealProgress;Vector3 revealLook;
        public void BeginReveal(Vector3 center,float surface)
        {
            // Continue the same Brain across shots; destroying/recreating it in one frame loses the camera.
            if(!active)Begin(center,false);
            else{focus=center;flooding=false;clock=0;from=view.transform.position;fromRotation=view.transform.rotation;smoothedTarget=from;targetVelocity=Vector3.zero;}
            revealing=true;waterHeight=surface;revealLook=center;revealProgress=0;
        }
        public void SetRevealProgress(float progress,Vector3 body){revealProgress=Mathf.Clamp01(progress);revealLook=Vector3.Lerp(focus-Vector3.up*2,body,Mathf.SmoothStep(0,1,progress));}
        public void SetFloodProgress(float progress,float height,Transform player){floodProgress=Mathf.Clamp01(progress);waterHeight=height;floodPlayer=player;}
        Camera view;PlayerCamera follow;CinemachineBrain brain;CinemachineCamera shot;
        bool active,followWasEnabled,createdBrain,brainWasEnabled,ignoreTime;
        CinemachineBrain.UpdateMethods updateMethod;CinemachineBlendDefinition blend;
        Vector3 from,focus;Quaternion fromRotation;float oldFov,clock;bool flooding;
        public void Begin(Vector3 center,bool flood)
        {
            if(active)End();view=Camera.main;if(!view)return;active=true;focus=center;flooding=flood;revealing=false;clock=0;
            BossCinematicUI.Begin(this);
            from=view.transform.position;fromRotation=view.transform.rotation;oldFov=view.fieldOfView;smoothedTarget=from;targetVelocity=Vector3.zero;floodProgress=0;
            follow=view.GetComponent<PlayerCamera>();followWasEnabled=follow&&follow.enabled;if(follow)follow.enabled=false;
            brain=view.GetComponent<CinemachineBrain>();createdBrain=!brain;if(!brain)brain=view.gameObject.AddComponent<CinemachineBrain>();
            brainWasEnabled=brain.enabled;updateMethod=brain.UpdateMethod;blend=brain.DefaultBlend;ignoreTime=brain.IgnoreTimeScale;
            brain.enabled=true;brain.UpdateMethod=CinemachineBrain.UpdateMethods.ManualUpdate;brain.IgnoreTimeScale=false;brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);
            shot=new GameObject("Kraken Cinemachine sequence").AddComponent<CinemachineCamera>();shot.Priority=1100;shot.Lens.FieldOfView=oldFov;shot.Lens.NearClipPlane=view.nearClipPlane;shot.Lens.FarClipPlane=view.farClipPlane;shot.transform.SetPositionAndRotation(from,fromRotation);
        }
        void LateUpdate()
        {
            if(!active||!view||!shot||!brain||Time.timeScale==0)return;clock+=Time.deltaTime;
            Vector3 target=focus+(flooding?floodOffset:entryOffset),look=focus+Vector3.up*(flooding?2:3);
            if(flooding&&floodPlayer)
            {
                float approach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.85f,floodProgress));
                Vector3 playerFocus=floodPlayer.position+Vector3.up*.9f;
                look=Vector3.Lerp(focus+Vector3.up*Mathf.Max(2,waterHeight-focus.y+.4f),playerFocus,approach);
                Vector3 wide=focus+Quaternion.Euler(0,Mathf.Lerp(-8,10,floodProgress),0)*floodWideOffset;
                wide.y=Mathf.Max(wide.y,waterHeight+2);
                target=Vector3.Lerp(wide,playerFocus+floodCloseOffset,approach);
            }
            if(revealing){look=revealLook;target=focus+Vector3.Lerp(revealWideOffset,revealCloseOffset,Mathf.SmoothStep(0,1,revealProgress));target.y=Mathf.Min(target.y,waterHeight-1);look.y=Mathf.Max(focus.y-2,look.y);}
            Vector3 delta=target-look;
            if(Physics.SphereCast(look,.35f,delta.normalized,out var h,delta.magnitude,1<<8,QueryTriggerInteraction.Ignore))target=look+delta.normalized*Mathf.Max(1,h.distance-.4f);
            smoothedTarget=Vector3.SmoothDamp(smoothedTarget,target,ref targetVelocity,positionDamping,100,Time.deltaTime);
            float q=Mathf.SmoothStep(0,1,clock/Mathf.Max(.01f,blendTime));Vector3 position=Vector3.Lerp(from,smoothedTarget,q);
            shot.transform.SetPositionAndRotation(position,Quaternion.Slerp(fromRotation,Quaternion.LookRotation(look-position),q));shot.Lens.FieldOfView=Mathf.Lerp(oldFov,revealing?revealFieldOfView:flooding?Mathf.Lerp(fieldOfView,floodFieldOfView,floodProgress):fieldOfView,q);brain.ManualUpdate();
        }
        public void End()
        {
            if(!active)return;active=false;
            if(shot){shot.gameObject.SetActive(false);Destroy(shot.gameObject);}
            if(brain){brain.enabled=false;if(createdBrain)Destroy(brain);else{brain.UpdateMethod=updateMethod;brain.DefaultBlend=blend;brain.IgnoreTimeScale=ignoreTime;brain.enabled=brainWasEnabled;}}
            if(follow&&followWasEnabled){follow.BlendFromCinematic(returnBlend,oldFov);follow.enabled=true;}else if(view){view.transform.SetPositionAndRotation(from,fromRotation);view.fieldOfView=oldFov;}
            BossCinematicUI.End(this,follow&&followWasEnabled?returnBlend:0);
        }
        void OnDisable(){End();BossCinematicUI.End(this);}
    }
}
