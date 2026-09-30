using UnityEngine;
using UnityEngine.Rendering;
namespace DogLife
{
    // 미리 연결한 파편 메시를 재사용합니다. 파편마다 콜라이더/오브젝트를 만들지 않습니다.
    public sealed class FrozenIceBurst:MonoBehaviour
    {
        public enum Mode { Chip,Shatter,Absorb }
        [SerializeField] Mesh shard;
        [SerializeField] Material material;
        [SerializeField,Range(12,64)] int pieces=36;
        [SerializeField,Min(.1f)] float duration=1.25f,scatterSpeed=4.5f;
        MaterialPropertyBlock block;Mode mode;Vector3 extent;Transform receiver;float born;
        public void Play(Mode value,Vector3 size,Transform target){mode=value;extent=size*.5f;receiver=target;born=Time.time;block=new MaterialPropertyBlock();}
        void LateUpdate()
        {
            float t=(Time.time-born)/duration;if(t>=1){Destroy(gameObject);return;}if(!shard||!material||block==null)return;
            int count=mode==Mode.Chip?10:pieces;
            for(int i=0;i<count;i++)
            {
                float angle=i*137.508f*Mathf.Deg2Rad;Vector3 d=new Vector3(Mathf.Cos(angle),.25f+Mathf.Repeat(i*.37f,1),Mathf.Sin(angle));
                Vector3 p;float scale;
                if(mode==Mode.Absorb)
                {
                    Vector3 end=receiver?receiver.position+Vector3.up*.65f:transform.position;
                    float r=(1-t)*(1.3f+.8f*Mathf.Sin(t*Mathf.PI));
                    p=Vector3.Lerp(transform.position,end,t)+new Vector3(Mathf.Cos(angle+t*8)*r,Mathf.Sin(angle+t*6)*r,Mathf.Sin(angle+t*8)*r);
                    scale=.14f*(1-t)+.04f;
                }
                else
                {
                    float age=t*duration;Vector3 initial=Vector3.Scale(new Vector3(Mathf.Cos(angle)*.7f,Mathf.Repeat(i*.61f,2)-1,Mathf.Sin(angle)*.7f),extent);
                    p=transform.position+initial+d*scatterSpeed*age*(mode==Mode.Chip?.4f:1)+Vector3.down*4*age*age;
                    scale=(mode==Mode.Chip?.1f:.23f+Mathf.Repeat(i*.19f,.25f))*(1-t);
                }
                block.SetColor("_Tint",new Color(.45f+.4f*(i%2),.85f,1,Mathf.Clamp01((1-t)*2)));
                Graphics.DrawMesh(shard,Matrix4x4.TRS(p,Quaternion.Euler(i*37+t*210,i*71,t*180),new Vector3(scale,scale*2,scale)),material,gameObject.layer,null,0,block,ShadowCastingMode.Off,false);
            }
        }
    }
}
