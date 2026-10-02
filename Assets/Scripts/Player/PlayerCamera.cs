using UnityEngine;
namespace DogLife
{
    // 마우스로 회전하는 3인칭 카메라. 지형에 닿으면 가까이 당깁니다.
    public sealed class PlayerCamera : MonoBehaviour
    {
        public Transform target;
        public LayerMask obstructionMask = 1 << 8;
        public float distance = 5.5f, height = 1f, sensitivity = 2f;
        public float shoulderOffset = .55f;
        public float yaw = 90, pitch = 18;
        public bool readInput = true;
        Vector3 cinematicFrom;Quaternion cinematicRotation;float cinematicReturnStart,cinematicReturnDuration,cinematicFov,gameplayFov;
        Camera ownCamera, collisionCamera;
        [Header("카메라 충돌")]
        [SerializeField, Min(.01f)] float collisionPadding = .08f;
        [SerializeField, Min(.05f)] float minimumCollisionRadius = .22f;
        [SerializeField, Min(.01f)] float returnSmoothTime = .28f;
        [SerializeField, Min(0)] float obstructionHoldTime = .18f;
        float armLength, armVelocity, releaseAt, shoulderLength, shoulderVelocity;
        bool armInitialized;
        readonly RaycastHit[] obstructionHits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[64];
        Vector3 lastClearPosition;
        bool hasClearPosition;

