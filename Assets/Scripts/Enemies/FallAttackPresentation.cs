using UnityEngine;

namespace DogLife
{
    // Hierarchy에 연결된 외형과 효과만 움직입니다. 이 컴포넌트에서는 피해를 주지 않습니다.
    public sealed class FallAttackPresentation : MonoBehaviour
    {
        [SerializeField] Transform model, upperArm, foreArm, hand;
        [SerializeField] LineRenderer warningRing, burrowMarker;
        [SerializeField] Transform[] spikes=new Transform[0],sporeMotes=new Transform[0];
        [SerializeField] GameObject chargeVfx;
        [SerializeField] ParticleSystem sporeCloud;
        [SerializeField,Range(.3f,1.5f)] float burrowFraction=1.2f;
        [SerializeField] float bodyHeight=1.15f, spikeHeight=1.8f;
        [SerializeField,Range(0,60)] float spikeLean=33;
        Vector3 modelHome,armScale,handScale,handHome;
        Vector3[] spikeHome,spikeScale;
        bool initialized;
        readonly System.Collections.Generic.List<Vector3> warningPoints=new System.Collections.Generic.List<Vector3>(112);
        public float BurrowAmount { get; private set; }
        void Awake(){Cache();ResetVisuals();}
        void Cache()
        {
            if(initialized)return;initialized=true;
            if(model)modelHome=model.localPosition;
            if(foreArm)armScale=foreArm.localScale;if(hand){handScale=hand.localScale;handHome=hand.localPosition;}
            spikeHome=new Vector3[spikes.Length];spikeScale=new Vector3[spikes.Length];
            for(int i=0;i<spikes.Length;i++)if(spikes[i]){spikeHome[i]=spikes[i].localPosition;spikeScale[i]=spikes[i].localScale;}
        }
        public void Apply(FallMonsterEnemy enemy)
        {
            Cache();var phase=enemy.CurrentPhase;float t=enemy.Progress;
            bool ready=phase==FallMonsterEnemy.Phase.Anticipation;
            bool attack=phase==FallMonsterEnemy.Phase.Attack;
            bool active=phase==FallMonsterEnemy.Phase.Active;
            bool recover=phase==FallMonsterEnemy.Phase.Recovery;
            if(warningRing)
            {
                warningRing.enabled=ready||attack||active;
                warningRing.useWorldSpace=true;
                // 가는 구리색 경계와 준비 단계의 밝기 변화로 바닥 장식을 살립니다.
                float pulse=.72f+.12f*Mathf.Sin(Time.time*5);
                // 공격 순간에도 준비 단계의 황갈색을 유지합니다.
                Color color=new Color(.95f,.62f,.22f,active?.85f:pulse);
                warningRing.startColor=color;warningRing.endColor=color;
                if(enemy.AttackPattern==FallMonsterEnemy.Pattern.ArmSweep||enemy.AttackPattern==FallMonsterEnemy.Pattern.CrowDash)
                {
                    // 손 끝의 판정 두께와 어깨 위치까지 실제 공격 외곽에 포함합니다.
                    enemy.GetWarningOutline(warningPoints,.07f);warningRing.positionCount=warningPoints.Count;
                    for(int i=0;i<warningPoints.Count;i++)warningRing.SetPosition(i,warningPoints[i]);
                }
                else
                {
                    warningRing.positionCount=65;
                    for(int i=0;i<65;i++){float angle=i*Mathf.PI*2/64;warningRing.SetPosition(i,enemy.transform.position+new Vector3(Mathf.Cos(angle)*enemy.Radius,.07f,Mathf.Sin(angle)*enemy.Radius));}
                }
            }
            if(chargeVfx)chargeVfx.SetActive(ready||attack);
            if(enemy.AttackPattern==FallMonsterEnemy.Pattern.Spores)
            {
                if(sporeCloud){var shape=sporeCloud.shape;shape.radius=enemy.Radius*.82f;
                    if(active&&!sporeCloud.isPlaying)sporeCloud.Play();
                    else if(!active&&sporeCloud.isPlaying)sporeCloud.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
                for(int i=0;i<sporeMotes.Length;i++)if(sporeMotes[i])
                {
                    sporeMotes[i].gameObject.SetActive(active);
                    float angle=i*2.39996f+Time.time*.45f;float r=enemy.Radius*Mathf.Sqrt((i+.5f)/sporeMotes.Length);
                    sporeMotes[i].localPosition=new Vector3(Mathf.Cos(angle)*r,.55f+.5f*Mathf.Sin(Time.time*2+i),Mathf.Sin(angle)*r);
                    float size=.035f+.025f*(1+Mathf.Sin(Time.time*3+i));sporeMotes[i].localScale=Vector3.one*size;
                }
            }
            if(enemy.AttackPattern==FallMonsterEnemy.Pattern.Spikes)
            {
                // 앞발로 땅 파는 동작을 먼저 보여준 뒤 마지막 구간에 잠복합니다.
                float sink=ready?.25f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t)):attack?Mathf.Lerp(.25f,1,Mathf.SmoothStep(0,1,t)):active?1:recover?1-Mathf.SmoothStep(0,1,t):0;
                BurrowAmount=sink;
                if(model)model.localPosition=modelHome-Vector3.up*(bodyHeight*burrowFraction*sink);
                // 땅속에 숨은 위치는 작은 낙엽색 원으로 회복이 끝날 때까지 표시합니다.
                if(burrowMarker)
                {
                    burrowMarker.enabled=sink>.05f;burrowMarker.useWorldSpace=true;burrowMarker.positionCount=49;
                    // 안전 반경은 그대로 두고 흙·낙엽 장식 위에 가는 금빛 경계만 남깁니다.
                    var markerColor=new Color(.88f,.65f,.30f,Mathf.SmoothStep(0,.8f,Mathf.Clamp01(sink*4)));
                    burrowMarker.startColor=markerColor;burrowMarker.endColor=markerColor;
                    for(int i=0;i<49;i++){float a=i*Mathf.PI*2/48;float r=enemy.SpikeSafeRadius;burrowMarker.SetPosition(i,enemy.transform.position+new Vector3(Mathf.Cos(a)*r,.06f,Mathf.Sin(a)*r));}
                }
                float rise=active?Mathf.Clamp01(t/.2f):recover?1-Mathf.Clamp01(t/.45f):0;
                for(int i=0;i<spikes.Length;i++)if(spikes[i])
                {
                    spikes[i].gameObject.SetActive(rise>0);
                    // 세 겹을 엇갈리게 배치해 중앙 표시 바깥의 피해 구역을 촘촘히 채웁니다.
                    int ring=i%3,index=i/3,count=(spikes.Length+2-ring)/3;
                    float angle=(index+(ring*.37f))*Mathf.PI*2/Mathf.Max(1,count);
                    float r=enemy.Radius*(.28f+ring*.23f)+.04f*Mathf.Sin(i*3.1f);
                    spikes[i].localPosition=new Vector3(Mathf.Cos(angle)*r,Mathf.Lerp(-spikeHeight,.02f,rise),Mathf.Sin(angle)*r);
                    // 등 중심에서 바깥으로 퍼지는 부채꼴 가시: 높이와 기울기를 함께 변화시킵니다.
                    Vector3 outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    float lean=(spikeLean+3*Mathf.Sin(i))*Mathf.Deg2Rad;
                    spikes[i].localRotation=Quaternion.FromToRotation(Vector3.up,outward*Mathf.Sin(lean)+Vector3.up*Mathf.Cos(lean));
                    var scale=spikeScale[i];
                    // 바깥 가시가 경고 원을 넘지 않도록 끝 길이를 반경 안에 맞춥니다.
                    float length=Mathf.Min(spikeHeight*scale.y,(enemy.Radius-r)/Mathf.Max(.1f,Mathf.Sin(lean)));
                    scale.y=(length/spikeHeight)*Mathf.Lerp(.15f,1,rise);spikes[i].localScale=scale;
                }
            }
            if(enemy.AttackPattern==FallMonsterEnemy.Pattern.ArmSweep&&upperArm&&foreArm&&hand)
            {
                // 회수 앞 80% 동안 끝 자세에서 팔을 줄이고, 나머지 시간에 기본 자세로 풀어 줍니다.
                float extend=attack?Mathf.SmoothStep(0,1,t):active?1:recover?1-Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.8f)):0;
                // 손 뼈의 위치로 전완을 늘립니다. 부모의 비균일 스케일을 없애 손의 전단 변형도 방지합니다.
                foreArm.localScale=armScale;hand.localScale=handScale;hand.localPosition=handHome;
                float upperLength=Vector3.Distance(upperArm.position,foreArm.position);
                float foreLength=Vector3.Distance(foreArm.position,hand.position);
                float ratio=Mathf.Lerp(1,Mathf.Max(1,(enemy.ArmLength-upperLength)/Mathf.Max(.1f,foreLength)),extend);
                hand.localPosition=handHome*ratio;
                if(ready||attack||active||recover)
                {
                    // 회수 단계에서도 휘두르기가 끝난 방향을 유지합니다.
                    float angle=recover?enemy.SweepAngle*.5f:active?Mathf.Lerp(-enemy.SweepAngle*.5f,enemy.SweepAngle*.5f,t):-enemy.SweepAngle*.5f;
                    Vector3 dir=Quaternion.AngleAxis(angle,Vector3.up)*enemy.LockedDirection;
                    // 준비 자세도 천천히 맞추어 연장 시작 순간 팔이 튀지 않게 합니다.
                    float poseWeight=ready?Mathf.SmoothStep(0,1,t):recover?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1,t)):1;
                    upperArm.rotation=Quaternion.Slerp(upperArm.rotation,Quaternion.FromToRotation(foreArm.position-upperArm.position,dir)*upperArm.rotation,poseWeight);
                    foreArm.rotation=Quaternion.Slerp(foreArm.rotation,Quaternion.FromToRotation(hand.position-foreArm.position,dir)*foreArm.rotation,poseWeight);
                    // 부모 뼈의 스케일 때문에 회전 첫 프레임에 팔이 사거리보다 길어지지 않게 보정합니다.
                    if(poseWeight>=.999f)
                    {
                        float remaining=Mathf.Max(.1f,enemy.ArmLength-Vector3.Distance(upperArm.position,foreArm.position));
                        hand.position=foreArm.position+dir*Mathf.Lerp(foreLength,remaining,extend);
                    }
                }
            }
        }
        public bool ArmPoints(out Vector3 a,out Vector3 b,out Vector3 c)
        {a=upperArm?upperArm.position:Vector3.zero;b=foreArm?foreArm.position:a;c=hand?hand.position:b;return upperArm&&foreArm&&hand;}
        public void ResetVisuals()
        {
            BurrowAmount=0;
            Cache();if(warningRing)warningRing.enabled=false;if(burrowMarker)burrowMarker.enabled=false;if(chargeVfx)chargeVfx.SetActive(false);
            if(sporeCloud)sporeCloud.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(model)model.localPosition=modelHome;if(foreArm)foreArm.localScale=armScale;if(hand){hand.localScale=handScale;hand.localPosition=handHome;}
            foreach(var s in spikes)if(s)s.gameObject.SetActive(false);
            foreach(var s in sporeMotes)if(s)s.gameObject.SetActive(false);
        }
        void OnDisable(){ResetVisuals();}
    }
}
