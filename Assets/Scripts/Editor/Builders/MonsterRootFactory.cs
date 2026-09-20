using UnityEngine;
namespace DogLife.EditorTools
{
    public static class MonsterRootFactory
    {
        public static GameObject Create()
        {
            var root=new GameObject("Monster");
            var capsule=root.AddComponent<CapsuleCollider>();capsule.radius=.4f;capsule.height=2;
            root.AddComponent<Rigidbody>().isKinematic=true;
            root.AddComponent<CombatHealth>();
            return root;
        }
    }
}
