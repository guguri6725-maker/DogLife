using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    // 폰트/텍스처에 의존하지 않는 Hierarchy 편집용 하트 그래픽.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HeartIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r=GetPixelAdjustedRect();
            const int segments=64;
            for(int layer=0;layer<3;layer++)
            {
            float scale=layer==0?1:layer==1?.88f:.73f;
            Color tint=layer==0?new Color(.28f,.23f,.18f,1):layer==1?new Color(.88f,.81f,.65f,1):color;
            int offset=vh.currentVertCount;
            vh.AddVert(new Vector3(r.center.x,r.center.y,0),tint,Vector2.zero);
            for(int i=0;i<=segments;i++)
            {
                float t=i*Mathf.PI*2/segments;
                float x=16*Mathf.Pow(Mathf.Sin(t),3);
                float y=13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t);
                Color shade=layer==2?Color.Lerp(tint*.62f,tint,Mathf.Clamp01((y+17)/30)):tint;shade.a=tint.a;
                vh.AddVert(new Vector3(r.center.x+x/32*r.width*scale,r.center.y+(y+2)/30*r.height*scale,0),shade,Vector2.zero);
                if(i>0)vh.AddTriangle(offset,offset+i,offset+i+1);
            }
            }
        }
    }
}
