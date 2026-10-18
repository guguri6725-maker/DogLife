using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace DogLife
{
    public sealed partial class KrakenPhaseTwo
    {
        sealed class HeartSlam
        {
            public int arm;
            public float began,length;
            public Vector3 start,from,raised,end,forward;
            public bool hit,impacted;
            public KrakenDeepVfx warning;
        }
        public bool SpecialIntro {get;private set;}
        IEnumerator AlternatingSlams(int strikeCount)
        {
            State=Phase.Heart;HeartExecutions++;HeartSlamsCompleted=0;
            SpecialIntro=true;Sound.Cue("Kraken_Roar");Play("Roar",heartIntroDuration);
            var cue=Effect(KrakenDeepVfx.Form.Orb,center);cue.Size=Vector3.one*1.5f;
            var tips=new Vector3[arms.Count];for(int i=0;i<arms.Count;i++)tips[i]=arms[i].Tip;
            for(float t=0;t<heartIntroDuration;t+=Time.deltaTime)
            {
                float q=Mathf.SmoothStep(0,1,t/heartIntroDuration);cue.Progress=q;cue.Intensity=.2f+.45f*q;
                for(int i=0;i<arms.Count;i++)
                {
                    IdleTargets(i,out var root,out var end,out var curve);
                    var ready=root+(end-root)*.8f+Vector3.up*heartIntroLift;
                    arms[i].SetShape(root,Vector3.Lerp(tips[i],ready,q),curve+Vector3.up*q,true);
                }
                yield return null;
            }
            Remove(cue);SpecialIntro=false;
            var strikes=new List<HeartSlam>();int launched=0;float next=Time.time;
            while(launched<strikeCount||strikes.Count>0)
            {
                int available=-1;
                for(int j=0;j<arms.Count;j++)
                {
                    int candidate=(launched+j)%arms.Count;
                    if(!strikes.Exists(s=>s.arm==candidate)){available=candidate;break;}
                }
                if(launched<strikeCount&&Time.time>=next&&available>=0)
                {
                    var arm=arms[available];var start=arm.SocketPosition;
                    var delta=Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up);
                    var forward=delta.sqrMagnitude>.01f?delta.normalized:facing;
                    float length=Mathf.Clamp(delta.magnitude,2,heartSlamReach);
                    var end=start+forward*length;end.y=arena.min.y+.2f;
                    var raised=start+forward*2+Vector3.up*heartRaiseHeight;
                    raised.y=Mathf.Max(raised.y,arena.max.y+heartOverheadClearance,player.transform.position.y+heartOverheadClearance);
                    var marker=Effect(KrakenDeepVfx.Form.Band,new Vector3((start.x+end.x)*.5f,arena.center.y,(start.z+end.z)*.5f));
                    marker.transform.rotation=Quaternion.LookRotation(forward);
                    marker.Size=new Vector3(heartSlamWidth,arena.size.y,length);
                    strikes.Add(new HeartSlam{arm=available,began=Time.time,start=start,from=arm.Tip,raised=raised,end=end,forward=forward,length=length,warning=marker});
                    launched++;next=Time.time+heartSlamInterval;
                    Sound.Cue("Kraken_Underwater_Windup",.55f);
                }
                for(int i=strikes.Count-1;i>=0;i--)
                {
                    var s=strikes[i];float age=Time.time-s.began;var arm=arms[s.arm];
                    if(age<heartSlamWarning)
                    {
                        float q=Mathf.SmoothStep(0,1,age/heartSlamWarning);
                        arm.SetShape(s.start,Vector3.Lerp(s.from,s.raised,q),Vector3.up*2,true);
                        s.warning.Progress=q;
                    }
                    else if(age<heartSlamWarning+heartSlamDuration)
                    {
                        if(!s.impacted){s.impacted=true;Sound.Cue("Kraken_Underwater_Strike");Play("Wave",heartSlamDuration);}
                        float q=Mathf.Clamp01((age-heartSlamWarning)/heartSlamDuration);
                        var tip=Vector3.Lerp(s.raised,s.end,q*q);
                        arm.SetShape(s.start,tip,Vector3.up*(1-q)*2);
                        s.warning.Impact=true;s.warning.Progress=q;
                        // Sweep a horizontal slice down the warned volume, including fast frames.
                        float lastQ=Mathf.Clamp01((age-Time.deltaTime-heartSlamWarning)/heartSlamDuration);
                        float previousY=Mathf.Lerp(s.raised.y,s.end.y,lastQ*lastQ);
                        var p=(s.start+s.end)*.5f;p.y=(previousY+tip.y)*.5f;
                        if(!s.hit)s.hit=BoxHit(p,new Vector3(heartSlamWidth,Mathf.Abs(previousY-tip.y)+heartSlamWidth,s.length),Quaternion.LookRotation(s.forward),heartDamage,heartKnockback);
                    }
                    else
                    {
                        if(!s.impacted){s.impacted=true;Sound.Cue("Kraken_Underwater_Strike");}
                        // Ensure the final slice is tested even if a frame skips past the short impact.
                        if(s.warning){
                            var p=(s.start+s.end)*.5f;p.y=s.end.y+heartSlamWidth*.5f;
                            if(!s.hit)s.hit=BoxHit(p,new Vector3(heartSlamWidth,heartSlamWidth,s.length),Quaternion.LookRotation(s.forward),heartDamage,heartKnockback);
                            Remove(s.warning);s.warning=null;HeartSlamsCompleted++;
                            var splash=Effect(KrakenDeepVfx.Form.Burst,s.end);splash.Size=new Vector3(heartSlamWidth,heartSlamWidth,0);splash.Impact=true;splash.Progress=1;
                            StartCoroutine(FadeSlamSplash(splash));
                        }
                        float holdAge=age-heartSlamWarning-heartSlamDuration;
                        if(holdAge<heartGroundHold)arm.SetShape(s.start,s.end,Vector3.up*.15f);
                        else
                        {
                            float q=Mathf.SmoothStep(0,1,(holdAge-heartGroundHold)/heartArmReturn);
                            IdleTargets(s.arm,out var root,out var end,out var curve);
                            arm.SetShape(root,Vector3.Lerp(s.end,end,q),curve,true);
                            if(holdAge>=heartGroundHold+heartArmReturn)strikes.RemoveAt(i);
                        }
                    }
                }
                attackBox.enabled=false;yield return null;
            }
            State=Phase.Groggy;Sound.Cue("Kraken_Groggy",.75f);Play("Groggy",groggyTime);
            yield return new WaitForSeconds(groggyTime);State=Phase.Idle;Play("Idle",1);
        }
        IEnumerator FadeSlamSplash(KrakenDeepVfx splash)
        {
            for(float t=0;t<heartArmReturn;t+=Time.deltaTime){if(!splash)yield break;splash.Intensity=1-t/heartArmReturn;yield return null;}
            Remove(splash);
        }
    }
}
