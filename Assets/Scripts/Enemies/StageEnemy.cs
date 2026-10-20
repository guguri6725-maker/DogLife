using UnityEngine;
using UnityEngine.AI;
namespace DogLife
{
    [RequireComponent(typeof(Rigidbody), typeof(CombatHealth))]
    // 플레이어를 감지해 추적하고 근접 또는 원거리 공격을 수행합니다.
    public sealed class StageEnemy : MonoBehaviour
    {
        public bool ranged, boss;
        public float speed = 2, damage = 5, detectionRange = 13;
        public Transform visualRoot;
        // Inspector에서 연결한 모델만 Idle / Walk / Attack 애니메이션을 사용합니다.
        public Animator modelAnimator;
        public Material projectileMaterial;
        public GameObject projectilePrefab;
        public LayerMask obstacleMask = 1 << 8;
        public float visualYawOffset = -90;
        public bool Windup => windupUntil > Time.time;
        Transform target;
        CombatHealth health;
        Rigidbody body;
        NavMeshAgent agent;
        float nextPathUpdate;
        bool attackAnimationRequested;
        static readonly int MovingParameter = Animator.StringToHash("Moving");
        static readonly int AttackingParameter = Animator.StringToHash("Attacking");
        // 고정 시간이 아니라 Animator의 실제 재생 상태로 이동과 다음 공격을 잠급니다.
        bool AttackAnimationBusy => modelAnimator && modelAnimator.isActiveAndEnabled &&
            (attackAnimationRequested || modelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Attack") ||
             (modelAnimator.IsInTransition(0) && modelAnimator.GetNextAnimatorStateInfo(0).IsName("Attack")));
        float attackAt, windupUntil;
        Vector3 lockedAim;
        Renderer[] healthBarRenderers;
        Vector3 visualScale;
        ForestRoom combatRoom;
        void Start()
        {
            combatRoom=GetComponentInParent<ForestRoom>();
            var player = FindFirstObjectByType<PlayerMotor>(); target = player ? player.transform : null;
            body = GetComponent<Rigidbody>(); health = GetComponent<CombatHealth>();
            agent = GetComponent<NavMeshAgent>();
            // 모델의 실제 크기로 체력바 위치를 계산하기 위해 외형만 보관합니다.
            healthBarRenderers = (visualRoot ? visualRoot : transform).GetComponentsInChildren<Renderer>(true);
            body.constraints = RigidbodyConstraints.FreezeRotation;
            if (visualRoot) visualScale = visualRoot.localScale;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (target) Physics.IgnoreCollision(GetComponent<Collider>(), target.GetComponent<Collider>());
            attackAt = Time.time + 1;
        }
        // 물리 프레임마다 이동, 공격 예고와 실제 공격을 처리합니다.
        void FixedUpdate()
        {
            // 로딩된 다음 방에서는 대기하고, 문 닫힘 시작 후에만 AI를 실행합니다.
            if(combatRoom&&!combatRoom.CombatStarted)return;
            if (!target || health.Dead || target.GetComponent<CombatHealth>().Dead) { StopNavigation(); return; }
            var delta = target.position - transform.position;
            float distance = delta.magnitude;
            float range = boss ? 15 : ranged ? 9 : 1.7f;
            bool clearSight = !Physics.Raycast(transform.position, delta.normalized, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            float stopDistance = boss ? 7 : ranged ? 6 : 1.3f;
            // 사거리 안이어도 벽에 가려져 있으면 멈추지 않고 돌아가는 길을 찾습니다.
            bool walking = (combatRoom || distance < detectionRange) && (distance > stopDistance || !clearSight) && !Windup && !AttackAnimationBusy;
            var heading = Vector3.ProjectOnPlane(delta, Vector3.up).normalized;
            if (agent && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = !walking;
                agent.speed = speed;
                agent.stoppingDistance = clearSight ? stopDistance : .1f;
                // 경로 갱신을 분산하고 플레이어 발밑의 걸을 수 있는 지점을 목적지로 사용합니다.
                if (walking && Time.time >= nextPathUpdate)
                {
                    nextPathUpdate = Time.time + .25f;
                    var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                    if (NavMesh.SamplePosition(target.position - Vector3.up, out var destination, 3f, filter))
                        agent.SetDestination(destination.position);
                    else agent.ResetPath();
                }
                if (walking && agent.desiredVelocity.sqrMagnitude > .001f) heading = agent.desiredVelocity.normalized;
            }
            if (visualRoot)
            {
                // 공격 예고부터 타격 및 Attack 종료 전환까지 시작 방향을 유지합니다.
                if (windupUntil <= 0 && !AttackAnimationBusy && heading.sqrMagnitude > .001f)
                    visualRoot.rotation = Quaternion.LookRotation(heading) * Quaternion.Euler(0, visualYawOffset, 0);
                // 애니메이션 모델에는 기존 임시 외형의 흔들기 효과를 중복 적용하지 않습니다.
                visualRoot.localScale = visualScale * (Windup && !modelAnimator ? 1 + .08f * Mathf.Sin(Time.time * 35) : 1);
            }
            if (windupUntil > 0 && Time.time >= windupUntil)
            {
                windupUntil = 0; attackAt = Time.time + (boss ? 2.2f : 1.4f);
                if (boss)
                {
                    for (int i = 0; i < 24; i++) Shoot(Quaternion.Euler(0, i * 15, 0) * Vector3.right, 5.5f);
                }
                else if (ranged) Shoot(lockedAim, 7);
                else if (distance < 2 && clearSight) target.GetComponent<CombatHealth>().Damage(damage);
            }
            if (distance <= range && Time.time >= attackAt && windupUntil == 0 && !AttackAnimationBusy)
            {
                if (!clearSight) return;
                lockedAim = delta.normalized; windupUntil = Time.time + (boss ? .9f : .55f);
                if (modelAnimator && modelAnimator.isActiveAndEnabled) attackAnimationRequested = true;
                StopNavigation();
            }
        }
        // 공격 예고, 사망, 비활성화 중에는 남아 있는 경로로 움직이지 않습니다.
        void StopNavigation()
        {
            if (agent && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
        }
        void Update()
        {
            if (!modelAnimator || !modelAnimator.isActiveAndEnabled) return;
            // 이동 명령이 있어도 실제로 막혀 있으면 Idle을 재생합니다.
            bool alive = health && !health.Dead && target && !target.GetComponent<CombatHealth>().Dead;
            var animationState = modelAnimator.GetCurrentAnimatorStateInfo(0);
            // Attack의 마지막 프레임까지 재생한 후에만 Idle/Walk 전환 조건을 해제합니다.
            if (attackAnimationRequested && animationState.IsName("Attack") &&
                animationState.normalizedTime >= 1f && !modelAnimator.IsInTransition(0))
            {
                attackAnimationRequested = false;
                attackAt = Mathf.Max(attackAt, Time.time + .2f);
            }
            bool attacking = attackAnimationRequested;
            bool moving = alive && !AttackAnimationBusy && agent && agent.isActiveAndEnabled && agent.isOnNavMesh &&
                !agent.isStopped && agent.velocity.sqrMagnitude > .01f;
            modelAnimator.SetBool(MovingParameter, moving);
            modelAnimator.SetBool(AttackingParameter, attacking);
        }
        void OnDisable()
        {
            StopNavigation(); nextPathUpdate = 0; attackAnimationRequested = false; windupUntil = 0;
            if (modelAnimator && modelAnimator.isActiveAndEnabled)
            {
                modelAnimator.SetBool(MovingParameter, false);
                modelAnimator.SetBool(AttackingParameter, false);
            }
        }
        void Shoot(Vector3 direction)
        { Shoot(direction, 7); }
        void Shoot(Vector3 direction, float projectileSpeed)
        {
            var origin = transform.position - Vector3.up * (boss ? 1f : 0);
            PlayerWeapons.SpawnProjectile(origin, direction, projectileSpeed, damage, 24, boss ? .14f : .12f, transform, true, projectileMaterial, boss ? new Color(1, .4f, .15f) : new Color(.85f, .4f, 1), projectilePrefab);
        }
        // 적 머리 위에 체력과 공격 예고를 표시합니다.
        void OnGUI()
        {
            // 메뉴가 열려 있을 때 월드 체력바를 숨깁니다.
            if (BossCinematicUI.Hidden || SkillWindow.IsOpen) return;
            if (!health || health.Dead || !Camera.main) return;
            // 고정 높이 대신 활성 모델의 위쪽 경계를 따라가므로 크기와 피벗이 달라도 맞습니다.
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (var renderer in healthBarRenderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            var anchor = hasBounds ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)
                : transform.position + Vector3.up * (boss ? 2.5f : 1.5f);
            // 카메라 렌더 해상도와 게임 화면 해상도가 달라도 같은 위치에 표시합니다.
            var point = Camera.main.WorldToViewportPoint(anchor);
            if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return;
            var viewport = Camera.main.rect;
            point.x = (viewport.x + point.x * viewport.width) * Screen.width;
            point.y = (viewport.y + point.y * viewport.height) * Screen.height;
            // 화면 좌표를 GUI 좌표로 바꾸고 머리와 일정한 픽셀 간격을 둡니다.
            float top = Screen.height - point.y - 17;
            var previousColor = GUI.color;
            var width = boss ? 160 : 55;
            GUI.color = Color.black; GUI.DrawTexture(new Rect(point.x - width / 2, top, width, 7), Texture2D.whiteTexture);
            float fraction = health.maxHealth > 0 ? Mathf.Clamp01(health.Current / health.maxHealth) : 0;
            GUI.color = Windup ? Color.yellow : new Color(.95f, .35f, .25f); GUI.DrawTexture(new Rect(point.x - width / 2, top, width * fraction, 7), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (Windup) GUI.Label(new Rect(point.x - 40, top - 25, 100, 25), "! ATTACK");
            GUI.color = previousColor;
        }
    }
}
