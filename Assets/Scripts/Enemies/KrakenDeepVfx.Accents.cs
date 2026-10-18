using UnityEngine;
namespace DogLife
{
    public sealed partial class KrakenDeepVfx
    {
        // Fine currents and foam stay inside the announced damage volume.
        // These are visual layers only; no colliders or lights are spawned.
        void DrawWaterAccents(float q,float time)
        {
            if(Shape==Form.Sand||Shape==Form.PressureBurst||Intensity<=.001f)return;
            float strength=Impact?1:.22f;
            if(Shape==Form.Band)
            {
                var h=Size*.5f;
                for(int wave=0;wave<3;wave++)
                {
                    float t=Mathf.Repeat(time*(Impact?.65f:.12f)+wave/3f,1);
                    float z=Mathf.Lerp(-h.z,h.z,t);
                    for(int j=0;j<48;j++)
                    {
                        float x=Mathf.Lerp(-h.x,h.x,j/48f),xx=Mathf.Lerp(-h.x,h.x,(j+1)/48f);
                        float y=Mathf.Sin(x*.7f+time*2.4f+wave)*h.y*.65f;
                        float yy=Mathf.Sin(xx*.7f+time*2.4f+wave)*h.y*.65f;
                        var a=new Vector3(x,y,z);var b=new Vector3(xx,yy,z);
                        float fade=Mathf.Sin(t*Mathf.PI)*strength;
                        Quad(a-Vector3.up*h.y*.28f,b-Vector3.up*h.y*.28f,b,a,new Color(.08f,.5f,.56f,fade*.22f),9);
                        Ribbon(a,b,.065f,Alpha(Pearl,fade*.68f));
                    }
                }
            }
            else if(Shape==Form.Beam)
            {
                float radius=Size.x*.5f;
                for(int strand=0;strand<7;strand++)for(int j=0;j<28;j++)
                {
                    float t=j/28f,u=(j+1)/28f;
                    float a=strand*Mathf.PI*2/7+t*9-time*(Impact?9:1.4f),b=strand*Mathf.PI*2/7+u*9-time*(Impact?9:1.4f);
                    float r=radius*(.65f+.2f*Mathf.Sin(time*3+strand));
                    var p=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Size.z*t);
                    var n=new Vector3(Mathf.Cos(b)*r,Mathf.Sin(b)*r,Size.z*u);
                    Ribbon(p,n,Impact?.075f:.025f,Alpha(Pearl,Mathf.Sin(t*Mathf.PI)*strength*.55f));
                }
            }
            else if(Shape==Form.Sector&&Impact)
            {
                float span=Size.z*Mathf.Deg2Rad,head=-span*.5f+span*q;
                for(int layer=0;layer<3;layer++)for(int j=0;j<56;j++)
                {
                    float a=Mathf.Max(-span*.5f,head-.48f+j*.48f/56),b=Mathf.Max(-span*.5f,head-.48f+(j+1)*.48f/56);
                    float r=Size.x*(.46f+layer*.24f),fade=(j+1)/56f;
                    float y=Mathf.Sin(a*7+time*4+layer)*Size.y*.22f;
                    Ribbon(Radial(a,r,y),Radial(b,r,y),.12f,Alpha(Pearl,fade*.65f));
                }
            }
            // Buoyant microbubbles give the pressure volume depth without obscuring safe lanes.
            int count=Impact?64:18;
            for(int i=0;i<count;i++)
            {
                float cycle=Mathf.Repeat(time*(Impact?.55f:.16f)+i*.618034f,1);
                float seed=Mathf.Repeat(i*.754877f,1),angle=i*2.399963f+time*.25f;
                Vector3 p;
                if(Shape==Form.Band)p=new Vector3((seed-.5f)*Size.x,(cycle-.5f)*Size.y,(Mathf.Repeat(i*.56984f,1)-.5f)*Size.z);
                else if(Shape==Form.Beam)p=new Vector3(Mathf.Cos(angle)*Size.x*.42f,Mathf.Sin(angle)*Size.x*.42f,cycle*Size.z);
                else if(Shape==Form.Sector){float a=(q-.5f)*Size.z*Mathf.Deg2Rad;p=Radial(a,Size.x*Mathf.Sqrt(seed),(cycle-.5f)*Size.y);}
                else p=Radial(angle,Size.x*Mathf.Sqrt(seed)*.95f,(cycle-.5f)*Mathf.Max(.3f,Size.y));
                Bubble(p,.025f+(i%4)*.013f,Mathf.Sin(cycle*Mathf.PI)*strength*.55f);
            }
        }
    }
}
