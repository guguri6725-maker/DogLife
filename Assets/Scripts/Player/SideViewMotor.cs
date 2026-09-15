using UnityEngine;

namespace DogLife
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    // 플레이어의 좌우 이동, 점프, 대시와 리스폰을 담당합니다.
    public sealed class SideViewMotor : MonoBehaviour
    {
        public float moveSpeed = 4f, jumpSpeed = 8f, dashSpeed = 12f;
        public float dashDuration = .22f, dashCooldown = 5f;
        public LayerMask groundMask = 1 << 8;
        public Transform visualRoot;
        // 2D풍 모델은 좌우 반전해 방향을 바꿔도 얼굴이 카메라에 보이도록 합니다.
        public bool mirrorVisualFacing;
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
        // 키보드 입력을 읽어 이동 요청으로 변환합니다.
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
            if (visualRoot)
            {
                visualRoot.localRotation = Quaternion.Euler(0, mirrorVisualFacing || Facing > 0 ? 0 : 180, 0);
                if (mirrorVisualFacing)
                {
                    var scale = visualRoot.localScale;
                    scale.x = Mathf.Abs(scale.x) * Facing;
                    visualRoot.localScale = scale;
                }
            }
        }
        // 물리 프레임마다 지면 판정과 실제 속도를 적용합니다.
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
        // 추락한 플레이어를 시작 위치로 되돌립니다.
        public void Respawn()
        {
            body.position = spawn;
            body.linearVelocity = Vector3.zero;
        }
    }
}
