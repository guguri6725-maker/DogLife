#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace DogLife
{
    public sealed class SpringProjectileRegression:MonoBehaviour
    {
        readonly List<GameObject> objects=new List<GameObject>();
        readonly List<string> report=new List<string>();int bursts;
        void Heard(string key){if(key=="Blossom_Burst")bursts++;}
        void Check(bool value,string label)=>report.Add((value?"PASS ":"FAIL ")+label);
        GameObject Cube(float x){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.position=new Vector3(1000+x,100,1000);objects.Add(go);return go;}
        CombatProjectile Shot(float start)
        {
            var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/monster/BlossomPetalProjectile.prefab"),new Vector3(1000+start,100,1000),Quaternion.identity);objects.Add(go);
            var shot=go.GetComponent<CombatProjectile>();shot.hostile=true;shot.direction=Vector3.right;shot.speed=10;shot.range=20;shot.damage=1;shot.impactSound="Blossom_Burst";return shot;
        }
        IEnumerator Start()
        {
            GameAudio.EffectStarted+=Heard;
            try{
                var enemy=Cube(2).AddComponent<CombatHealth>();var shot=Shot(0);Physics.SyncTransforms();
                yield return new WaitForSeconds(.5f);
                Check(shot&&shot.transform.position.x>1003,"Blossom projectile passes through another monster");
                Check(enemy.Current==enemy.maxHealth&&bursts==0,"Passing a monster causes no damage or explosion sound");
                Cube(8);Physics.SyncTransforms();yield return new WaitForSeconds(.6f);
                Check(!shot&&bursts==1,"Wall collision still consumes projectile and explodes once");
                var victim=Cube(24).AddComponent<CombatHealth>();victim.player=true;shot=Shot(20);Physics.SyncTransforms();yield return new WaitForSeconds(.6f);
                Check(!shot&&victim.Current==victim.maxHealth-1&&bursts==2,"Player collision still damages and consumes projectile once");
                var other=Cube(42).AddComponent<CombatHealth>();shot=Shot(40);shot.ignoreFriendlyMonsters=false;Physics.SyncTransforms();yield return new WaitForSeconds(.4f);
                Check(!shot,"Opt-out leaves unrelated projectile collision behavior unchanged");
            }finally{
                GameAudio.EffectStarted-=Heard;foreach(var go in objects)if(go)Destroy(go);
                System.IO.Directory.CreateDirectory("Builds/AudioValidation");System.IO.File.WriteAllLines("Builds/AudioValidation/spring-projectile-regression.txt",report);
                Debug.Log(string.Join("\n",report));Destroy(gameObject);
            }
        }
    }
}
#endif
