using UnityEngine;

namespace DogLife
{
    // 앞뒤 두 겹의 꽃잎이 구형 꽃봉오리를 감싸며 회전합니다.
    public sealed class BlossomProjectileVfx : MonoBehaviour
    {
        Transform[] petals;
        void Awake()
        {
            var found=new System.Collections.Generic.List<Transform>();
            foreach(Transform child in transform)if(child.name.StartsWith("PetalCore"))found.Add(child);
            petals=found.ToArray();
        }
        void LateUpdate()
        {
            for(int i=0;i<petals.Length;i++)
            {
                int ring=i/5;
                float angle=Time.time*55+(i%5)*72+ring*36;
                float radians=angle*Mathf.Deg2Rad;
                Vector3 normal=new Vector3(Mathf.Cos(radians)*.82f,Mathf.Sin(radians)*.82f,ring==0?.57f:-.57f).normalized;
                Vector3 tangent=new Vector3(-Mathf.Sin(radians),Mathf.Cos(radians),0);
                Quaternion rotation=Quaternion.LookRotation(normal,tangent)*Quaternion.Euler(0,0,12*Mathf.Sin(Time.time*2+i));
                petals[i].localRotation=rotation;
                // 꽃잎 중심을 구 표면에 맞춰 앞뒤로 입체감을 유지합니다.
                petals[i].localPosition=normal*.36f-rotation*(Vector3.up*petals[i].localScale.y*.45f);
            }
        }
    }
}

