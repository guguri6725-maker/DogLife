using UnityEngine;
namespace DogLife
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    // 카메라 기준 3D 이동, 점프, 웅크리기와 대시를 처리합니다.
    public sealed partial class PlayerMotor : MonoBehaviour
    {
        public float moveSpeed = 4, jumpSpeed = 8, dashSpeed = 12;
        public float dashDuration = .22f, dashCooldown = 5;
        // 이동 시간과 독립적으로 대시 시작부터 피해를 막는 시간을 설정합니다.
        [Min(0), Tooltip("대시 시작부터 무적이 유지되는 시간(초). 0이면 대시 무적을 사용하지 않습니다.")]
        public float dashInvulnerabilityDuration = .22f;
        // 상승 중 점프 키를 놓으면 남은 상승 속도를 줄입니다. 작을수록 짧은 점프가 낮아집니다.
        [Range(.1f, 1f)] public float jumpReleaseMultiplier = .45f;
        public LayerMask groundMask = 1 << 8;
        public Transform visualRoot, movementCamera;
        public float visualYawOffset = -90;
        public bool readInput = true;
        [System.NonSerialized] public bool directionalLocomotion;
        public Vector2 MoveInput { get; set; }
        public bool Grounded { get; private set; }
        public bool Crouching { get; private set; }
        public bool IsDashing => Time.time < dashUntil;
        // 짧은 대쉬도 튜토리얼에서 프레임 간 누락 없이 확인합니다.
        public event System.Action Dashed;
        public bool IsDashInvulnerable => isActiveAndEnabled && Time.time < dashInvulnerableUntil;
        public float DashRemaining => Mathf.Max(0, nextDash - Time.time);
        public Vector3 Facing { get; private set; } = Vector3.right;
        public Vector3 MoveDirection {
            get {
                var f = movementCamera ? Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized : Vector3.forward;
                return Vector3.ClampMagnitude(f * MoveInput.y + Vector3.Cross(Vector3.up, f) * MoveInput.x, 1);
            }
        }
        Rigidbody body;
        PlayerWaterMovement waterMovement;
        BasicWeaponCombat attackCombo;
        // 적의 공격 감지 상태와 플레이어의 자세 잠금 조건을 분리합니다.
        public bool AttackLocked => attackCombo && attackCombo.IsAttacking && !attackCombo.IsCharging;
        bool AttackPostureLocked => attackCombo && attackCombo.LocksPosture;
        public bool AttackMovementLocked => attackCombo && attackCombo.LocksMovement;
        CapsuleCollider capsule;
        const float GroundNormalY = .55f;
        readonly System.Collections.Generic.List<Vector3> wallNormals = new System.Collections.Generic.List<Vector3>();
        float dashUntil, nextDash, jumpBuffer, lastGrounded = -10, standingHeight;
        float dashInvulnerableUntil;
        bool jumpHeld, jumping, jumpReleased, suppressCrouchUntilRelease;
        Vector3 knockbackVelocity;
        float knockbackUntil, knockbackDuration;
        public bool IsKnockedBack => Time.time < knockbackUntil;
        float movementSlowUntil,movementSlowMultiplier=1;
        public float MovementSlowMultiplier=>Time.time<movementSlowUntil?movementSlowMultiplier:1;
        public void ApplyMovementSlow(float multiplier,float duration)
        {
            // 얼음검은 일반 이동만 감속합니다. 대시 속도/시간/쿨타임은 변경하지 않습니다.
            movementSlowMultiplier=Mathf.Min(MovementSlowMultiplier,Mathf.Clamp(multiplier,.1f,1));
            movementSlowUntil=Mathf.Max(movementSlowUntil,Time.time+Mathf.Max(0,duration));
        }
        public void ApplyKnockback(Vector3 direction,float speed,float lift,float duration)
        {
            CancelMeleeApproach();
            if(!isActiveAndEnabled||body.isKinematic)return;
            direction=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            knockbackVelocity=direction*Mathf.Max(0,speed);knockbackDuration=Mathf.Max(.02f,duration);
            knockbackUntil=Time.time+knockbackDuration;dashUntil=0;ClearJumpInput();
            var v=body.linearVelocity;v.y=Mathf.Max(v.y,lift);body.linearVelocity=v;
        }
        Vector3 dashDirection, spawn, standingCenter, visualScale, visualPosition;
        void Awake() {
            attackCombo = GetComponent<BasicWeaponCombat>();
            body = GetComponent<Rigidbody>(); capsule = GetComponent<CapsuleCollider>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if(!GetComponent<PlayerMovementAudio>())gameObject.AddComponent<PlayerMovementAudio>();
            spawn = transform.position; standingHeight = capsule.height; standingCenter = capsule.center;
            if (visualRoot) { visualScale = visualRoot.localScale; visualPosition = visualRoot.localPosition; }
        }
        void Update() {
            if (!waterMovement) waterMovement = GetComponent<PlayerWaterMovement>();
            if (SkillWindow.BlocksGameplayInput) { MoveInput = Vector2.zero; ReleaseJump(); return; }
            if (!readInput) return;
            if (Cursor.lockState != CursorLockMode.Locked) { MoveInput = Vector2.zero; ReleaseJump(); return; }
            MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            // 도끼처럼 이동이 잠긴 공격도 뒤로 입력하면 접근 보정부터 취소합니다.
            if(approachCollider&&Vector3.Dot(MoveDirection,approachDirection)<-.05f)CancelMeleeApproach();
            bool crouchHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            if (!crouchHeld) suppressCrouchUntilRelease = false;
            // Shift와 마우스 우클릭은 같은 대시 조건과 쿨타임을 사용합니다.
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetMouseButtonDown(1)) RequestDash();
            if (AttackMovementLocked) { MoveInput = Vector2.zero; ClearJumpInput(); return; }
            SetCrouching(crouchHeld && !suppressCrouchUntilRelease);
            if (waterMovement && waterMovement.Submerged) waterMovement.AscendHeld = Input.GetKey(KeyCode.Space);
            else if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (!Input.GetKey(KeyCode.Space)) ReleaseJump();
        }
        public void RequestJump() {
            if (waterMovement && waterMovement.Submerged) { waterMovement.AscendHeld = true; return; }
            if (IsKnockedBack || AttackPostureLocked || !StandForAction()) return;
            CancelMeleeApproach();
            jumpHeld = true; jumpBuffer = Time.time + .12f;
        }
        // 물리 프레임 전에 짧게 눌렀다 놓아도 점프 예약은 유지합니다.
        public void ReleaseJump() { jumpHeld = false; if(waterMovement)waterMovement.AscendHeld=false; }
        public bool RequestDash() {
            var health=GetComponent<CombatHealth>();
            if (!isActiveAndEnabled || SkillWindow.BlocksGameplayInput || (health&&health.Dead) || IsKnockedBack) return false;
            // 대시는 현재 자세를 유지합니다. 앉기 키를 떼기 전까지 낮은 몸통과 자세로 이동합니다.
            if (Time.time < nextDash) return false;
            // 대시가 실제 발동할 때 타격 슬로만 해제합니다. 재발동 시각은 유지합니다.
            CombatFeedback.CancelGlobalHitSlow();
            CancelMeleeApproach();
            dashDirection = MoveDirection.sqrMagnitude > .01f ? MoveDirection.normalized : Facing;
            if(attackCombo)attackCombo.CancelForDash();
            var feedback=GetComponent<CombatFeedback>();if(feedback)feedback.CancelHitStop();
            GameAudio.Play("Roll",transform.position,.75f);
            dashUntil = Time.time + dashDuration;
            dashInvulnerableUntil = Time.time + Mathf.Max(0, dashInvulnerabilityDuration);
            nextDash = Time.time + dashCooldown; Dashed?.Invoke(); return true;
        }
        bool StandForAction()
        {
            // 머리 위 공간을 먼저 확인하고, 앉기 키를 계속 눌러도 바로 다시 앉지 않게 합니다.
            if (Crouching && !SetCrouching(false)) return false;
            suppressCrouchUntilRelease = true;
            return true;
        }
        public void Face(Vector3 direction) {
            if (attackCombo&&attackCombo.LocksFacing) return;
            FaceForAttack(direction);
        }
        // 타격 시점에는 전투 코드가 판정과 동일한 방향으로 맞춥니다.
        internal void FaceForAttack(Vector3 direction) {
            direction.y = 0; if (direction.sqrMagnitude < .001f) return;
            Facing = direction.normalized;
            // 활 조준 보정은 몸 전체에 적용하여 상체와 골반이 과하게 비틀리지 않게 합니다.
            if (visualRoot) visualRoot.rotation = Quaternion.LookRotation(Facing) * Quaternion.Euler(0, visualYawOffset + (attackCombo ? attackCombo.VisualYawOffset : 0), 0);
        }
        public bool SetCrouching(bool value) {
            // 공격 중 자세 잠금은 유지하고, 대시로 공격을 취소한 뒤에는 앉기 입력을 그대로 받습니다.
            if (AttackPostureLocked) return false;
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
        bool IsGroundCollider(Collider candidate) {
            // Default 레이어의 구조물도 받치지만 자신과 전투 캐릭터는 발판으로 사용하지 않습니다.
            return candidate && candidate.attachedRigidbody != body &&
                !candidate.transform.IsChildOf(transform) && !candidate.GetComponentInParent<CombatHealth>() &&
                !Physics.GetIgnoreLayerCollision(gameObject.layer, candidate.gameObject.layer) &&
                !Physics.GetIgnoreCollision(capsule, candidate);
        }
        bool CheckGround(Vector3 origin) {
            if (body.linearVelocity.y > .5f) return false;
            // 가까운 옆면이 먼저 검출되어도 그 아래의 평평한 지지면을 계속 확인합니다.
            foreach (var hit in Physics.SphereCastAll(origin, capsule.radius * .85f, Vector3.down,
                .22f, groundMask.value | 1, QueryTriggerInteraction.Ignore))
                if (hit.normal.y > GroundNormalY && IsGroundCollider(hit.collider)) return true;
            return false;
        }
        void FixedUpdate() {
            // 캡슐 하단에서 지면을 검사하고 수직 벽은 바닥에서 제외합니다.
            var b = capsule.bounds;
            var origin = new Vector3(b.center.x, b.min.y + capsule.radius + .08f, b.center.z);
            Grounded = CheckGround(origin);
            if (Grounded) { lastGrounded = Time.time; jumping = false; }
            var horizontal = IsDashing ? dashDirection * dashSpeed : MoveDirection * moveSpeed * (Crouching ? .5f : 1);
            if(!IsDashing){horizontal*=MovementSlowMultiplier;if(TryGetComponent<RelicCombatEffects>(out var relicEffects))horizontal*=relicEffects.MovementMultiplier;}
            if (TryGetComponent<BubbleDebuff>(out var bubbleDebuff))
                horizontal *= IsDashing ? bubbleDebuff.RollMultiplier : bubbleDebuff.MoveMultiplier;
            if(Grounded&&!IsDashing&&!IsKnockedBack&&!AttackMovementLocked)
                horizontal=WinterIcePatch.ApplySurface(new Vector3(b.center.x,b.min.y,b.center.z),body.linearVelocity,horizontal,Time.fixedDeltaTime);
            if (AttackMovementLocked) horizontal = Vector3.zero;
            if(IsKnockedBack)horizontal=knockbackVelocity*Mathf.Clamp01((knockbackUntil-Time.time)/knockbackDuration);
            horizontal=ResolveMeleeApproach(horizontal);
            horizontal = LimitWallMovement(horizontal);
            var velocity = new Vector3(horizontal.x, body.linearVelocity.y, horizontal.z);
            if (!(waterMovement && waterMovement.Submerged) && !AttackPostureLocked && !Crouching && jumpBuffer > Time.time && Time.time - lastGrounded < .1f) {
                GameAudio.Play("Jump",transform.position,.55f);
                velocity.y = jumpSpeed; jumpBuffer = 0; lastGrounded = -10; Grounded = false;
                jumping = true; jumpReleased = false;
            }
            // 직접 시작한 점프의 상승 중에 한 번만 감속합니다. 재입력으로 공중에서 높이를 늘릴 수 없습니다.
            if (jumping && !jumpHeld && !jumpReleased) {
                if (velocity.y > 0) velocity.y *= Mathf.Clamp(jumpReleaseMultiplier, .1f, 1f);
                jumpReleased = true;
            }
            if (velocity.y <= 0) jumping = false;
            if(waterMovement && waterMovement.Submerged) velocity=waterMovement.ResolveVelocity(velocity);
            body.linearVelocity = velocity;
            // 방향별 걷기는 조준 방향을 유지해야 옆걸음과 뒷걸음이 선택됩니다.
            if (horizontal.sqrMagnitude > .01f && !directionalLocomotion) Face(horizontal);
            if (body.position.y < -12) Respawn();
        }
        public void Respawn() { CancelMeleeApproach();if(waterMovement)waterMovement.Leave(); dashInvulnerableUntil=0;movementSlowUntil=0;knockbackUntil=0;wallNormals.Clear(); ClearJumpInput(); body.position = spawn; body.linearVelocity = Vector3.zero; }
        public void SetRespawnPoint(Vector3 point) { spawn = point; }
        public void StopForAttack()
        {
            dashUntil = 0; MoveInput = Vector2.zero; ClearJumpInput();
            if (body && !body.isKinematic) body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
        }
        void ClearJumpInput() { jumpHeld = jumping = jumpReleased = false; jumpBuffer = 0; lastGrounded = -10; }
        void OnDisable() { CancelMeleeApproach();dashInvulnerableUntil=0;movementSlowUntil=0;knockbackUntil=0;wallNormals.Clear(); MoveInput = Vector2.zero; ClearJumpInput(); }
    }
}

