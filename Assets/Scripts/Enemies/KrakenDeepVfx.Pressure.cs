using UnityEngine;
namespace DogLife
{
    public sealed partial class KrakenDeepVfx
    {
        public static float PressureFront(float progress)=>1-Mathf.Pow(1-Mathf.Clamp01(progress),2);
        void DrawPressureBurst(float q)
        {
            float r=Mathf.Max(.01f,Size.x*PressureFront(q));
            // One expanding pressure shell, with a luminous foam crest rather than repeated rings.
            for(int lat=0;lat<16;lat++)for(int j=0;j<64;j++)
            {
                float a=j*Mathf.PI/32,b=(j+1)*Mathf.PI/32;
                float p=-Mathf.PI*.5f+lat*Mathf.PI/16,p1=p+Mathf.PI/16;
                float ripple=1+.012f*Mathf.Sin(a*13+q*17)*Mathf.Sin(p*6);
                Quad(Radial(a,r*Mathf.Cos(p),r*Mathf.Sin(p))*ripple,
                    Radial(b,r*Mathf.Cos(p),r*Mathf.Sin(p)),
                    Radial(b,r*Mathf.Cos(p1),r*Mathf.Sin(p1)),
                    Radial(a,r*Mathf.Cos(p1),r*Mathf.Sin(p1))*ripple,
                    new Color(.12f,.68f,.75f,.24f),9);
            }
            Ring(Vector3.zero,r,Mathf.Min(r*.25f,.32f),Quaternion.identity,new Color(.76f,1,.97f,.95f));
            for(int strand=0;strand<18;strand++)for(int j=0;j<12;j++)
            {
                float seed=Mathf.Repeat(strand*.618034f,1),a=strand*2.39996f;
                float p=Mathf.Lerp(-1.15f,1.15f,seed)+j*.025f;
                float p1=p+.025f,b=a+.04f;
                var from=Radial(a,r*Mathf.Cos(p),r*Mathf.Sin(p));
                var to=Radial(b,r*Mathf.Cos(p1),r*Mathf.Sin(p1));
                Ribbon(from,to,.09f+.1f*seed,new Color(.64f,.98f,.96f,.8f));
            }
            for(int i=0;i<96;i++)
            {
                float y=1-2*(i+.5f)/96,flat=Mathf.Sqrt(1-y*y),a=i*2.399963f;
                var dir=new Vector3(Mathf.Cos(a)*flat,y,Mathf.Sin(a)*flat);
                float seed=Mathf.Repeat(i*.754877f,1);
                var tip=dir*r;Ribbon(dir*r*(.84f+.1f*seed),tip,.055f+.07f*seed,Alpha(Pearl,.75f));
                Bubble(tip,.045f+.07f*seed,.8f);
            }
            if(q<.2f)Bubble(Vector3.zero,Size.x*.12f*(1-q/.2f),.8f*(1-q/.2f));
        }
    }
}
