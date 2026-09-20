using UnityEngine;
namespace DogLife
{
    // Render a reduced world pass before the main camera's overlay UI.
    [RequireComponent(typeof(Camera))]
    public sealed class WorldRenderScale : MonoBehaviour
    {
        public static float Scale = 1f;
        Camera view, world;
        RenderTexture buffer;
        int originalMask;
        bool scaledFrame;
        void Awake() { view=GetComponent<Camera>(); }
        void OnPreCull()
        {
            scaledFrame=false;
            if(Scale>=.999f||view.targetTexture){Release();return;}
            int width=Mathf.Max(1,Mathf.RoundToInt(Screen.width*Scale));
            int height=Mathf.Max(1,Mathf.RoundToInt(Screen.height*Scale));
            if(!buffer||buffer.width!=width||buffer.height!=height)
            {
                Release();buffer=new RenderTexture(width,height,24,RenderTextureFormat.Default){name="Scaled world",filterMode=FilterMode.Bilinear};buffer.Create();
            }
            if(!world)
            {
                var go=new GameObject("Scaled world camera",typeof(Camera));go.transform.SetParent(transform,false);
                world=go.GetComponent<Camera>();world.enabled=false;
            }
            world.CopyFrom(view);world.enabled=false;world.targetTexture=buffer;
            world.transform.SetPositionAndRotation(view.transform.position,view.transform.rotation);
            originalMask=view.cullingMask;
            world.Render();
            view.cullingMask=0;scaledFrame=true;
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {Graphics.Blit(scaledFrame&&buffer?buffer:source,destination);}
        void OnPostRender(){if(scaledFrame&&view)view.cullingMask=originalMask;}
        void RestoreMask(){if(scaledFrame&&view)view.cullingMask=originalMask;scaledFrame=false;}
        void Release()
        {
            if(world)world.targetTexture=null;
            if(buffer){buffer.Release();Destroy(buffer);buffer=null;}
        }
        void OnDisable(){RestoreMask();Release();}
        void OnDestroy(){RestoreMask();Release();if(world)Destroy(world.gameObject);}
    }
}

