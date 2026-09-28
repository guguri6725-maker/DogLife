using UnityEngine;
namespace DogLife
{
 public sealed class KrakenAttackTrigger:MonoBehaviour
 {
  public KrakenTentacle tentacle;public KrakenWave wave;
  void OnTriggerEnter(Collider other){if(tentacle)tentacle.Touch(other);if(wave)wave.Touch(other);}
  void OnTriggerStay(Collider other){if(tentacle)tentacle.Touch(other);if(wave)wave.Touch(other);}
 }
}
