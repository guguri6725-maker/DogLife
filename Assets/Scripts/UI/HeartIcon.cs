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
            vh.AddVert(new Vector3(r.center.x,r.center.y,0),color,Vector2.zero);
            const int segments=64;
            for(int i=0;i<=segments;i++)
            {
                float t=i*Mathf.PI*2/segments;
                float x=16*Mathf.Pow(Mathf.Sin(t),3);
                float y=13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t);
                vh.AddVert(new Vector3(r.center.x+x/32*r.width,r.center.y+(y+2)/30*r.height,0),color,Vector2.zero);
                if(i>0)vh.AddTriangle(0,i,i+1);
            }
        }
    }
}