        // 화면 가까운 면의 모서리까지 포함해 가장자리로 벽 밖이 보이지 않게 합니다.
        float CollisionRadius()
        {
            if (!collisionCamera) collisionCamera = GetComponent<Camera>();
            if (!collisionCamera) return minimumCollisionRadius;
            float near = collisionCamera.nearClipPlane;
            float h = collisionCamera.orthographic ? collisionCamera.orthographicSize : near * Mathf.Tan(collisionCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            float w = h * collisionCamera.aspect;
            return Mathf.Max(minimumCollisionRadius, Mathf.Sqrt(near * near + h * h + w * w)) + collisionPadding;
        }
        bool BlocksCamera(Collider c)
        {
            if (!c || c.transform.IsChildOf(target) || c.transform.IsChildOf(transform)) return false;
            var health = c.GetComponentInParent<CombatHealth>();
            if (health && health.player) return false;
            // 몸체 Trigger만 포함하고 공격 영역, 센서, 포자 장식은 제외합니다.
            if (c.isTrigger) return c.GetComponent<MonsterHurtbox>();
            return (obstructionMask.value & (1 << c.gameObject.layer)) != 0 || c.gameObject.layer == 0 || health;
        }
        bool IsClear(Vector3 point, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(point, radius, overlaps, ~0, QueryTriggerInteraction.Collide);
            var found = n == overlaps.Length ? Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Collide) : overlaps;
            if (found != overlaps) n = found.Length;
            for (int i = 0; i < n; i++) if (BlocksCamera(found[i])) return false;
            return true;
        }
        Vector3 ClipSegment(Vector3 from, Vector3 to, float radius)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < .0001f) return from;
            Vector3 direction = delta / length;
            int n = Physics.SphereCastNonAlloc(from, radius, direction, obstructionHits, length, ~0, QueryTriggerInteraction.Collide);
            var hits = n == obstructionHits.Length ? Physics.SphereCastAll(from, radius, direction, length, ~0, QueryTriggerInteraction.Collide) : obstructionHits;
            if (hits != obstructionHits) n = hits.Length;
            for (int i = 0; i < n; i++)
                if (hits[i].distance > .0001f && BlocksCamera(hits[i].collider)) length = Mathf.Min(length, Mathf.Max(0, hits[i].distance - collisionPadding));
            // 시작 구가 벽과 겹칠 때 놓치는 얇은 벽도 선 검사로 막습니다.
            n = Physics.RaycastNonAlloc(from, direction, obstructionHits, delta.magnitude, ~0, QueryTriggerInteraction.Collide);
            hits = n == obstructionHits.Length ? Physics.RaycastAll(from, direction, delta.magnitude, ~0, QueryTriggerInteraction.Collide) : obstructionHits;
            if (hits != obstructionHits) n = hits.Length;
            for (int i = 0; i < n; i++)
                if (BlocksCamera(hits[i].collider)) length = Mathf.Min(length, Mathf.Max(0, hits[i].distance - radius));
            return from + direction * length;
        }
        Vector3 ClearPosition(Vector3 anchor, Vector3 wanted, float radius)
        {
            wanted.x = Mathf.Max(wanted.x, minimumWorldX);
            var clipped = ClipSegment(anchor, wanted, radius);
            if (clipped.x >= minimumWorldX && IsClear(clipped, radius)) return clipped;
            // 플레이어가 몬스터와 겹쳐도 카메라를 몸 안에 두지 않도록 빈 위치를 찾습니다.
            for (int i = 1; i <= 24; i++)
            {
                var candidate = Vector3.Lerp(clipped, anchor, i / 24f);
                if (candidate.x >= minimumWorldX && IsClear(candidate, radius)) return candidate;
            }
            // 빈 방향을 매 프레임 다시 고르면 좌우/위로 순간이동합니다. 막힌 동안 마지막 안전 시점을 유지합니다.
            if (hasClearPosition && lastClearPosition.x >= minimumWorldX && IsClear(lastClearPosition, radius)) return lastClearPosition;
            return clipped;
        }
        // 연출 마지막 화면에서 평소 추적 카메라로 부드럽게 복귀합니다.
        public void BlendFromCinematic(float duration,float originalFov)
        {cinematicFrom=transform.position;cinematicRotation=transform.rotation;cinematicReturnStart=Time.unscaledTime;cinematicReturnDuration=duration;ownCamera=GetComponent<Camera>();cinematicFov=ownCamera?ownCamera.fieldOfView:originalFov;gameplayFov=originalFov;}
        // 지나온 방을 해제한 뒤 카메라가 닫힌 문 뒤로 넘어가지 않게 합니다.
        [System.NonSerialized] public float minimumWorldX = float.NegativeInfinity;
        void Start() { CaptureCursor(true); }
        public void CaptureCursor(bool capture) {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !capture;
        }
        void Update() {
            if (SkillWindow.BlocksGameplayInput) return;
            if (!readInput) return;
            if (Input.GetKeyDown(KeyCode.Escape)) CaptureCursor(Cursor.lockState != CursorLockMode.Locked);
            if (Cursor.lockState != CursorLockMode.Locked) return;
            yaw += Input.GetAxis("Mouse X") * sensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * sensitivity, -25, 65);
        }
        void LateUpdate() {
            if (!target) return;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            float blend=cinematicReturnDuration>0?Mathf.SmoothStep(0,1,(Time.unscaledTime-cinematicReturnStart)/cinematicReturnDuration):1;
            if(ownCamera){ownCamera.fieldOfView=Mathf.Lerp(cinematicFov,gameplayFov,blend);if(blend>=1)ownCamera=null;}
            float radius = CollisionRadius();
            var anchor = target.position + Vector3.up * height;
            // 어깨 오프셋 자체가 벽을 넘지 않도록 먼저 검사합니다.
            var shoulder = rotation * Vector3.right * Mathf.Sign(shoulderOffset);
            float allowedShoulder = Vector3.Distance(anchor, ClipSegment(anchor, anchor + shoulder * Mathf.Abs(shoulderOffset), radius));
            if (!armInitialized) shoulderLength = allowedShoulder;
            if (allowedShoulder < shoulderLength) { shoulderLength = allowedShoulder; shoulderVelocity = 0; }
            else shoulderLength = Mathf.SmoothDamp(shoulderLength, allowedShoulder, ref shoulderVelocity, returnSmoothTime);
            var pivot = anchor + shoulder * shoulderLength;
            var back = rotation * Vector3.back;
            var limited = ClipSegment(pivot, pivot + back * distance, radius);
            float allowedLength = Vector3.Distance(pivot, limited);
            if (!armInitialized) { armLength = allowedLength; armInitialized = true; }
            // 장애물 쪽으로는 즉시 당기고, 경계가 잠깐 풀려도 곧바로 줌아웃하지 않습니다.
            if (allowedLength < armLength) { armLength = allowedLength; armVelocity = 0; releaseAt = Time.time + obstructionHoldTime; }
            else if (Time.time >= releaseAt) armLength = Mathf.Min(allowedLength, Mathf.SmoothDamp(armLength, allowedLength, ref armVelocity, returnSmoothTime));
            var position = ClearPosition(anchor, pivot + back * armLength, radius);
            // 연출 복귀 보간과 방 경계 보정 뒤의 최종 위치도 다시 검사합니다.
            position = ClearPosition(anchor, Vector3.Lerp(cinematicFrom, position, blend), radius);
            // 이동과 시점 회전은 즉시 반영하고, 충돌로 줄어든 거리의 복귀만 위에서 보간합니다.
            transform.SetPositionAndRotation(position,Quaternion.Slerp(cinematicRotation,rotation,blend));
            if (IsClear(position, radius)) { lastClearPosition = position; hasClearPosition = true; }
        }
        void OnDisable() { armInitialized = false; hasClearPosition = false; armVelocity = shoulderVelocity = 0; CaptureCursor(false); }
    }
}
