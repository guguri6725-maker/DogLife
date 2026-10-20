using UnityEngine;
using System.Collections.Generic;

namespace DogLife
{
    // 방에 배치한 왕좌와 촬영 기준을 사용하며 기존 플레이어 카메라를 잠시 빌립니다.
    [DefaultExecutionOrder(1000)]
    public sealed class FrozenKnightCinematicStage : MonoBehaviour
    {
        public Transform seatedOrigin, standingOrigin,horseWaitingOrigin;
        [Min(0)] public float seatedHold=1.1f, shotBlend=.8f, returnBlend=.65f;
        public Vector3 throneCamera=new Vector3(5,3.5f,9), throneLook=new Vector3(0,2.1f,0);
        public Vector3 mountCamera=new Vector3(-7,4,9), mountLook=new Vector3(0,2.5f,0);
        public Vector3 deathCamera=new Vector3(-7,4.2f,10),deathLook=new Vector3(0,3,0);
        // 긴 대검이 머리 위를 지날 때 칼끝까지 담는 발도 전용 구도입니다.
        public Vector3 drawCamera=new Vector3(7,6.4f,17),drawLook=new Vector3(0,4.8f,0);
        bool deathShot,drawShot;
        public Vector3 iceCamera=new Vector3(-8,9,12),iceLook=new Vector3(0,0,0);
        bool iceShot;Vector3 iceOrigin;Quaternion iceRotation;
        [Range(20,80)] public float fieldOfView=43;
        Camera view;PlayerCamera follow;PlayerMotor motor;PlayerWeapons weapons;CombatHealth hp;
        bool motorEnabled,weaponsEnabled,followEnabled,oldInvulnerable,oldRecoveryBlocked,locked;
        float oldFov,blend;Vector3 fromPosition;Quaternion fromRotation;Transform subject;bool mountedShot;
        readonly Dictionary<Renderer,bool> hiddenIceWalls=new Dictionary<Renderer,bool>();
        public bool Active=>locked;
        public bool Valid=>seatedOrigin&&standingOrigin;
        public void Begin(PlayerMotor player,Transform actor,bool mounting)
        {
            if(locked)return;
            motor=player;weapons=player?player.GetComponent<PlayerWeapons>():null;hp=player?player.GetComponent<CombatHealth>():null;
            view=Camera.main;follow=view?view.GetComponent<PlayerCamera>():null;
            motorEnabled=motor&&motor.enabled;weaponsEnabled=weapons&&weapons.enabled;followEnabled=follow&&follow.enabled;oldInvulnerable=hp&&hp.invulnerable;
            // 연출 중 회복을 잠그고 종료·중단 시 기존 상태로 돌립니다. 보스 HP에는 영향을 주지 않습니다.
            oldRecoveryBlocked=hp&&hp.RecoveryBlocked;
            if(hp)hp.RecoveryBlocked=true;
            if(motor){motor.StopForAttack();motor.enabled=false;}if(weapons)weapons.enabled=false;if(hp)hp.invulnerable=true;
            if(view){oldFov=view.fieldOfView;fromPosition=view.transform.position;fromRotation=view.transform.rotation;}
            if(follow)follow.enabled=false;
            subject=actor;mountedShot=mounting;blend=0;locked=true;
            // 닫힌 얼음벽이 발도/탑승 카메라를 가리지 않게 외형만 숨깁니다. 충돌은 유지합니다.
            foreach(var wall in FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None))
                if(wall.IsPillar&&!wall.Broken)
                    foreach(var renderer in wall.GetComponentsInChildren<Renderer>())
                    {hiddenIceWalls[renderer]=renderer.forceRenderingOff;renderer.forceRenderingOff=true;}
            BossCinematicUI.Begin(this);
            deathShot=false;iceShot=false;drawShot=false;
        }
        public void BeginDeath(PlayerMotor player,Transform actor){Begin(player,actor,true);deathShot=true;}
        // 검의 기울기 대신 보스의 정면을 기준으로 바닥을 내려다봅니다.
        public void ShowIceField(Transform sword,Transform actor)
        {
            iceOrigin=new Vector3(sword.position.x,actor.position.y,sword.position.z);
            iceRotation=Quaternion.Euler(0,actor.eulerAngles.y,0);iceShot=true;ResetShot();
        }
        public void ShowDraw(){drawShot=true;ResetShot();}
        public void ShowMount(Transform actor){subject=actor;iceShot=false;mountedShot=true;ResetShot();}
        void ResetShot(){if(view){fromPosition=view.transform.position;fromRotation=view.transform.rotation;}blend=0;}
        void LateUpdate()
        {
            if(!locked||!view||!subject)return;
            blend+=Time.unscaledDeltaTime/Mathf.Max(.01f,shotBlend);float t=Mathf.SmoothStep(0,1,blend);
            // 피사체 기준 구도로 왕좌/탑승 지점이 달라도 같은 화면 구성을 유지합니다.
            var rotation=Quaternion.Euler(0,subject.eulerAngles.y,0);
            Vector3 look=subject.position+rotation*(deathShot?deathLook:mountedShot?mountLook:drawShot?drawLook:throneLook);
            Vector3 position=subject.position+rotation*(deathShot?deathCamera:mountedShot?mountCamera:drawShot?drawCamera:throneCamera);
            if(iceShot){look=iceOrigin+iceRotation*iceLook;position=iceOrigin+iceRotation*iceCamera;}
            view.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition,position,t),Quaternion.Slerp(fromRotation,Quaternion.LookRotation(look-position),t));
            view.fieldOfView=Mathf.Lerp(oldFov,fieldOfView,t);
        }
        public void End()
        {
            if(!locked)return;locked=false;
            foreach(var pair in hiddenIceWalls)if(pair.Key)pair.Key.forceRenderingOff=pair.Value;
            hiddenIceWalls.Clear();
            if(motor)motor.enabled=motorEnabled;if(weapons)weapons.enabled=weaponsEnabled;
            if(hp){hp.invulnerable=oldInvulnerable;hp.RecoveryBlocked=oldRecoveryBlocked;}
            if(follow&&followEnabled){follow.BlendFromCinematic(returnBlend,oldFov);follow.enabled=true;follow.CaptureCursor(true);}
            else if(view)view.fieldOfView=oldFov;
            BossCinematicUI.End(this,follow&&followEnabled?returnBlend:0);
        }
        void OnDisable(){End();BossCinematicUI.End(this);}
    }
}
