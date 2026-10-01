using UnityEngine;
namespace DogLife
{
    public sealed class EquippedBowString:MonoBehaviour
    {
        public float Draw;
        public float size=1;
        [SerializeField] LineRenderer line;
        public void Initialize(Material material,float scale)
        {
            size=scale;var go=new GameObject("Equipped bowstring");go.transform.SetParent(transform,false);
            line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=3;line.sharedMaterial=material;
            line.startWidth=line.endWidth=.018f*size;line.startColor=line.endColor=new Color(.94f,.89f,.74f,1);
            Update();
        }
        void Update()
        {
            if(!line)return;
            // 양 끝과 당기는 지점, 세 점으로 프리팹 활시위도 갱신합니다.
            line.positionCount=3;
            line.SetPositions(new[]{new Vector3(.12f,.43f,0)*size,new Vector3(.12f+.26f*Draw,0,0)*size,new Vector3(.12f,-.43f,0)*size});
            Draw=Mathf.MoveTowards(Draw,0,Time.deltaTime*4);
        }
    }
    public static class WeaponEquipmentVisuals
    {
        public static void Decorate(GameObject visual,InventoryWeapon item)
        {
            // 합성 모델은 손잡이와 활시위를 프리팹에서 완성해 두었습니다.
            if(item.weaponForm==InventoryWeapon.WeaponForm.Synthesized)return;
            float size=item.boneOrigin==InventoryWeapon.BoneOrigin.Humanoid?1.4f:1;
            if(item.weaponCategory==InventoryWeapon.WeaponCategory.Bow)
            {
                var effect=Resources.Load<Material>("BoneEquipmentLine");
                if(effect&&!visual.GetComponent<EquippedBowString>())
                {
                    var turn=Quaternion.Euler(0,0,90);
                    foreach(Transform child in visual.transform){child.localPosition=turn*child.localPosition;child.localRotation=turn*child.localRotation;}
                    visual.AddComponent<EquippedBowString>().Initialize(effect,size);
                }
            }
            if(item.weaponCategory!=InventoryWeapon.WeaponCategory.Axe||visual.transform.Find("AxeHandle"))return;
            // The source asset remains a bare scapula; fittings exist only on equipped instances.
            foreach(Transform child in visual.transform)child.localPosition+=Vector3.right*.25f*size;
            var handle=GameObject.CreatePrimitive(PrimitiveType.Cylinder);handle.name="AxeHandle";
            Object.Destroy(handle.GetComponent<Collider>());handle.transform.SetParent(visual.transform,false);
            handle.transform.localPosition=new Vector3(-.15f,0,0)*size;handle.transform.localRotation=Quaternion.Euler(0,0,90);
            handle.transform.localScale=new Vector3(.075f,.58f,.075f)*size;
            handle.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("BoneAxeHandle");
        }
    }
}
