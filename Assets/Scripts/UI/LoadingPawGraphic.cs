using UnityEngine;
using UnityEngine.UI;
namespace DogLife
{
    // Small vector paw: no texture imports or extra material loads during transitions.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingPawGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Oval(vh,new Vector2(0,-7),new Vector2(10,8));
            Oval(vh,new Vector2(-12,3),new Vector2(4,6));
            Oval(vh,new Vector2(-5,12),new Vector2(4,6));
            Oval(vh,new Vector2(5,12),new Vector2(4,6));
            Oval(vh,new Vector2(12,3),new Vector2(4,6));
        }
        void Oval(VertexHelper vh,Vector2 center,Vector2 radius)
        {
            int start=vh.currentVertCount;vh.AddVert(center,color,Vector2.zero);
            const int segments=24;
            for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;vh.AddVert(center+new Vector2(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),color,Vector2.zero);}
            for(int i=0;i<segments;i++)vh.AddTriangle(start,start+1+(i+1)%segments,start+1+i);
        }
    }
}
