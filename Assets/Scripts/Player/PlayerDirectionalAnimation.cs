using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(PlayerMotor), typeof(Rigidbody))]
    public sealed class PlayerDirectionalAnimation : MonoBehaviour
    {
        public Animator animator;
        public float blendDamping = .1f;
        PlayerMotor motor;
        Rigidbody body;
        static readonly int X = Animator.StringToHash("MoveX");
        static readonly int Y = Animator.StringToHash("MoveY");
        void Awake()
        {
            motor = GetComponent<PlayerMotor>(); body = GetComponent<Rigidbody>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (animator) animator.applyRootMotion = false;
        }
        void OnEnable() { GetComponent<PlayerMotor>().directionalLocomotion = true; }
        void OnDisable()
        {
            GetComponent<PlayerMotor>().directionalLocomotion = false;
            if (animator && animator.isInitialized) { animator.SetFloat(X, 0); animator.SetFloat(Y, 0); }
        }
        void Update()
        {
            if (!animator || !animator.isActiveAndEnabled) return;
            var velocity = motor.enabled ? Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up) : Vector3.zero;
            var heading = Vector3.ProjectOnPlane(motor.Facing, Vector3.up);
            if (heading.sqrMagnitude < .001f) heading = Vector3.forward;
            var local = Quaternion.Inverse(Quaternion.LookRotation(heading)) * velocity;
            var blend = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / Mathf.Max(.1f, motor.moveSpeed), 1);
            if (velocity.sqrMagnitude < .01f) blend = Vector2.zero;
            animator.SetFloat(X, blend.x, blendDamping, Time.deltaTime);
            animator.SetFloat(Y, blend.y, blendDamping, Time.deltaTime);
        }
    }
}
