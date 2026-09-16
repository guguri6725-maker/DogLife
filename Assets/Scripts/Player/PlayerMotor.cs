using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    // 카메라 기준 3D 이동, 점프, 웅크리기와 대시를 처리합니다.
    public sealed class PlayerMotor : MonoBehaviour
    {
        public float moveSpeed = 4, jumpSpeed = 8, dashSpeed = 12;
        public float dashDuration = .22f, dashCooldown = 5;
        public LayerMask groundMask = 1 << 8;
        public Transform visualRoot, movementCamera;
        public float visualYawOffset = -90;
        public bool readInput = true;
        public Vector2 MoveInput { get; set; }
        public bool Grounded { get; private set; }
        public bool Crouching { get; private set; }
        public bool IsDashing => Time.time < dashUntil;
        public float DashRemaining => Mathf.Max(0, nextDash - Time.time);
        public Vector3 Facing { get; private set; } = Vector3.right;
        public Vector3 MoveDirection {
            get {
                var f = movementCamera ? Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized : Vector3.forward;
                return Vector3.ClampMagnitude(f * MoveInput.y + Vector3.Cross(Vector3.up, f) * MoveInput.x, 1);
            }
        }
        Rigidbody body;
        CapsuleCollider capsule;
        const float GroundNormalY = .55f;
        readonly System.Collections.Generic.List<Vector3> wallNormals = new System.Collections.Generic.List<Vector3>();
        float dashUntil, nextDash, jumpBuffer, lastGrounded = -10, standingHeight;
        Vector3 dashDirection, spawn, standingCenter, visualScale, visualPosition;
        void Awake() {
            body = GetComponent<Rigidbody>(); capsule = GetComponent<CapsuleCollider>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            spawn = transform.position; standingHeight = capsule.height; standingCenter = capsule.center;
            if (visualRoot) { visualScale = visualRoot.localScale; visualPosition = visualRoot.localPosition; }
        }
        void Update() {
            if (SkillWindow.BlocksGameplayInput) { MoveInput = Vector2.zero; return; }
            if (!readInput) return;
            if (Cursor.lockState != CursorLockMode.Locked) { MoveInput = Vector2.zero; return; }
            MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            SetCrouching(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (Input.GetKeyDown(KeyCode.LeftShift)) RequestDash();
        }
        public void RequestJump() { if (!Crouching) jumpBuffer = Time.time + .12f; }
        public bool RequestDash() {
            if (Time.time < nextDash || Crouching) return false;
            dashDirection = MoveDirection.sqrMagnitude > .01f ? MoveDirection.normalized : Facing;
            dashUntil = Time.time + dashDuration; nextDash = Time.time + dashCooldown; return true;
        }
        public void Face(Vector3 direction) {
            direction.y = 0; if (direction.sqrMagnitude < .001f) return;
            Facing = direction.normalized;
            if (visualRoot) visualRoot.rotation = Quaternion.LookRotation(Facing) * Quaternion.Euler(0, visualYawOffset, 0);
        }
        public bool SetCrouching(bool value) {
            if (value == Crouching) return true;
            // 머리 위가 막혀 있으면 일어나지 않습니다.
            if (!value) {
                var top = transform.TransformPoint(standingCenter + Vector3.up * (standingHeight / 2 - capsule.radius));
                if (Physics.CheckCapsule(top - Vector3.up * standingHeight * .4f, top, capsule.radius * .9f, groundMask, QueryTriggerInteraction.Ignore)) return false;
            }
            Crouching = value; capsule.height = standingHeight * (value ? .6f : 1);
            capsule.center = standingCenter - Vector3.up * (standingHeight - capsule.height) / 2;
            if (visualRoot) {
                visualRoot.localScale = Vector3.Scale(visualScale, new Vector3(1, value ? .65f : 1, 1));
                visualRoot.localPosition = visualPosition - Vector3.up * (value ? standingHeight * .16f : 0);
            }
            return true;
        }
        // 벽의 수평 방향만 사용하여 이동이 위쪽 속도로 바뀌지 않도록 합니다.
        void RememberWall(Vector3 normal) {
            if (Mathf.Abs(normal.y) >= GroundNormalY) return;
            normal.y = 0;
            if (normal.sqrMagnitude > .001f) wallNormals.Add(normal.normalized);
        }
        void OnCollisionEnter(Collision collision) { RememberContacts(collision); }
        void OnCollisionStay(Collision collision) { RememberContacts(collision); }
        void RememberContacts(Collision collision) {
            // 이미 벽에 붙어 있을 때도 다음 물리 프레임에서 밀어붙이지 않습니다.
            for (int i = 0; i < collision.contactCount; i++) RememberWall(collision.GetContact(i).normal);
        }
        Vector3 LimitWallMovement(Vector3 horizontal) {
            // 실제 몸통으로 이동 구간을 검사하므로 웅크리기와 대시에도 대응합니다.
            for (int pass = 0; pass < 3 && horizontal.sqrMagnitude > .0001f; pass++) {
                var hits = body.SweepTestAll(horizontal.normalized,
                    horizontal.magnitude * Time.fixedDeltaTime + .02f, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits) {
                    if (!hit.collider || hit.collider.attachedRigidbody == body ||
                        Physics.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer) ||
                        Physics.GetIgnoreCollision(capsule, hit.collider)) continue;
                    RememberWall(hit.normal);
                }
                var before = horizontal;
                foreach (var normal in wallNormals) {
                    float intoWall = Vector3.Dot(horizontal, normal);
                    if (intoWall < 0) horizontal -= normal * intoWall;
                }
                if ((before - horizontal).sqrMagnitude < .000001f) break;
            }
            // 여러 벽이 만나는 모서리에서는 다른 벽 안쪽으로 다시 밀리지 않게 멈춥니다.
            foreach (var normal in wallNormals)
                if (Vector3.Dot(horizontal, normal) < -.001f) { horizontal = Vector3.zero; break; }
            wallNormals.Clear();
            return horizontal;
        }
        void FixedUpdate() {
            // 캡슐 하단에서 지면을 검사하고 수직 벽은 바닥에서 제외합니다.
            var b = capsule.bounds;
            var origin = new Vector3(b.center.x, b.min.y + capsule.radius + .08f, b.center.z);
            Grounded = Physics.SphereCast(origin, capsule.radius * .85f, Vector3.down, out var hit, .22f, groundMask, QueryTriggerInteraction.Ignore) && hit.normal.y > GroundNormalY && body.linearVelocity.y <= .5f;
            if (Grounded) lastGrounded = Time.time;
            var horizontal = IsDashing ? dashDirection * dashSpeed : MoveDirection * moveSpeed * (Crouching ? .5f : 1);
            horizontal = LimitWallMovement(horizontal);
            var velocity = new Vector3(horizontal.x, body.linearVelocity.y, horizontal.z);
            if (jumpBuffer > Time.time && Time.time - lastGrounded < .1f) {
                velocity.y = jumpSpeed; jumpBuffer = 0; lastGrounded = -10; Grounded = false;
            }
            body.linearVelocity = velocity;
            if (horizontal.sqrMagnitude > .01f) Face(horizontal);
            if (body.position.y < -12) Respawn();
        }
        public void Respawn() { wallNormals.Clear(); body.position = spawn; body.linearVelocity = Vector3.zero; }
        void OnDisable() { wallNormals.Clear(); MoveInput = Vector2.zero; }
    }
}
