using System.Collections;
using UnityEngine;
namespace DogLife
{
    // MonsterTest only. Remove this scene object to remove the test station.
    public sealed class RelicTestRewards : MonoBehaviour
    {
        IEnumerator Start()
        {
            if(gameObject.scene.name!="MonsterTest")yield break;
            PlayerMotor player=null;
            while(!player){player=FindFirstObjectByType<PlayerMotor>();yield return null;}
            yield return null;
            RelicShrine.Spawn(player.gameObject,player.transform.position+player.transform.right*2,true);
        }
    }
}
