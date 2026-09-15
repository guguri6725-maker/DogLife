using System.Collections;
using UnityEngine;
namespace DogLife
{
    // Temporary Play Mode integration probe. Does not alter saved scenes or prefabs.
    public sealed class StageVerification : MonoBehaviour
    {
        public string Result { get; private set; } = "RUNNING";
        public int Passed { get; private set; }
        void Check(bool condition, string message)
        {
            if (!condition) { Result = "FAIL: " + message; throw new System.Exception(Result); }
            Passed++; Debug.Log("STAGE VERIFY PASS: " + message);
        }
        IEnumerator Start()
        {
            var director = FindFirstObjectByType<StageOneDirector>();
            var motor = director.hud.motor; var weapons = director.hud.weapons; var playerHealth = director.hud.health; var body = motor.GetComponent<Rigidbody>();
            motor.readInput = false; weapons.readInput = false;
            yield return new WaitForSeconds(.3f);
            Check(director.encounters.Length == 7, "Six encounters and one boss room");
            Check(director.encounters[0].activeSelf && !director.encounters[1].activeSelf, "Only first encounter active");
            Check(director.gates[0].activeSelf, "Exit gate locked while enemies live");
            // Verify contact with the closed gate using the actual motor.
            body.position = new Vector3(20.7f, 1.05f, 0); body.linearVelocity = Vector3.zero; motor.MoveInput = 1;
            yield return new WaitForSeconds(.7f); motor.MoveInput = 0;
            Check(body.position.x < 21.6f, "Closed gate blocks player physics");
            body.position = new Vector3(0, 1.05f, 0); body.linearVelocity = Vector3.zero;
            playerHealth.Restore();
            float normalHP = playerHealth.maxHealth;
            // Isolate progression from balance: a large test-only health pool allows deterministic combat.
            playerHealth.maxHealth = 10000; playerHealth.Restore();
            weapons.Equip(0);
            for (int room = 0; room < 7; room++)
            {
                float deadline = Time.time + 45;
                var enemies = director.encounters[room].GetComponentsInChildren<CombatHealth>(true);
                while (director.Cleared == room && Time.time < deadline)
                {
                    CombatHealth nearest = null;
                    float best = float.MaxValue;
                    foreach (var enemy in enemies)
                    {
                        if (!enemy || enemy.Dead) continue;
                        float distance = Vector3.Distance(motor.transform.position, enemy.transform.position);
                        if (distance < best) { best = distance; nearest = enemy; }
                    }
                    if (nearest)
                    {
                        motor.MoveInput = best > 5 ? Mathf.Sign(nearest.transform.position.x - motor.transform.position.x) : 0;
                        weapons.AimAt(nearest.transform.position); weapons.TryAttack();
                    }
                    yield return null;
                }
                motor.MoveInput = 0;
                Check(director.Cleared == room + 1, "Real weapon combat clears room " + (room + 1));
                Check(!director.gates[room].activeSelf, "Room gate opens " + (room + 1));
                if (room < 6)
                {
                    Check(director.encounters[room + 1].activeSelf, "Next encounter activates " + (room + 2));
                    float moveDeadline = Time.time + 10;
                    while (motor.transform.position.x < (room + 1) * 23 && Time.time < moveDeadline) { motor.MoveInput = 1; yield return null; }
                    motor.MoveInput = 0;
                    Check(motor.transform.position.x >= (room + 1) * 23, "Walks through opened gate " + (room + 1));
                }
            }
            Check(!director.Complete, "Boss defeat still requires reaching exit");
            float exitDeadline = Time.time + 15;
            while (!director.Complete && Time.time < exitDeadline) { motor.MoveInput = 1; yield return null; }
            Check(director.Complete, "Exit completes only stage 1");
            Check(!motor.enabled && !weapons.enabled, "Completion stops combat controls");
            playerHealth.maxHealth = normalHP; playerHealth.Restore();
            Result = "PASS: " + Passed + " stage checks"; Debug.Log(Result);
        }
    }
}
