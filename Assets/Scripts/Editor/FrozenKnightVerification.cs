#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
namespace DogLife.EditorTools
{
    // 배치된 보스와 플레이어로만 실행하는 수동 검증 도구입니다. 씬/프리팹 설정은 저장하지 않습니다.
    public static class FrozenKnightVerification
    {
        static IEnumerator routine;
        static readonly List<string> results=new List<string>();
        static FrozenKnightBoss boss;
        static CombatHealth hp,playerHealth;
        static Transform player;
        static float deadline;
        public static string Status {get;private set;}="Not started";
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object target,string field,object value)=>target.GetType().GetField(field,Fields).SetValue(target,value);
        static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Fields).GetValue(target);
        static void Check(bool condition,string name){results.Add((condition?"PASS ":"FAIL ")+name);if(!condition)Debug.LogError("Frozen Knight verification: "+name);}
        public static string Start()
        {
            if(!EditorApplication.isPlaying)return "Requires play mode with an existing FrozenKnight and Player";
            boss=UnityEngine.Object.FindFirstObjectByType<FrozenKnightBoss>();var motor=UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
            if(!boss||!motor)return "Missing scene objects";
            hp=boss.GetComponent<CombatHealth>();player=motor.transform;playerHealth=motor.GetComponent<CombatHealth>();
            results.Clear();playerHealth.maxHealth=100;playerHealth.Restore();playerHealth.invulnerable=true;
            Set(boss,"engaged",true);Set(boss,"nextAttack",float.PositiveInfinity);Set(boss,"globalCooldown",float.PositiveInfinity);
            Time.timeScale=1;Status="Running";deadline=Time.realtimeSinceStartup+360;routine=Flatten(Encounters(false));EditorApplication.update-=Tick;EditorApplication.update+=Tick;return Status;
        }
        public static string StartWallChecks()
        {
            var status=Start();if(status!="Running")return status;
            routine=Flatten(WallChecks());return status;
        }
        static IEnumerator WallChecks()
        {
            var motor=player.GetComponent<PlayerMotor>();motor.readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            // 테스트 중의 접근 조건만 바꾸어 짧은 후퇴의 거리와 방향 고정을 측정합니다.
            Set(boss,"rangedMinimumDistance",100f);Set(boss,"nextRetreat",0f);
            Vector3 start=boss.transform.position;Check(boss.Begin(FrozenKnightBoss.Pattern.IcePillars),"Start wall summon");
            yield return null;
            Vector3 facing=boss.transform.forward;bool facingLocked=true;float dashStart=Time.time;bool dashShot=false;
            while(boss.State==FrozenKnightBoss.CombatState.Reposition)
            {facingLocked&=Vector3.Angle(facing,boss.transform.forward)<1;if(!dashShot&&Time.time-dashStart>.15f){CaptureAt("BackDashPreview",boss.transform.position+Vector3.up,new Vector3(5,2,6));dashShot=true;}yield return null;}
            Vector3 delta=boss.transform.position-start;
            Check(facingLocked&&Vector3.Dot(delta,facing)<=.01f&&delta.magnitude<=2.65f,"Back dash keeps facing and stays short");
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            var walls=UnityEngine.Object.FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None).Where(c=>c.IsPillar).ToArray();
            Check(walls.Length==3,"Three ice walls spawn");
            if(walls.Length>0)CaptureAt("TransparentWallPreview",walls[0].transform.position+Vector3.up*2.5f,new Vector3(9,3,12));
            var weapons=player.GetComponent<PlayerWeapons>();
            var id=AssetDatabase.FindAssets("t:InventoryWeapon").First();
            var weapon=AssetDatabase.LoadAssetAtPath<InventoryWeapon>(AssetDatabase.GUIDToAssetPath(id));
            var strike=typeof(PlayerWeapons).GetMethod("DamageSegment",Fields);
            foreach(var wall in walls)
            {
                var box=wall.GetComponent<BoxCollider>();
                for(int i=0;i<3;i++)
                {
                    // 양쪽 넓은 면과 옆면을 기존 근접 판정으로 실제 검사합니다.
                    Vector3 local=i==2?new Vector3(box.size.x*.5f,1,0):new Vector3(0,1,box.size.z*.5f*(i==0?1:-1));
                    Vector3 normal=i==2?wall.transform.right:wall.transform.forward*(i==0?1:-1);
                    Vector3 point=wall.transform.TransformPoint(local),origin=point+normal;
                    Get<HashSet<CombatHealth>>(weapons,"meleeDamaged").Clear();
                    strike.Invoke(weapons,new object[]{weapon,origin,origin,point-normal*.1f,.15f,1f});
                    Check(wall.Hits==i+1,"Wall face "+i+" accepts melee hit");yield return Delay(.3f);
                }
                Check(wall.Broken,"Wall breaks on third hit");
            }
            var core=Get<FrozenKnightSwordCore>(boss,"swordCore");core.Begin(3);
            Check(core.HitArea.isTrigger,"Sword target cannot physically block movement");
            var center=core.Center;bool found=Physics.SphereCastAll(center+Vector3.back*15,.25f,Vector3.forward,30,~0,QueryTriggerInteraction.Collide).Any(h=>h.collider==core.HitArea);
            Check(found,"Spear sweep detects sword trigger");core.Clear();
        }
        public static string StartFinaleMovement()
        {
            var value=Start();if(value!="Running")return value;
            routine=Flatten(FinaleMovement());return value;
        }
        static IEnumerator FinaleMovement()
        {
            player.GetComponent<PlayerMotor>().readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            hp.Damage(999999);while(boss.State==FrozenKnightBoss.CombatState.Transition)yield return null;
            // 검사 시간을 줄이되 위치 선택과 이동/정지 동작은 실제 패턴 코드를 실행합니다.
            Set(boss,"spearGap",.1f);Set(boss,"spearSpeed",40f);Set(boss,"spearWarning",.8f);Set(boss,"finalePrepare",.1f);Set(boss,"rideMoveSpeed",12f);
            hp.Damage(999999);var seen=new HashSet<FrozenKnightSpear>();var positions=new List<Vector3>();
            var horse=Get<Animator>(boss,"horseAnimator");bool frozen=true,walking=false;float fastest=0;float deadline=Time.time+100;
            while(Time.time<deadline&&positions.Count<4)
            {
                // 같은 쪽에 서서 검에 맞히지 않고 네 번의 위치 변경을 관찰합니다.
                var point=boss.transform.position+boss.transform.right*2;point.y=player.position.y;player.position=point;player.GetComponent<Rigidbody>().position=point;
                if(boss.State==FrozenKnightBoss.CombatState.Reposition&&horse.speed>0){walking=true;fastest=Mathf.Max(fastest,boss.GetComponent<NavMeshAgent>().velocity.magnitude);}
                var spear=UnityEngine.Object.FindObjectsByType<FrozenKnightSpear>(FindObjectsSortMode.None).FirstOrDefault(v=>!Get<bool>(v,"launched"));
                if(spear)
                {
                    frozen&=horse.speed==0;
                    if(seen.Add(spear)){positions.Add(boss.transform.position);results.Add("Throw position "+positions.Count+": "+boss.transform.position);}
                }
                yield return null;
            }
            Check(positions.Count==4,"Four distinct spear preparations");
            for(int i=1;i<positions.Count;i++)Check(Vector3.Distance(positions[i],positions[i-1])>=3.8f,"Reposition before spear "+(i+1));
            Check(walking,"Horse moves between throws");Check(fastest>8,"Final stand relocation reaches rush speed");Check(frozen,"Horse animation stays frozen during spear aim");
            boss.StopAllCoroutines();
        }
        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying||Time.realtimeSinceStartup>deadline)throw new Exception("Verification stopped or timed out");
                if(routine.MoveNext())return;
                Status=results.Any(r=>r.StartsWith("FAIL"))?"FAILED":"PASSED";Finish();
            }
            catch(Exception e){results.Add("FAIL "+e);Status="FAILED";Finish();}
        }
        static void Finish(){EditorApplication.update-=Tick;Directory.CreateDirectory("Temp/FrozenKnight");File.WriteAllLines("Temp/FrozenKnight/verification.txt",results);}
        static IEnumerator Run()
        {
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            Check(boss.GetComponent<NavMeshAgent>().isOnNavMesh,"Boss uses the existing baked NavMesh");
            var sword=boss.GetComponentsInChildren<Transform>().First(t=>t.name=="Sword");var hand=sword.parent.parent;
            Check(Vector3.Distance(hand.position,sword.TransformPoint(new Vector3(0,.85f,0)))<.01f,"Sword handle stays in the right hand");
            var area=Get<FrozenKnightDamageArea>(boss,"radialArea");var collider=player.GetComponent<Collider>();
            playerHealth.invulnerable=false;Set(playerHealth,"invulnerableUntil",0f);float before=playerHealth.Current;
            area.Arm(boss,1,0,0);area.Touch(collider);Set(playerHealth,"invulnerableUntil",0f);for(int i=0;i<8;i++)area.Touch(collider);
            Check(Mathf.Approximately(playerHealth.Current,before-1),"One hit per attack despite repeated player contacts");area.Disarm();
            Set(playerHealth,"invulnerableUntil",0f);before=playerHealth.Current;area.Arm(boss,1,0,0,null,1);area.Touch(collider);Set(playerHealth,"invulnerableUntil",0f);area.Touch(collider);
            Check(Mathf.Approximately(playerHealth.Current,before-1),"Damage tick cannot repeat immediately");float until=Time.time+1.1f;while(Time.time<until)yield return null;area.Touch(collider);
            Check(Mathf.Approximately(playerHealth.Current,before-2),"Damage tick can repeat after its interval");area.Disarm();playerHealth.invulnerable=true;
            for(int p=0;p<5;p++)
            {
                if(p==(int)FrozenKnightBoss.Pattern.IceBlade){Check(!boss.Begin(FrozenKnightBoss.Pattern.IceBlade),"Removed ice blade cannot be started");continue;}
                var pattern=(FrozenKnightBoss.Pattern)p;Check(boss.Begin(pattern),"Start phase 1 "+pattern);bool warning=false,active=false;bool shot=false;
                while(boss.State!=FrozenKnightBoss.CombatState.Idle)
                {warning|=boss.State==FrozenKnightBoss.CombatState.Warning;active|=boss.State==FrozenKnightBoss.CombatState.Active;if(active&&!shot){Snapshot(pattern.ToString());shot=true;}yield return null;}
                Check(warning&&active,"Warning then active and recovery: "+pattern);Check(!boss.GetComponentsInChildren<FrozenKnightDamageArea>().Any(a=>a.Armed),"All hitboxes disarmed after "+pattern);
            }
            Check(boss.Phase1SpecialUsed&&!boss.Begin(FrozenKnightBoss.Pattern.SwordFury),"Phase 1 special only once");
            int deaths=0;hp.Died+=()=>deaths++;hp.Damage(99999);Check(!hp.Dead&&hp.invulnerable&&deaths==0,"Phase 1 lethal damage defers death and enables invulnerability");hp.Damage(99999);
            var horse=Get<Transform>(boss,"horseRoot");var rider=Get<Transform>(boss,"riderRoot");bool approached=false,mountedAfterStop=true;float farthest=0;
            while(boss.State==FrozenKnightBoss.CombatState.Transition)
            {
                if(horse.gameObject.activeSelf){float distance=Vector3.Distance(horse.position,boss.transform.position);farthest=Mathf.Max(farthest,distance);if(distance>3)approached=true;if(rider.position.y>boss.transform.position.y+.5f&&distance>.15f)mountedAfterStop=false;}
                yield return null;
            }
            Check(approached&&farthest>3&&mountedAfterStop,"Horse runs in from a distance before rider mounts");
            Check(boss.PhaseNumber==2&&Mathf.Approximately(hp.Current,450)&&!hp.invulnerable&&deaths==0,"Separate phase 2 HP restored after mounting");Snapshot("MountedIdle");
            Check(boss.SwordLeftInGround&&boss.IceFieldActive,"Mounted phase leaves sword and slippery field on ground");
            Vector3 planted=Get<Transform>(boss,"swordRoot").position;
            for(int p=5;p<=10;p++)
            {
                if(p==(int)FrozenKnightBoss.Pattern.MultiCharge){Check(!boss.Begin(FrozenKnightBoss.Pattern.MultiCharge),"Removed multi charge cannot be started");continue;}
                if(p==(int)FrozenKnightBoss.Pattern.RideSlash){Check(!boss.Begin(FrozenKnightBoss.Pattern.RideSlash),"Removed mounted side slash cannot be started");continue;}
                var pattern=(FrozenKnightBoss.Pattern)p;Check(boss.Begin(pattern),"Start phase 2 "+pattern);bool warning=false,active=false,shot=false;Vector3 direction=Vector3.zero;
                while(boss.State!=FrozenKnightBoss.CombatState.Idle)
                {
                    warning|=boss.State==FrozenKnightBoss.CombatState.Warning;
                    if(boss.State==FrozenKnightBoss.CombatState.Active){active=true;if(!shot){direction=boss.Forward;Snapshot(pattern.ToString());shot=true;}if(pattern==FrozenKnightBoss.Pattern.Charge)Check(Vector3.Angle(direction,boss.Forward)<.01f,"Charge direction stays fixed");}
                    yield return null;
                }
                Check(warning&&active,"Warning then active and recovery: "+pattern);
                Check(Vector3.Distance(planted,Get<Transform>(boss,"swordRoot").position)<.01f,"Planted sword stays fixed during mounted "+pattern);
            }
            Check(boss.Phase2SpecialUsed&&!boss.Begin(FrozenKnightBoss.Pattern.MountedFury),"Phase 2 special only once");
            hp.Damage(99999);Check(hp.Dead&&deaths==1&&!boss.gameObject.activeSelf,"Phase 2 death fires once and deactivates boss");
            Check(!boss.IceFieldActive&&!boss.OuterFuryActive,"Boss death clears slippery field and outer danger");
        }
        // 새 전투 기믹 검증: 실제 씬의 프리팹과 물리 충돌을 사용하고 저장하지 않습니다.
        public static string StartEncounters(bool failFinale=false)
        {
            string value=Start();if(value!="Running")return value;
            deadline=Time.realtimeSinceStartup+360;routine=Flatten(Encounters(failFinale));return value;
        }
        public static string StartThresholds()
        {
            string value=Start();if(value!="Running")return value;
            deadline=Time.realtimeSinceStartup+180;routine=Flatten(Thresholds());return value;
        }
        public static string StartPolish()
        {
            string value=Start();if(value!="Running")return value;deadline=Time.realtimeSinceStartup+240;routine=Flatten(Polish());return value;
        }
        public static string StartHitAlignment()
        {
            string value=Start();if(value!="Running")return value;
            routine=Flatten(HitAlignment());return value;
        }
        public static string StartBladeWarning()
        {
            string value=Start();if(value!="Running")return value;
            routine=Flatten(BladeWarning());return value;
        }
        public static string StartImportedCasts()
        {
            string value=Start();if(value!="Running")return value;
            routine=Flatten(ImportedCasts());return value;
        }
        static IEnumerator ImportedCasts()
        {
            player.GetComponent<PlayerMotor>().readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            yield return ImportedCast(FrozenKnightBoss.Pattern.FrostBurst,"PushUpper");
            yield return ImportedCast(FrozenKnightBoss.Pattern.ExpandingBlade,"Spin3Sword");
            yield return ImportedCast(FrozenKnightBoss.Pattern.IcePillars,"IceWallFull");
            hp.Damage(99999);while(boss.State==FrozenKnightBoss.CombatState.Transition)yield return null;
            yield return ImportedCast(FrozenKnightBoss.Pattern.IcePillars,"IceWallMounted");
            yield return ImportedCast(FrozenKnightBoss.Pattern.SummonMinions,"IceWallMounted");
            foreach(var enemy in Get<List<CombatHealth>>(boss,"summonedMinions"))if(enemy)enemy.invulnerable=true;
            yield return ImportedCast(FrozenKnightBoss.Pattern.MountedIceBlades,"IceSwordMounted");
            yield return ImportedCast(FrozenKnightBoss.Pattern.RemoteSword,"IceSwordMounted");
        }
        static IEnumerator ImportedCast(FrozenKnightBoss.Pattern pattern,string state)
        {
            float began=Time.time;Check(boss.Begin(pattern),"Start imported "+pattern);bool matched=false,nativeSpeed=true;float clipLength=0;
            var animator=Get<Animator>(boss,"knightAnimator");
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)
            {
                var info=animator.GetCurrentAnimatorStateInfo(0);
                if(info.IsName(state)&&!CombatFeedback.Paused(boss))nativeSpeed&=Mathf.Abs(animator.speed-1)<.001f;
                if(!matched&&info.IsName(state)&&info.normalizedTime>.35f)
                {
                    matched=true;CaptureAt("Imported_"+pattern+"_P"+boss.PhaseNumber,boss.transform.position+Vector3.up*3,new Vector3(7,3,8));
                }
                yield return null;
            }
            Check(matched,"Correct imported state "+state+" for "+pattern);
            foreach(var clip in animator.runtimeAnimatorController.animationClips)if(clip.name==state)clipLength=clip.length;
            Check(nativeSpeed&&Time.time-began>=clipLength,"Native speed and complete animation duration for "+pattern);
        }
        // 생성 위치와 표적 위치를 분리하고 이동 중 표식의 실제 월드 좌표를 검사합니다.
        static IEnumerator BladeWarning()
        {
            var motor=player.GetComponent<PlayerMotor>();motor.readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            hp.Damage(99999);while(boss.State==FrozenKnightBoss.CombatState.Transition)yield return null;
            motor.enabled=false;player.GetComponent<Rigidbody>().isKinematic=true;
            var vfx=Get<FrozenKnightVfx>(boss,"effects");Vector3 start=player.position;
            Check(boss.Begin(FrozenKnightBoss.Pattern.MountedIceBlades),"Start moving-target ice blade warning");
            bool follows=true,fixedAfterLaunch=true,captured=false;int samples=0;Vector3 frozen=Vector3.zero;bool active=false;float began=Time.time,maxError=0;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)
            {
                Vector3 origin=Get<Vector3>(vfx,"origin");
                if(boss.State==FrozenKnightBoss.CombatState.Warning)
                {
                    var center=player.GetComponent<Collider>().bounds.center;
                    float error=Vector2.Distance(new Vector2(origin.x,origin.z),new Vector2(center.x,center.z));maxError=Mathf.Max(maxError,error);
                    if(samples>0)follows&=error<.15f+2*Time.deltaTime;
                    samples++;active=false;
                    if(samples>12&&!captured){CaptureAt("IceBlade_PlayerWarning",player.position,new Vector3(6,7,6));captured=true;}
                }
                else if(boss.State==FrozenKnightBoss.CombatState.Active)
                {
                    if(!active){frozen=origin;active=true;}else fixedAfterLaunch&=Vector3.Distance(origin,frozen)<.001f;
                }
                player.position=start+Vector3.right*Mathf.Sin((Time.time-began)*2);Physics.SyncTransforms();yield return null;
            }
            Check(samples>12&&follows,"Warning stays below moving target, not behind boss; max sampling error="+maxError);
            Check(fixedAfterLaunch,"Warning locks on launch instead of chasing afterward");
        }
        // 실제 프리팹의 발사/휘두르기에서 정지 표적 피격과 공중 회피를 비교합니다.
        static IEnumerator HitAlignment()
        {
            var motor=player.GetComponent<PlayerMotor>();motor.readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            hp.Damage(99999);while(boss.State==FrozenKnightBoss.CombatState.Transition)yield return null;
            var rb=player.GetComponent<Rigidbody>();motor.enabled=false;rb.isKinematic=true;
            Vector3 floor=Get<Vector3>(boss,"initialArenaCenter");
            Vector3 spot=boss.transform.position+boss.transform.forward*7;
            spot.y=floor.y+(player.position.y-player.GetComponent<Collider>().bounds.min.y)+.05f;
            player.position=spot;Physics.SyncTransforms();playerHealth.invulnerable=false;Set(playerHealth,"invulnerableUntil",0f);
            float before=playerHealth.Current;
            Check(boss.Begin(FrozenKnightBoss.Pattern.MountedIceBlades),"Start ice blades against stationary player");
            bool aligned=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)
            {
                foreach(var hazard in UnityEngine.Object.FindObjectsByType<FrozenKnightHazard>(FindObjectsSortMode.None))
                {
                    var area=Get<FrozenKnightDamageArea>(hazard,"damageArea");var visual=Get<Transform>(hazard,"projectileVisual");
                    if(!visual||!area.Armed)continue;
                    var render=visual.GetComponentInChildren<Renderer>();
                    aligned|=(area.Shape.bounds.center-render.bounds.center).magnitude<.01f&&(area.Shape.bounds.size-render.bounds.size).magnitude<.02f;
                }
                yield return null;
            }
            Check(aligned,"Flying blade collider matches visible mesh bounds");
            Check(playerHealth.Current<before,"Ice blade damages stationary target before planting");
            for(int air=0;air<2;air++)
            {
                player.position=spot+Vector3.up*(air==0?0:2);Physics.SyncTransforms();Set(playerHealth,"invulnerableUntil",0f);before=playerHealth.Current;
                Check(boss.Begin(FrozenKnightBoss.Pattern.RemoteSword),"Start remote sword "+air);
                bool height=true,captured=false;float maxY=float.NegativeInfinity;float maxRootY=float.NegativeInfinity;
                while(boss.State!=FrozenKnightBoss.CombatState.Idle)
                {
                    var swordArea=Get<FrozenKnightDamageArea>(boss,"swordArea");
                    if(swordArea.Armed)
                    {
                        if(!captured){CaptureAt("HitAlignment_Remote"+air,player.position,new Vector3(6,3,5));captured=true;}
                        height&=Mathf.Abs(Get<Transform>(boss,"swordRoot").position.y-(floor.y+Get<float>(boss,"remoteSwordHeight")))<.15f;
                        maxY=Mathf.Max(maxY,swordArea.Shape.bounds.max.y);maxRootY=Mathf.Max(maxRootY,Get<Transform>(boss,"swordRoot").position.y);
                    }
                    yield return null;
                }
                Check(height,"Remote sword stays at floor height "+air+" root="+maxRootY+" collider top="+maxY+" player bottom="+player.GetComponent<Collider>().bounds.min.y);
                Check(air==0?playerHealth.Current<before:playerHealth.Current==before,air==0?"Remote sword hits grounded target":"Jump height avoids remote sword");
            }
            var marker=boss.GetType().GetMethod("SpearMarkerOrigin",Fields);
            Vector3 direction=Vector3.forward;
            Vector3 start=(Vector3)marker.Invoke(boss,new object[]{direction});
            Vector3 end=start+direction*Get<float>(boss,"spearMarkerLength");
            Check(Vector2.Distance(new Vector2(end.x,end.z),new Vector2(player.position.x,player.position.z))<.1f,"Spear marker ends at player instead of spear");
            player.position+=Vector3.right*2;
            Vector3 moved=(Vector3)marker.Invoke(boss,new object[]{direction});
            Check(Mathf.Abs((moved-start).x-2)<.1f,"Spear marker follows moving player");
            playerHealth.invulnerable=true;player.position=spot;Physics.SyncTransforms();hp.Damage(99999);
            while(UnityEngine.Object.FindObjectsByType<FrozenKnightSpear>(FindObjectsSortMode.None).Length==0&&!hp.Dead)yield return null;
            yield return Delay(.8f);
            CaptureAt("PlayerSpearMarker",player.position,new Vector3(5,7,5));
            CaptureAt("RefinedSpearPose",boss.transform.position+Vector3.up*4,new Vector3(7,3,7));
        }
        static IEnumerator Polish()
        {
            var motor=player.GetComponent<PlayerMotor>();motor.readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            yield return Pattern(FrozenKnightBoss.Pattern.FrostBurst);
            yield return Pattern(FrozenKnightBoss.Pattern.ExpandingBlade);
            yield return Pattern(FrozenKnightBoss.Pattern.IcePillars);
            var columns=UnityEngine.Object.FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None).Where(c=>c.IsPillar).ToArray();
            Check(columns.Length==6,"Six pillars find clear spawn locations");
            var pillar=columns[0];var ph=pillar.GetComponent<CombatHealth>();Vector3 focus=pillar.transform.position+Vector3.up*2;
            Check(CombatFeedback.DealDamage(ph,999,focus,Vector3.forward,1,player,Color.cyan),"First pillar strike reports a real hit to weapon logic");
            yield return Delay(.3f);CaptureAt("Polish_Crack1",focus,new Vector3(3,1,5));
            var block=new MaterialPropertyBlock();pillar.GetComponentInChildren<Renderer>().GetPropertyBlock(block);float first=block.GetFloat("_CrackProgress");
            ph.Damage(999);yield return Delay(.3f);CaptureAt("Polish_Crack2",focus,new Vector3(3,1,5));pillar.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            Check(first>.3f&&block.GetFloat("_CrackProgress")>first+.25f,"DOTween progressively reveals persistent cracks");
            ph.Damage(999);yield return Delay(.15f);CaptureAt("Polish_Shatter",focus,new Vector3(3,1,5));
            Check(ph.Dead&&UnityEngine.Object.FindObjectsByType<FrozenIceBurst>(FindObjectsSortMode.None).Length>0,"Third hit destroys pillar and emits independent fragments");
            yield return Pattern(FrozenKnightBoss.Pattern.IcePillars);
            columns=UnityEngine.Object.FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None).Where(c=>c.IsPillar).ToArray();bool separate=true;
            for(int i=0;i<columns.Length;i++)for(int j=i+1;j<columns.Length;j++)if(columns[i].GetComponent<Collider>().bounds.Intersects(columns[j].GetComponent<Collider>().bounds))separate=false;
            Check(separate&&columns.Length>=6,"Repeated pillar summons do not overlap existing pillars");
            playerHealth.invulnerable=false;float before=playerHealth.Current;float started=Time.time;boss.Begin(FrozenKnightBoss.Pattern.ColdWave);yield return Delay(.4f);
            var pickups=UnityEngine.Object.FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None).Where(c=>c.name.Contains("Pickup")).ToArray();
            pickups[0].transform.position=player.position;yield return Delay(.1f);pickups[1].transform.position=player.position;yield return Delay(.2f);
            Check(playerHealth.FrostProtectionRemaining>5.8f,"Two crystals grant six seconds of protection");
            var hud=UnityEngine.Object.FindFirstObjectByType<FrostProtectionHUD>();Check(hud&&hud.GetComponent<CanvasGroup>().alpha==1,"Protection HUD appears after pickup");CaptureAt("Polish_Absorb",player.position+Vector3.up,new Vector3(4,3,6));
            while(boss.State==FrozenKnightBoss.CombatState.Warning)yield return null;
            Check(Time.time-started>=6.9f&&playerHealth.Current==before,"At least seven seconds to gather before storm damage");Check(playerHealth.FrostProtectionRemaining>5.8f,"Gathering does not consume protection time");
            yield return Delay(.4f);CaptureAt("Polish_Blizzard",boss.transform.position+Vector3.up,new Vector3(10,6,14));
            yield return Delay(6.8f);Check(playerHealth.FrostProtectionRemaining==0&&playerHealth.Current<before,"Protection expires and blizzard deals tick damage");
            playerHealth.invulnerable=true;while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            hp.Damage(9999);while(boss.State==FrozenKnightBoss.CombatState.Transition)yield return null;
            yield return Pattern(FrozenKnightBoss.Pattern.Stomp);yield return Pattern(FrozenKnightBoss.Pattern.MountedIceBlades);
            int deaths=0;hp.Died+=()=>deaths++;hp.Damage(9999);bool aimed=false,shot=false;
            var rb=player.GetComponent<Rigidbody>();
            while(!hp.Dead)
            {
                var core=Get<FrozenKnightSwordCore>(boss,"swordCore");
                if(core&&core.Broken&&boss.State==FrozenKnightBoss.CombatState.Groggy)
                {yield return VerifyFinalHit();break;}
                if(core&&!core.Broken)
                {
                    Vector3 d=boss.transform.position-core.Center;d.y=0;d.Normalize();Vector3 point=core.Center-d*7+Vector3.Cross(d,Vector3.up)*Mathf.Sin(Time.time)*.6f;point.y=1;player.position=point;rb.position=point;rb.linearVelocity=Vector3.zero;
                    var spear=UnityEngine.Object.FindFirstObjectByType<FrozenKnightSpear>();
                    if(spear&&boss.State==FrozenKnightBoss.CombatState.Warning)
                    {
                        var effect=Get<FrozenKnightVfx>(boss,"effects");Vector3 expected=player.position+Vector3.up-spear.transform.position;expected.y=0;
                        Vector3 displayed=Get<Vector3>(effect,"direction");if(Vector3.Angle(expected,displayed)<3)aimed=true;
                        if(spear.transform.localScale.x>.8f&&!shot){CaptureAt("Polish_SpearReady",spear.transform.position,new Vector3(5,3,7));shot=true;}
                    }
                }
                yield return null;
            }
            Check(aimed,"Simple spear direction follows the moving player during preparation");Check(deaths==1&&!playerHealth.Dead,"Fast hand-launched spears still allow three-hit core solution");
        }
        // 검 파괴와 보스 사망을 분리하고 아주 작은 피해도 마지막 한 타로 인정하는지 확인합니다.
        static IEnumerator VerifyFinalHit()
        {
            Check(!hp.Dead&&hp.Current==1&&boss.gameObject.activeInHierarchy,"Sword break leaves boss alive at one HP");
            yield return Delay(1);
            Check(!hp.Dead&&boss.State==FrozenKnightBoss.CombatState.Groggy,"Boss waits for finishing hit without restarting AI");
            Check(!Get<WinterIcePatch>(boss,"swordIcePatch").IsActive,"Slippery field stops after sword destruction");
            hp.Damage(.1f);
            Check(hp.Dead,"One positive finishing hit defeats boss regardless of damage amount");
        }
        static void CaptureAt(string name,Vector3 focus,Vector3 offset)
        {
            var camera=Camera.main;if(!camera)return;Vector3 p=camera.transform.position;Quaternion q=camera.transform.rotation;
            var old=camera.targetTexture;var previous=RenderTexture.active;var rt=new RenderTexture(1100,700,24);var tex=new Texture2D(1100,700,TextureFormat.RGB24,false);
            camera.transform.position=focus+offset;camera.transform.LookAt(focus);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1100,700),0,0);tex.Apply();
            Directory.CreateDirectory("Temp/FrozenKnight");File.WriteAllBytes("Temp/FrozenKnight/"+name+".png",tex.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=previous;camera.transform.SetPositionAndRotation(p,q);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
        }
        static IEnumerator Thresholds()
        {
            var motor=player.GetComponent<PlayerMotor>();motor.readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            Set(boss,"coldDuration",.6f);Set(boss,"coldWarning",.5f);
            Get<FrozenKnightBoss.CavalryFurySettings>(boss,"cavalryFury").charges=1;
            hp.Damage(hp.maxHealth*.31f);yield return Delay(.2f);
            Check(boss.CurrentPattern==FrozenKnightBoss.Pattern.ColdWave&&Get<bool>(boss,"coldHighUsed"),"70 percent starts cold wave");
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            yield return Delay(.5f);Check(boss.State==FrozenKnightBoss.CombatState.Idle,"70 percent cold wave only once");
            hp.Damage(hp.maxHealth*.2f);yield return Delay(.2f);Check(boss.CurrentPattern==FrozenKnightBoss.Pattern.SwordFury&&boss.Phase1SpecialUsed,"50 percent retains safe-zone horse curse");
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            hp.Damage(hp.maxHealth*.2f);yield return Delay(.2f);Check(boss.CurrentPattern==FrozenKnightBoss.Pattern.ColdWave&&Get<bool>(boss,"coldLowUsed"),"30 percent starts independent cold wave");
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            yield return Delay(.5f);Check(boss.State==FrozenKnightBoss.CombatState.Idle,"30 percent cold wave only once");
            hp.Damage(9999);while(boss.State==FrozenKnightBoss.CombatState.Transition)yield return null;
            playerHealth.invulnerable=false;Set(playerHealth,"invulnerableUntil",0f);float before=playerHealth.Current;
            yield return Pattern(FrozenKnightBoss.Pattern.RemoteSword);
            Check(playerHealth.Current<before,"Remote sword physical sweep hits player");
        }
        static IEnumerator Flatten(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0){var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}if(top.Current is IEnumerator nested)stack.Push(nested);else yield return null;}
        }
        static IEnumerator Delay(float seconds){float end=Time.time+seconds;while(Time.time<end)yield return null;}
        static IEnumerator Pattern(FrozenKnightBoss.Pattern pattern)
        {
            Check(boss.Begin(pattern),"Start "+pattern);bool warning=false,active=false,shot=false;float activeAt=-1;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)
            {warning|=boss.State==FrozenKnightBoss.CombatState.Warning;active|=boss.State==FrozenKnightBoss.CombatState.Active;if(active&&activeAt<0)activeAt=Time.time;if(active&&!shot&&Time.time-activeAt>.2f){Snapshot("New_"+pattern);shot=true;}yield return null;}
            Check(warning&&active,"Complete warning/active/recovery "+pattern);
        }
        static IEnumerator Encounters(bool failFinale)
        {
            var motor=player.GetComponent<PlayerMotor>();motor.readInput=false;
            while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            if(!failFinale)
            {
                yield return Pattern(FrozenKnightBoss.Pattern.FrostBurst);
                yield return Pattern(FrozenKnightBoss.Pattern.IcePillars);
                var pillar=UnityEngine.Object.FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None).First(c=>c.name.Contains("Pillar"));
                var ph=pillar.GetComponent<CombatHealth>();ph.Damage(999);Check(!ph.Dead&&pillar.Hits==1,"Pillar survives first hit");yield return Delay(.15f);ph.Damage(999);Check(!ph.Dead&&pillar.Hits==2,"Pillar survives second hit");yield return Delay(.15f);ph.Damage(999);Check(ph.Dead,"Pillar breaks on third hit");
                for(int evasion=0;evasion<3;evasion++)
                {
                    var body=player.GetComponent<Rigidbody>();Vector3 at=boss.transform.position+boss.Forward*3+Vector3.up;body.position=at;player.position=at;body.linearVelocity=Vector3.zero;
                    motor.SetCrouching(evasion==1);playerHealth.invulnerable=false;Set(playerHealth,"invulnerableUntil",0f);float healthBefore=playerHealth.Current;
                    Check(boss.Begin(FrozenKnightBoss.Pattern.ExpandingBlade),"Start wave avoidance case "+evasion);
                    while(boss.State==FrozenKnightBoss.CombatState.Warning)yield return null;
                    if(evasion==2)motor.RequestJump();
                    while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
                    Check(evasion==0?playerHealth.Current<healthBefore:playerHealth.Current==healthBefore,"Wave "+(evasion==0?"hits standing player":evasion==1?"crouch avoids":"jump avoids"));
                    motor.SetCrouching(false);playerHealth.invulnerable=true;
                }
                Check(boss.Begin(FrozenKnightBoss.Pattern.ColdWave),"Start cold wave");yield return Delay(.5f);
                var pickups=UnityEngine.Object.FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None).Where(c=>c.name.Contains("Pickup")).ToArray();
                pickups[0].transform.position=player.position;yield return Delay(.1f);float first=playerHealth.FrostProtectionRemaining;pickups[1].transform.position=player.position;yield return Delay(.1f);
                Check(first>2.8f&&playerHealth.FrostProtectionRemaining>first+2.9f,"Crystal protection stacks three seconds each");Snapshot("New_ColdWave");
                while(boss.State!=FrozenKnightBoss.CombatState.Idle)yield return null;
            }
            int deaths=0;hp.Died+=()=>deaths++;hp.Damage(99999);
            while(boss.State==FrozenKnightBoss.CombatState.Transition)yield return null;
            Check(boss.PhaseNumber==2&&!hp.Dead,"Phase 2 separate health");
            if(!failFinale)
            {
                Check(!boss.Begin(FrozenKnightBoss.Pattern.MountedFury),"Removed mounted fury cannot start");
                yield return Pattern(FrozenKnightBoss.Pattern.IcePillars);
                yield return Pattern(FrozenKnightBoss.Pattern.SummonMinions);
                var list=Get<List<CombatHealth>>(boss,"summonedMinions");Check(list.Count==3,"Winter minions spawn three");
                yield return Pattern(FrozenKnightBoss.Pattern.SummonMinions);Check(list.Count==3,"Repeat summon never exceeds three");
                yield return Pattern(FrozenKnightBoss.Pattern.CloneCharge);
                Vector3 rest=Get<Transform>(boss,"swordRoot").position;yield return Pattern(FrozenKnightBoss.Pattern.RemoteSword);Check(Vector3.Distance(rest,Get<Transform>(boss,"swordRoot").position)<.02f,"Remote sword returns to planted position");
            }
            hp.Damage(99999);Check(!hp.Dead&&deaths==0&&boss.CurrentPattern==FrozenKnightBoss.Pattern.FinalStand,"Phase 2 lethal damage starts final stand without death event");
            FrozenKnightSwordCore core=null;bool captured=false;var rb=player.GetComponent<Rigidbody>();
            while(!hp.Dead)
            {
                if(!core)core=Get<FrozenKnightSwordCore>(boss,"swordCore");
                if(core&&core.Broken&&boss.State==FrozenKnightBoss.CombatState.Groggy)
                {yield return VerifyFinalHit();break;}
                if(core&&!core.Broken)
                {
                    if(!captured){hp.Damage(9999);Check(core.Hits==0&&!hp.Dead,"Boss stays invulnerable before sword breaks");Snapshot("New_FinalStand");captured=true;}
                    Vector3 direction=boss.transform.position-core.Center;direction.y=0;direction.Normalize();
                    Vector3 point=core.Center+(failFinale?direction:-direction)*7;point.y=1;player.position=point;rb.position=point;rb.linearVelocity=Vector3.zero;
                }
                yield return null;
            }
            Check(deaths==1,"Final stand sends exactly one death event");Check(failFinale?playerHealth.Dead:!playerHealth.Dead,failFinale?"Missed spears cause fatal explosion":"Three real spear impacts break core and save player");
            yield return Delay(.1f);Check(UnityEngine.Object.FindObjectsByType<FrozenKnightConstruct>(FindObjectsSortMode.None).Length==0,"Encounter objects cleaned up after death");
        }
        static void Snapshot(string name)
        {
            var camera=GameObject.Find("BossReviewCamera")?.GetComponent<Camera>()??Camera.main;if(!camera)return;
            Vector3 originalPosition=camera.transform.position;Quaternion originalRotation=camera.transform.rotation;
            // 임시 렌더 텍스처만 사용하며 프로젝트 에셋이나 카메라 설정을 저장하지 않습니다.
            camera.transform.position=boss.transform.position+boss.transform.TransformDirection(new Vector3(5,4,8));camera.transform.LookAt(boss.transform.position+Vector3.up*2);
            var rt=new RenderTexture(1100,700,24);var old=camera.targetTexture;var previous=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1100,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1100,700),0,0);image.Apply();
            Directory.CreateDirectory("Temp/FrozenKnight");File.WriteAllBytes("Temp/FrozenKnight/"+name+".png",image.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=previous;camera.transform.SetPositionAndRotation(originalPosition,originalRotation);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
        public static IEnumerator VerifySlowDash(PlayerMotor motor)
        {
            // Editor 콜백 대신 실제 FixedUpdate 뒤에서 속도를 측정합니다.
            var body=motor.GetComponent<Rigidbody>();motor.readInput=false;motor.MoveInput=Vector2.up;
            Set(motor,"nextDash",0f);Set(motor,"movementSlowUntil",0f);motor.ApplyMovementSlow(.6f,1.2f);
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            var lines=new List<string>{"slow="+Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude+" expected="+motor.moveSpeed*.6f};
            lines.Add("request="+motor.RequestDash());
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            lines.Add("dash="+Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude+" expected="+motor.dashSpeed+" active="+motor.IsDashing);
            yield return new WaitForSeconds(1.4f);yield return new WaitForFixedUpdate();
            lines.Add("expired="+Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude+" expected="+motor.moveSpeed);
            motor.MoveInput=Vector2.zero;File.WriteAllLines("Temp/FrozenKnight/SlowDashPhysics.txt",lines);
        }
    }
}
#endif
