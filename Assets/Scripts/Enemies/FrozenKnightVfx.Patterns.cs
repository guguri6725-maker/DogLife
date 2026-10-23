using UnityEngine;
using UnityEngine.Rendering;
namespace DogLife
{
    public sealed partial class FrozenKnightVfx
    {
        public enum Theme { Auto, Slash, Crack, Burst, Fury, HorseCharge, Charge, Stomp, MultiCharge, MountedFury, BladeCharge, BladeFlight, PlantedBlade, IceTrail, SwordField, Summon, MultiReady, RoyalReady, BladeWave, ColdField, SwordConjure, SpearAim, WallEnclosure }
        Vector3[] wallPoints;Quaternion[] wallRotations;Vector3 wallSize;
        public void ShowWallEnclosure(Vector3[] points,Quaternion[] rotations,Vector3 size)
        {
            wallPoints=points;wallRotations=rotations;wallSize=size;
            Show(Shape.Ring,points[0],Vector3.forward,1,0,Theme.WallEnclosure);
        }
        int chargeIndex,chargeCount=1;
        public void SetChargeSequence(int index,int count){chargeIndex=index;chargeCount=Mathf.Max(1,count);}
        [Header("기사의 얼음 / 은백색 공통 재질")]
        [SerializeField] Color iceTint=new Color(.36f,.68f,.88f,1);
        [SerializeField,Range(.2f,2)] float ornamentIntensity=1;
        [SerializeField,Min(.1f)] float auroraHeight=3.5f,burstHeight=2.8f;
        Theme theme;
        Theme ResolveTheme()
        {
            if(!boss)return detailedIceSurface?Theme.SwordField:Theme.Auto;
            switch(boss.CurrentPattern)
            {
                case FrozenKnightBoss.Pattern.Combo:return Theme.Slash;
                case FrozenKnightBoss.Pattern.Crack:return Theme.Crack;
                case FrozenKnightBoss.Pattern.FrostBurst:return Theme.Burst;
                case FrozenKnightBoss.Pattern.SwordFury:return Theme.Fury;
                case FrozenKnightBoss.Pattern.Charge:return Theme.Charge;
                case FrozenKnightBoss.Pattern.CloneCharge:return Theme.MountedFury;
                case FrozenKnightBoss.Pattern.Stomp:return Theme.Stomp;
                case FrozenKnightBoss.Pattern.MultiCharge:return Theme.MultiCharge;
                case FrozenKnightBoss.Pattern.MountedFury:return Theme.MountedFury;
                case FrozenKnightBoss.Pattern.MountedIceBlades:return Theme.PlantedBlade;
                default:return Theme.Auto;
            }
        }
        Color Ink(Color color,float alpha){color.a=alpha*ornamentIntensity;return color;}
        Color Accent()
        {
            return iceTint;
        }
        // 갑옷과 대검의 넓고 각진 면을 닮은 입체 빙판 조각입니다. 원뿔 가시와 실루엣이 다릅니다.
        void IcePlate(Vector3 p,Quaternion rotation,float w,float h,float depth,float alpha)
        {
            if(h<.02f)return;
            Vector3 a=p+rotation*new Vector3(-w*.5f,0,-depth*.5f),b=p+rotation*new Vector3(w*.5f,0,-depth*.5f);
            Vector3 c=p+rotation*new Vector3(w*.4f,h*.76f,-depth*.35f),d=p+rotation*new Vector3(-w*.28f,h*.68f,-depth*.35f);
            Vector3 tip=p+rotation*new Vector3(w*.1f,h,0),back=rotation*Vector3.forward*depth;
            IceFace(a,b,c,d,Ink(iceTint,alpha));IceFace(d,c,tip,tip,Ink(Color.Lerp(iceTint,Color.white,.5f),alpha));
            IceFace(a,a+back,b+back,b,Ink(iceTint*.65f,alpha));IceFace(a,d,d+back,a+back,Ink(iceTint*.6f,alpha));
            IceFace(b,b+back,c+back,c,Ink(Color.Lerp(iceTint,Color.white,.2f),alpha));IceFace(a+back,d+back,c+back,b+back,Ink(iceTint*.8f,alpha));
            IceFace(d,d+back,tip+back*.5f,tip,Ink(Color.white,alpha*.7f));IceFace(c,tip,tip+back*.5f,c+back,Ink(Color.white,alpha*.8f));
        }
        // 얼음 면에는 별도 표면 모드를 주어 내부 결 및 시선 방향 광택을 계산합니다.
        void IceFace(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color tint)
        {SoftSurface(a,b,c,d,tint,5,0);}
        void Hoofprint(Vector3 p,float size,Color color)
        {
            Arc(p,size,-135,135,.07f,color);
            Stroke(p+Radial(-135,size),p+Radial(-135,size*.65f),.09f,color);
            Stroke(p+Radial(135,size),p+Radial(135,size*.65f),.09f,color);
        }
        void Arc(Vector3 center,float radius,float from,float to,float thickness,Color color,float height=0)
        {
            int n=Mathf.Max(8,Mathf.CeilToInt(segments*Mathf.Abs(to-from)/360));
            for(int i=0;i<n;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)n),b=Mathf.Lerp(from,to,(i+1f)/n);
                Vector3 pa=center+Radial(a,radius),pb=center+Radial(b,radius);
                if(height>0)Quad(pa,pb,pb+Vector3.up*height,pa+Vector3.up*height,color);
                else Quad(center+Radial(a,radius-thickness),pa,pb,center+Radial(b,radius-thickness),color);
            }
        }
        void Shard(Vector3 point,float angle,float height,float breadth,Color color)
        {
            if(height<.01f||!crystalMesh)return;
            properties.SetColor("_Tint",color);
            Graphics.DrawMesh(crystalMesh,Matrix4x4.TRS(point,Quaternion.Euler(7,angle,12),new Vector3(breadth,height,breadth)),frostMaterial,gameObject.layer,null,0,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
        }
        // 피해 범위의 외곽은 공통으로 정확히 유지하고 내부 문양과 발동 동작을 분리합니다.
        void Boundary(Vector3 c,Color accent)
        {
            // 기존 문양/가장자리 색은 유지하고 바탕만 공통 예고색으로 구분합니다.
            Color fill=active?Ink(accent,.15f):new Color(1,.30f,.36f,.24f),edge=Ink(Color.Lerp(accent,Color.white,.45f),.78f);
            if(shape==Shape.Line)
            {
                Vector3 side=Vector3.Cross(Vector3.up,direction).normalized*width*.5f,end=c+direction*length;
                Quad(c-side,c+side,end+side,end-side,fill);Stroke(c-side,end-side,edgeWidth,edge);Stroke(c+side,end+side,edgeWidth,edge);Stroke(end-side,end+side,edgeWidth,edge);
            }
            else
            {
                float angle=shape==Shape.Sector?width:360,inner=shape==Shape.Ring?width:0;
                for(int i=0;i<segments;i++)
                {float a=-angle*.5f+angle*i/segments,b=-angle*.5f+angle*(i+1)/segments;Quad(c+Radial(a,inner),c+Radial(a,length),c+Radial(b,length),c+Radial(b,inner),fill);}
                Arc(c,length,-angle*.5f,angle*.5f,edgeWidth,edge);
                if(inner>0)Arc(c,inner+edgeWidth,0,360,edgeWidth,edge);
                if(shape==Shape.Sector){Stroke(c,c+Radial(-angle*.5f,length),edgeWidth,edge);Stroke(c,c+Radial(angle*.5f,length),edgeWidth,edge);}
            }
        }
        void DrawStyledArea()
        {
            Clear();Vector3 c=origin+Vector3.up*floorOffset;Color accent=Accent();float age=Time.time-started;
            if(theme==Theme.BladeCharge||theme==Theme.BladeFlight){DrawBladeAura(accent,age);Submit(areaMesh);return;}
            // 시작 문장은 피해 범위가 아니므로 일반 범위 테두리를 그리지 않습니다.
            if(theme==Theme.MultiReady||theme==Theme.RoyalReady){DrawChargeEmblem(c,age,theme==Theme.RoyalReady);Submit(areaMesh);return;}
            if(theme==Theme.SpearAim)
            {
                Vector3 side=Vector3.Cross(Vector3.up,direction),end=c+direction*length;
                Stroke(c,end,.065f,Ink(Color.white,.65f));
                for(int i=1;i<=5;i++){Vector3 p=c+direction*length*i/6;Stroke(p-direction*.6f+side*.45f,p,.1f,Ink(iceTint,.9f));Stroke(p-direction*.6f-side*.45f,p,.1f,Ink(iceTint,.9f));}
                Submit(areaMesh);return;
            }
            if(theme==Theme.SwordConjure||theme==Theme.WallEnclosure)
            {
                // 칼날 주변을 감싸 올라가는 냉기입니다. 수평 원판은 그리지 않습니다.
                Vector3 tip=swordTip?swordTip.position:c,grip=swordBase?swordBase.position:c+Vector3.up*3;
                Vector3 axis=(grip-tip).normalized,side=Vector3.Cross(axis,Vector3.right).normalized;if(side.sqrMagnitude<.1f)side=Vector3.forward;
                Vector3 cross=Vector3.Cross(axis,side);
                for(int band=0;band<4;band++)for(int j=0;j<20;j++)
                {
                    float u=j/20f,v=(j+1)/20f,a=age*5+band*Mathf.PI*.5f+u*7,b=age*5+band*Mathf.PI*.5f+v*7;
                    Vector3 p=Vector3.Lerp(tip,grip,u)+(side*Mathf.Cos(a)+cross*Mathf.Sin(a))*(.2f+.2f*Mathf.Sin(u*Mathf.PI));
                    Vector3 q=Vector3.Lerp(tip,grip,v)+(side*Mathf.Cos(b)+cross*Mathf.Sin(b))*(.2f+.2f*Mathf.Sin(v*Mathf.PI));
                    Stroke(p,q,.075f,Ink(Color.Lerp(iceTint,Color.white,u),.7f));
                }
                for(int i=0;i<14;i++){float t=Mathf.Repeat(age*.5f+i*.071f,1);Vector3 p=Vector3.Lerp(tip,grip,t)+Radial(i*137+age*80,.45f);Shard(p,i*73,.14f,.06f,Ink(Color.white,.8f));}
                if(theme==Theme.WallEnclosure)DrawWallFootprints(age);
                Submit(areaMesh);return;
            }
            Boundary(c,accent);
            DrawWarningFill(c);
            switch(theme)
            {
                case Theme.BladeWave:
                    Arc(c,length,0,360,.15f,Ink(Color.white,.95f),.16f);Arc(c,length-.2f,0,360,.18f,Ink(iceTint,.85f));break;
                case Theme.ColdField:
                    if(active)DrawBlizzard(c,age);
                    else for(int i=0;i<24;i++){float a=i*137.5f,r=length*Mathf.Sqrt((i+1f)/24);Vector3 p=c+Radial(a+age*3,r);Snowflake(p+Vector3.up*.2f,.25f,Ink(Color.white,.4f));}
                    break;
                case Theme.Slash:
                    // 경고는 고정된 부채꼴입니다. 발동 시에만 실제 칼끝 이동을 바닥에 투영합니다.
                    if(active)DrawSlashSweep(c,accent);
                    break;
                case Theme.Crack:DrawCrack(c,accent,age);break;
                case Theme.Burst:DrawBurst(c,accent,age);break;
                case Theme.Stomp:DrawStomp(c,accent,age);break;
                case Theme.Fury:DrawAurora(c,width+.45f,accent,age);break;
                case Theme.Summon:
                    // 전환 자세를 가리지 않도록 무릎 높이 아래에서 냉기가 모입니다.
                    Snowflake(c,Mathf.Min(length*.42f,2.4f),Ink(Color.white,.55f));
                    for(int i=0;i<12;i++)
                    {
                        float angle=i*30;Vector3 p=c+Radial(angle,length*.82f);
                        Stroke(c+Radial(angle,length*.55f),p,.055f,Ink(iceTint,.65f));
                        Shard(p,angle,.28f+.12f*Mathf.Sin(age*2+i),.22f,Ink(iceTint,.7f));
                    }
                    for(int i=0;i<3;i++)Arc(c,length*(.6f+i*.12f),age*12+i*120,age*12+i*120+65,.045f,Ink(Color.white,.45f));
                    break;
                case Theme.Charge:case Theme.HorseCharge:case Theme.MultiCharge:case Theme.MountedFury:DrawCharge(c,accent,age);break;
                case Theme.IceTrail:
                    DrawCrack(c,accent,age);
                    for(int i=0;i<8;i++){float t=(i+.5f)/8;Vector3 p=c+direction*(length*t);Arc(p,Mathf.Min(width*.35f,.55f),age*25+i*45,age*25+i*45+220,.045f,Ink(accent,.35f));}
                    break;
                case Theme.SwordField:
                    for(int i=0;i<3;i++)Arc(c,length*(.35f+i*.27f),age*5+i*120,age*5+i*120+95,.035f,Ink(iceTint,.22f));
                    Snowflake(c,Mathf.Min(length*.28f,2.4f),Ink(Color.white,.45f));
                    break;
                case Theme.PlantedBlade:
                    Snowflake(c,length*.7f,Ink(Color.white,.7f));
                    Arc(c,length*.75f,age*20,age*20+120,.045f,Ink(iceTint,.55f));
                    break;
            }
            Submit(areaMesh);
        }
        void DrawWallFootprints(float age)
        {
            if(wallPoints==null)return;
            float progress=Mathf.Max(0,warningProgress),fade=active?1-Mathf.Clamp01(age/.7f):1;
            for(int i=0;i<wallPoints.Length;i++)
            {
                Vector3 c=wallPoints[i]+Vector3.up*floorOffset;
                Vector3 x=wallRotations[i]*Vector3.right*wallSize.x*.5f,z=wallRotations[i]*Vector3.forward*wallSize.z*.5f;
                // 생성될 벽의 실제 폭과 두께를 먼저 보여주어 발밑에서 갑자기 막히지 않습니다.
                var color=Ink(new Color(.55f,.88f,1),fade*(.12f+.2f*progress));
                Quad(c-x-z,c-x+z,c+x+z,c+x-z,color);
                var edge=Ink(Color.white,fade*.9f);
                Stroke(c-x-z,c-x+z,.14f,edge);Stroke(c-x+z,c+x+z,.14f,edge);
                Stroke(c+x+z,c+x-z,.14f,edge);Stroke(c+x-z,c-x-z,.14f,edge);
                Stroke(c-x,c-x+x*2*progress,.2f,Ink(iceTint,fade*.9f));
            }
        }
        void DrawBlizzard(Vector3 c,float age)
        {
            // 가을 보스의 입체 회오리와 같은 방식으로 냉기 띠와 눈 입자를 전장에 흘립니다.
            float radius=Mathf.Min(length,28);
            for(int band=0;band<7;band++)for(int j=0;j<40;j++)
            {
                float u=j/40f,v=(j+1)/40f,r=radius*(.2f+band*.135f),spin=age*65+band*61;
                Vector3 a=c+Radial(spin+u*270,r)+Vector3.up*(.3f+Mathf.Sin(u*Mathf.PI)*4);
                Vector3 b=c+Radial(spin+v*270,r)+Vector3.up*(.3f+Mathf.Sin(v*Mathf.PI)*4);
                SoftSurface(a,b,b+Vector3.up*.45f,a+Vector3.up*.45f,Ink(iceTint,.28f*Mathf.Sin(u*Mathf.PI)),4,band);
            }
            var camera=Camera.main;Vector3 right=camera?camera.transform.right:Vector3.right,up=camera?camera.transform.up:Vector3.up;
            for(int i=0;i<240;i++)
            {
                float r=radius*Mathf.Sqrt(Mathf.Repeat(i*.618f,1)),a=i*137.5f+age*(32+i%6*7);
                Vector3 p=c+Radial(a,r)+Vector3.up*(.25f+Mathf.Repeat(i*.37f-age*.24f,1)*6);
                float s=.09f+(i%4)*.04f;Vector3 x=right*s,y=up*s;
                Quad(p-x,p+y,p+x,p-y,Ink(Color.white,.82f));
                Vector3 trail=Radial(a+90,.35f)+Vector3.up*.08f;
                Quad(p-x*.25f,p+x*.25f,p+trail+x*.25f,p+trail-x*.25f,Ink(Color.white,.25f));
            }
        }
        void DrawSlashSweep(Vector3 center,Color accent)
        {
            Vector3 forward=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;bool previousValid=false;float previous=0;
            foreach(var tip in ribbonTips)
            {
                Vector3 delta=Vector3.ProjectOnPlane(tip-origin,Vector3.up);if(delta.sqrMagnitude<.25f){previousValid=false;continue;}
                float a=Vector3.SignedAngle(forward,delta,Vector3.up);
                if(Mathf.Abs(a)>width*.5f+12){previousValid=false;continue;}
                a=Mathf.Clamp(a,-width*.5f,width*.5f);
                if(previousValid&&Mathf.Abs(a-previous)<100)
                {
                    Arc(center+Vector3.up*.08f,length*.94f,previous,a,slashIndex==1?.26f:.18f,Ink(Color.white,.85f));
                    Arc(center+Vector3.up*.04f,length*.84f,previous,a,.09f,Ink(accent,.6f));
                }
                previous=a;previousValid=true;
            }
        }
        void DrawWarningFill(Vector3 center)
        {
            if(warningProgress<0)return;
            float p=warningProgress;if(p<=0)return;
            // 고정된 전체 경고 위에 밝은 얼음색을 덧씌웁니다. 안전한 안쪽 고리는 채우지 않습니다.
            Vector3 c=center+Vector3.up*.012f;Color fill=new Color(.55f,.88f,1,active?.18f:.55f);
            // 눈/얼음 바닥에서도 근접 폭발의 예고와 실제 발동을 구분합니다.
            if(theme==Theme.Burst&&!active)fill=new Color(1,.59f,.18f,.28f);
            if(shape==Shape.Line)
            {
                Vector3 side=Vector3.Cross(Vector3.up,direction).normalized*width*.5f,end=c+direction*(length*p);
                Quad(c-side,c+side,end+side,end-side,fill);Stroke(end-side,end+side,edgeWidth,Color.white);
            }
            else
            {
                float angle=shape==Shape.Sector?width:360,inner=shape==Shape.Ring?Mathf.Min(width,length):0;
                float radius=shape==Shape.Sector?length:Mathf.Lerp(inner,length,p);
                float from=-angle*.5f,to=shape==Shape.Sector?Mathf.Lerp(from,angle*.5f,p):angle*.5f;
                if(shape==Shape.Sector&&reverseWarning){from=angle*.5f;to=Mathf.Lerp(from,-angle*.5f,p);}
                for(int i=0;i<segments;i++)
                {
                    float a=Mathf.Lerp(from,to,i/(float)segments),b=Mathf.Lerp(from,to,(i+1f)/segments);
                    Quad(c+Radial(a,inner),c+Radial(a,radius),c+Radial(b,radius),c+Radial(b,inner),fill);
                }
                if(shape==Shape.Sector)Stroke(c,c+Radial(to,radius),edgeWidth,Color.white);
                else Arc(c,radius,0,360,edgeWidth,Color.white);
            }
        }
        void DrawCrack(Vector3 c,Color accent,float age)
        {
            Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;int count=Mathf.Clamp(Mathf.CeilToInt(length/1.1f),4,24);
            float front=active?Mathf.Clamp01(age*4):1;Vector3 prev=c;
            for(int i=1;i<=count;i++)
            {
                float t=i/(float)count;if(t>front)break;
                Vector3 p=c+direction*(length*t)+side*(Mathf.Sin(i*4.6f)*width*.22f);
                Stroke(prev,p,active?.19f:.065f,Ink(Color.Lerp(accent,Color.white,.5f),.9f));
                Vector3 branch=p+side*(i%2==0?1:-1)*width*.23f-direction*.3f;Stroke(p,branch,.045f,Ink(accent,.7f));
                if(active)Shard(p,i*137,crystalHeight*(.5f+.5f*Mathf.Sin(t*Mathf.PI)),crystalWidth*1.2f,Ink(accent,.85f));prev=p;
            }
        }
        void DrawBurst(Vector3 c,Color accent,float age)
        {
            if(!active)
            {
                float progress=Mathf.Clamp01(warningProgress);
                // 처음부터 전체 위험 반경을 고정하고, 안쪽 채움과 모이는 냉기로 남은 시간을 알립니다.
                Arc(c+Vector3.up*.025f,length,0,360,.18f,Ink(new Color(1,.61f,.18f),.85f+.12f*Mathf.Sin(age*8)));
                Snowflake(c+Vector3.up*.035f,1.25f,Ink(Color.white,.75f));
                for(int i=0;i<12;i++)
                {
                    float u=Mathf.Repeat(age*.7f+i/12f,1),r=Mathf.Lerp(length*.78f,1.1f,u);
                    Vector3 p=c+Radial(i*30+age*12,r)+Vector3.up*(.12f+u*.65f);
                    Stroke(p,p+Vector3.up*(.12f+.25f*progress),.045f,Ink(accent,Mathf.Sin(u*Mathf.PI)*.8f));
                }
            }
            // 꽂힌 검에서 빙판이 쪼개지고 넓은 갑옷형 얼음 판이 바깥으로 들립니다.
            for(int i=0;i<8;i++)
            {
                float a=i*45;Vector3 foot=c+Radial(a,length*.38f),tip=c+Radial(a+8,length*.86f);
                Stroke(c+Radial(a,.65f),foot,.075f,Ink(Color.white,.6f));Stroke(foot,tip,.04f,Ink(accent,.75f));
                if(active)
                {
                    float rise=burstHeight*Mathf.Sin(Mathf.Clamp01(age*2.1f)*Mathf.PI);
                    IcePlate(foot,Quaternion.LookRotation(Radial(a,1))*Quaternion.Euler(-28,0,(i%2==0?8:-8)),1.2f,rise*(.8f+i%3*.15f),.32f,.85f);
                    Vector3 chip=c+Radial(a+16,length*.67f)+Vector3.up*rise*.25f;
                    IcePlate(chip,Quaternion.Euler(age*160,a,35),.45f,rise*.4f,.18f,.65f);
                }
            }
            for(int j=0;j<3;j++)
            {
                float u=active?Mathf.Clamp01(age*2.7f-j*.12f):.55f+j*.15f;
                Arc(c+Vector3.up*(active?Mathf.Sin(u*Mathf.PI)*burstHeight*.6f:0),length*u,0,360,.12f,Ink(Color.Lerp(accent,Color.white,.5f),active?.8f*(1-u):.5f),active?.16f:0);
            }
        }
        void DrawStomp(Vector3 c,Color accent,float age)
        {
            // 발굽 아래의 굵은 방사형 균열과 낮게 퍼지는 세 번의 충격파입니다.
            for(int i=0;i<8;i++)
            {
                float a=i*45;Vector3 p=c+Radial(a,length*.45f),end=c+Radial(a+9,length*.92f);
                Stroke(c+Radial(a,.7f),p,active?.16f:.07f,Ink(accent,.75f));Stroke(p,end,.075f,Ink(accent,.65f));
                if(active)IcePlate(end,Quaternion.Euler(-45,a,i*17),.9f,crystalHeight*.45f*Mathf.Clamp01(age*8),.32f,.75f);
            }
            Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;
            Hoofprint(c+side*.55f,.42f,Ink(Color.white,.9f));Hoofprint(c-side*.55f,.42f,Ink(Color.white,.9f));
            for(int j=0;j<3;j++){float u=active?Mathf.Clamp01(age*3-j*.13f):.42f+j*.23f;Arc(c,length*u,0,360,.15f,Ink(Color.white,active?(1-u)*.7f:.35f),active?.18f:0);}
        }
        void DrawCharge(Vector3 c,Color accent,float age)
        {
            Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;int n=Mathf.Clamp(Mathf.CeilToInt(length/2),3,18);
            bool royal=theme==Theme.MountedFury,multi=theme==Theme.MultiCharge;
            if(royal||multi)
            {
                // 남은 돌진 수를 경로 입구의 얼음 문장으로 표시합니다.
                for(int j=chargeIndex;j<chargeCount;j++)
                {
                    Vector3 mark=c+direction*.65f+side*((j-(chargeCount-1)*.5f)*.55f);
                    Stroke(mark-direction*.25f-side*.18f,mark+direction*.2f,.1f,Ink(Color.white,.9f));
                    Stroke(mark+direction*.2f,mark-direction*.25f+side*.18f,.1f,Ink(Color.white,.9f));
                }
                if(royal&&!active)for(int j=0;j<4;j++)
                {
                    Vector3 p=c+direction*(length*(j+.5f)/4);
                    IcePlate(p-side*width*.45f,Quaternion.LookRotation(direction),.4f,.75f,.16f,.55f);
                    IcePlate(p+side*width*.45f,Quaternion.LookRotation(direction),.4f,.75f,.16f,.55f);
                }
            }
            for(int i=0;i<n;i++)
            {
                float u=(i+.5f)/n;Vector3 p=c+direction*(length*u);float half=width*.36f;
                float pulse=.45f+.35f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(age*9-i*.65f)),2);
                Hoofprint(p-side*half*.45f,Mathf.Min(.28f,half*.25f),Ink(Color.white,pulse));
                Hoofprint(p+side*half*.45f-direction*.35f,Mathf.Min(.28f,half*.25f),Ink(Color.white,pulse));
                if(multi){Stroke(p-side*half-direction*.6f,p-side*half+direction*.3f,.07f,Ink(accent,pulse));Stroke(p+side*half-direction*.6f,p+side*half+direction*.3f,.07f,Ink(accent,pulse));}
                else{Stroke(p-direction*.7f,p,.055f,Ink(accent,pulse));}
                if(royal&&active&&i%2==0){IcePlate(p-side*width*.42f,Quaternion.LookRotation(direction)*Quaternion.Euler(0,0,20),.45f,1.4f,.18f,.72f);IcePlate(p+side*width*.42f,Quaternion.LookRotation(direction)*Quaternion.Euler(0,0,-20),.45f,1.4f,.18f,.72f);}
            }
            if(active)for(int j=0;j<3;j++)
            {float u=Mathf.Repeat(age*1.6f+j*.33f,1);Vector3 p=c+direction*(length*u);Stroke(p-side*width*.42f,p+side*width*.42f,.16f,Ink(Color.white,.6f*(1-u)));}
        }
        void DrawChargeEmblem(Vector3 c,float age,bool royal)
        {
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.65f));
            Vector3 side=Vector3.Cross(Vector3.up,direction).normalized;
            if(royal)
            {
                // 기사 왕관을 닮은 다섯 장의 얼음날이 군마 뒤에서 솟아 특별한 돌격을 예고합니다.
                Vector3 back=c-direction*.8f+Vector3.up*(4+rise*.7f);
                for(int i=-2;i<=2;i++)IcePlate(back+side*i*1.05f,Quaternion.LookRotation(direction)*Quaternion.Euler(0,0,-i*12),.7f,(3-Mathf.Abs(i)*.6f)*rise,.25f,.8f);
                Arc(c,length,0,360,.1f,Ink(Color.white,.8f));
                Snowflake(c,length*.75f,Ink(iceTint,.7f));
                for(int i=0;i<6;i++){float a=i*60;Stroke(c+Radial(a,length*.7f),c+Radial(a,length*(1.2f-rise*.25f)),.08f,Ink(Color.white,.7f));}
            }
            else
            {
                for(int i=0;i<chargeCount;i++)
                {
                    Vector3 p=c+direction*(1+i*.8f);float w=.8f+i*.15f;
                    Stroke(p-side*w-direction*.4f,p+direction*.25f,.16f,Ink(Color.white,.65f+.3f*Mathf.Sin(age*8-i)));
                    Stroke(p+direction*.25f,p+side*w-direction*.4f,.16f,Ink(Color.white,.65f+.3f*Mathf.Sin(age*8-i)));
                }
            }
        }
        void DrawAurora(Vector3 c,float inner,Color accent,float age)
        {
            // 보스의 왕관과 대검 같은 얼음 성벽을 위험 경계 바깥에만 세웁니다.
            for(int band=0;band<2;band++)for(int i=0;i<segments;i++)
            {
                float a=i*360f/segments,b=(i+1)*360f/segments,r=Mathf.Min(length-.1f,inner+band*1.8f);
                float h=auroraHeight*(.65f+.25f*Mathf.Sin(a*Mathf.Deg2Rad*3+age*1.5f+band))*(active?1:.35f);
                float hb=auroraHeight*(.65f+.25f*Mathf.Sin(b*Mathf.Deg2Rad*3+age*1.5f+band))*(active?1:.35f);
                Vector3 pa=c+Radial(a,r),pb=c+Radial(b,r);Color color=Color.Lerp(iceTint,Color.white,.25f);
                Quad(pa,pb,pb+Vector3.up*hb,pa+Vector3.up*h,Ink(color,active?.075f:.035f));
            }
            for(int i=0;i<20;i++)
            {float a=i*18,r=Mathf.Min(length-.3f,inner+.8f);Vector3 p=c+Radial(a,r);Snowflake(p,.3f,Ink(Color.white,.6f));if(active)IcePlate(p,Quaternion.LookRotation(Radial(a,1))*Quaternion.Euler(-12,0,i%2==0?8:-8),.9f,crystalHeight*(i%2==0?1.8f:1.05f),.35f,.82f);}
            Arc(c,Mathf.Min(length,inner+.18f),0,360,.1f,Ink(Color.white,.8f));
        }
        void DrawBladeAura(Color accent,float age)
        {
            Vector3 forward=direction.normalized;Vector3 right=Vector3.Cross(forward,Vector3.up).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;
            Vector3 up=Vector3.Cross(right,forward).normalized;
            if(theme==Theme.BladeCharge)
            {
                for(int ring=0;ring<2;ring++)for(int i=0;i<48;i++)
                {
                    float a=(i*7.5f+age*(ring==0?80:-65))*Mathf.Deg2Rad,b=a+7.5f*Mathf.Deg2Rad;
                    float r=length*(ring==0?.8f:1.05f);Vector3 pa=right*Mathf.Cos(a)+up*Mathf.Sin(a),pb=right*Mathf.Cos(b)+up*Mathf.Sin(b);
                    if(i%6<4)Quad(origin+pa*r,origin+pb*r,origin+pb*(r-.045f),origin+pa*(r-.045f),Ink(ring==0?Color.white:iceTint,.8f));
                }
            }
            else
            {
                for(int i=0;i<3;i++)
                {float a=(age*180+i*120)*Mathf.Deg2Rad;Vector3 offset=(right*Mathf.Cos(a)+up*Mathf.Sin(a))*.2f;Vector3 tail=origin-forward*length*1.3f;Quad(origin+offset-right*.05f,origin+offset+right*.05f,tail+right*.015f,tail-right*.015f,Ink(i==0?Color.white:accent,.45f));}
            }
        }
    }
}
