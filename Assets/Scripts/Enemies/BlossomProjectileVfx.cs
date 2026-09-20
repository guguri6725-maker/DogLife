using UnityEngine;

namespace DogLife
{
    // 피해 판정은 그대로 두고, 꽃잎 탄두의 다섯 장을 회전시킵니다.
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
                float angle=Time.time*9+i*Mathf.PI*2/Mathf.Max(1,petals.Length);
                petals[i].localPosition=new Vector3(Mathf.Cos(angle)*.065f,Mathf.Sin(angle)*.065f,0);
                petals[i].Rotate(35*Time.deltaTime,75*Time.deltaTime,180*Time.deltaTime,Space.Self);
            }
        }
    }
}
