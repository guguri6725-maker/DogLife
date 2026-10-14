using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DogLife.EditorTools
{
    // 플레이 모드 전용 검증: 실제 트리거를 통과하며 방/통로 개수와 입장 처리를 확인합니다.
    public static class PassageStreamingCheck
    {
        public static string Result="Not run";
        static int RoomCount(ForestRoomStreaming s)=>s.roomScenePaths.Count(p=>SceneManager.GetSceneByPath(p).isLoaded);
        static void Place(PlayerMotor motor,Vector3 p)
        {
            motor.transform.position=p;
            var body=motor.GetComponent<Rigidbody>();body.position=p;body.linearVelocity=Vector3.zero;
            if(Camera.main)Camera.main.transform.position=p+new Vector3(-.15f,2,0);
            Physics.SyncTransforms();
        }
        public static IEnumerator Run(ForestRoomStreaming stream)
        {
            Result="Running";
            float end=Time.realtimeSinceStartup+30;
            while(!stream.Ready&&Time.realtimeSinceStartup<end)yield return null;
            if(!stream.Ready){Result="FAIL initial loading: "+stream.LoadError;yield break;}
            var hud=stream.director.hud;var motor=hud.motor;var camera=Camera.main.GetComponent<PlayerCamera>();
            bool motorEnabled=motor.enabled,cameraEnabled=camera.enabled;
            motor.enabled=false;camera.enabled=false;hud.health.Restore();hud.health.invulnerable=true;Time.timeScale=1;
            var checks=new System.Collections.Generic.List<string>();
            try
            {
                for(int run=0;run<2;run++)
                {
                    int index=stream.CurrentRoom;
                    if(RoomCount(stream)!=1){Result="FAIL initial room count "+RoomCount(stream);yield break;}
                    // 방어/회피 패턴 검증이 아니므로 사망 이벤트로 클리어 조건만 준비합니다.
                    foreach(var enemy in stream.Current.Enemies)if(enemy&&!enemy.Dead)enemy.FatalDamage();
                    yield return new WaitForSeconds(1.5f);
                    var passage=Object.FindObjectsByType<SeasonPassage>(FindObjectsSortMode.None).Single(p=>p.previousRoomIndex==index);
                    Vector3 origin=passage.transform.position;
                    Place(motor,origin+new Vector3(7.1f,.65f,-3.1f));
                    end=Time.realtimeSinceStartup+15;
                    while((stream.Current||stream.Busy)&&Time.realtimeSinceStartup<end)yield return null;
                    if(stream.Current||RoomCount(stream)!=0||!passage.rearSeal.activeSelf){Result="FAIL release "+index+" rooms="+RoomCount(stream);yield break;}
                    checks.Add(index+": 1→0; rear sealed");
                    Place(motor,origin+new Vector3(10.5f,.65f,-3.1f));
                    end=Time.realtimeSinceStartup+25;
                    while(!stream.IsRoomPrepared(index+1)&&Time.realtimeSinceStartup<end)yield return null;
                    if(!stream.IsRoomPrepared(index+1)||RoomCount(stream)!=1){Result="FAIL load "+index+" "+stream.LoadError;yield break;}
                    yield return new WaitForSeconds(1);
                    if(passage.forwardSeal.activeSelf){Result="FAIL front door";yield break;}
                    Place(motor,origin+new Vector3(23,1,0));
                    end=Time.realtimeSinceStartup+10;
                    while((stream.CurrentRoom==index||stream.Busy)&&Time.realtimeSinceStartup<end)yield return null;
                    int passages=Object.FindObjectsByType<SeasonPassage>(FindObjectsSortMode.None).Length;
                    if(stream.CurrentRoom!=index+1||!stream.Current||!stream.Current.CombatStarted||passages>2){Result="FAIL advance "+index;yield break;}
                    checks.Add(index+": 0→1; encounter started; passages="+passages);
                }
                Result="PASS "+string.Join(" / ",checks);
            }
            finally{motor.enabled=motorEnabled;camera.enabled=cameraEnabled;hud.health.invulnerable=false;}
            Debug.Log(Result);
        }
    }
}
