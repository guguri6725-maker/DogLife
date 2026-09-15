using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class SideViewMotor : MonoBehaviour
    {
        public float moveSpeed = 4f, jumpSpeed = 8f, dashSpeed = 12f;
        public float dashDuration = .22f, dashCooldown = 5f;
        public LayerMask groundMask = 1 << 8;
        public Transform visualRoot;
        public bool readInput = true;
        public float MoveInput { get; set; }
        public bool Grounded { get; private set; }
        public bool IsDashing => Time.time < dashUntil;
        public float DashRemaining => Mathf.Max(0, nextDash - Time.time);
        public int Facing { get; private set; } = 1;
        Rigidbody body;
        float dashUntil, nextDash, jumpBuffer, lastGrounded = -10;
        int dashDirection;
        Vector3 spawn;
        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            spawn = transform.position;
        }
        void Update()
        {
            if (!readInput) return;
            MoveInput = Input.GetAxisRaw("Horizontal");
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (Input.GetKeyDown(KeyCode.LeftShift)) RequestDash();
        }
        public void RequestJump() { jumpBuffer = Time.time + .12f; }
        public bool RequestDash()
        {
            if (Time.time < nextDash) return false;
            dashDirection = Mathf.Abs(MoveInput) > .1f ? (int)Mathf.Sign(MoveInput) : Facing;
            dashUntil = Time.time + dashDuration;
            nextDash = Time.time + dashCooldown;
            return true;
        }
        public void Face(float direction)
        {
            if (Mathf.Abs(direction) < .01f) return;
            Facing = direction > 0 ? 1 : -1;
            if (visualRoot) visualRoot.localRotation = Quaternion.Euler(0, Facing > 0 ? 0 : 180, 0);
        }
        void FixedUpdate()
        {
            Grounded = Physics.CheckSphere(body.position + Vector3.down * .88f, .18f, groundMask, QueryTriggerInteraction.Ignore) && body.linearVelocity.y <= .2f;
            if (Grounded) lastGrounded = Time.time;
            var velocity = body.linearVelocity;
            velocity.x = IsDashing ? dashDirection * dashSpeed : Mathf.Clamp(MoveInput, -1, 1) * moveSpeed;
            if (jumpBuffer > Time.time && Time.time - lastGrounded < .1f)
            {
                velocity.y = jumpSpeed;
                jumpBuffer = 0;
                lastGrounded = -10;
                Grounded = false;
            }
            velocity.z = 0;
            body.linearVelocity = velocity;
            if (body.position.y < -12) Respawn();
        }
        public void Respawn()
        {
            body.position = spawn;
            body.linearVelocity = Vector3.zero;
        }
    }
}
