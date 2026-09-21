using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    // A small leather satchel matching the ivory and antique gold HUD.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventoryBagGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var gold = new Color(.83f,.68f,.43f);
            Quad(vh,r,.30f,.70f,.40f,.23f,gold);
            Quad(vh,r,.36f,.70f,.28f,.16f,new Color(.10f,.09f,.08f));
            Quad(vh,r,.12f,.12f,.76f,.64f,gold);
            Quad(vh,r,.16f,.16f,.68f,.56f,new Color(.32f,.20f,.12f));
            Quad(vh,r,.12f,.49f,.76f,.27f,new Color(.53f,.36f,.20f));
            Quad(vh,r,.18f,.50f,.64f,.025f,gold);
            Quad(vh,r,.44f,.32f,.12f,.40f,new Color(.24f,.15f,.09f));
            Quad(vh,r,.40f,.40f,.20f,.17f,gold);
            Quad(vh,r,.445f,.445f,.11f,.08f,new Color(.26f,.17f,.10f));
        }
        static void Quad(VertexHelper vh, Rect r, float x,float y,float w,float h,Color c)
        {
            int n=vh.currentVertCount;
            vh.AddVert(new Vector3(r.x+x*r.width,r.y+y*r.height),c,Vector2.zero);
            vh.AddVert(new Vector3(r.x+x*r.width,r.y+(y+h)*r.height),c,Vector2.zero);
            vh.AddVert(new Vector3(r.x+(x+w)*r.width,r.y+(y+h)*r.height),c,Vector2.zero);
            vh.AddVert(new Vector3(r.x+(x+w)*r.width,r.y+y*r.height),c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
