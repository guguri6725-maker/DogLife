using UnityEngine;
namespace DogLife
{
    public sealed class HarvestPumpkinVisual : MonoBehaviour
    {
        public Renderer[] embers;
        MaterialPropertyBlock block;
        public void Show(Vector3 point,Quaternion rotation,float scale,float progress)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(point,rotation);
            transform.localScale=Vector3.one*scale*(1+.025f*Mathf.Sin(progress*18)*progress);
            if(block==null)block=new MaterialPropertyBlock();
            float energy=Mathf.Lerp(.6f,3.8f,progress*progress);
            block.SetColor("_EmissionColor",new Color(1,.31f,.035f)*energy);
            foreach(var ember in embers)if(ember)ember.SetPropertyBlock(block);
        }
    }
}
