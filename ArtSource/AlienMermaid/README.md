# 새 여름 인어 리그

현재 모델: 사용자가 제공한 `Teal Scaled Mermaid-d177994f047e546ada701b0de1415ac5.fbx`. 기존 Alien Mermaid의 32개 뼈와 8개 Action을 유지하고 표면 보간으로 가중치를 이전했다. 이전 모델은 `AlienMermaid_PreTeal.blend`에 보관했다.

## 파일

- `AlienMermaid_Rigged.blend`: 메시, 텍스처, 가중치, 8개 Action을 포함한 편집 원본.
- `build_rig.py`: 뼈대/가중치와 애니메이션 생성 과정을 기록한 Blender 스크립트.
- `refresh_animation_curves.cs.txt`: FBX 곡선을 기존 Unity 클립으로 옮기는 애니메이션 제작 코드.
- 게임 모델/재질/텍스처: `Assets/Art/SummerMonsters/AlienMermaid/`.
- 교체된 게임 프리팹: `Assets/Prefabs/monster/CoralMermaid.prefab`.

## 리깅

총 32개 관절: Root 1개, Hips/Spine/Chest/Neck/Head 5개, 좌우 쇄골/상완/전완/손 8개, 꼬리 8개, 좌우 꼬리지느러미 4개, 좌우 머리카락 6개.

메시 디자인과 UV를 유지하고 기존 인어와 같은 약 2.45m 높이에 맞췄다. 정점마다 최대 4개 관절 가중치를 사용한다. 꼬리는 뒤쪽 관절일수록 위상을 지연하여 연속적으로 휘어지며, 지느러미와 머리카락에 후속 움직임을 준다. 손가락 개별 제어용 리그는 포함하지 않는다.

## 애니메이션

Idle, Walk(수영), Dive, Burrow, Emerge, Recovery, AttackReady, Attack을 새 리그 기준으로 제작했다. 기존 Animator Controller와 전투 속도 제어를 유지하기 위해 단발 클립의 재생 길이는 기존 값에 맞췄다. 위치 이동/잠수 깊이/피격 판정은 기존 SummerMonsterEnemy에서 처리한다.

## FBX 재출력

- 메시와 Armature만 선택하고 출력한다. Add Leaf Bones는 끈다.
- Forward -Z, Up Y, 애니메이션 Bake를 사용한다. NLA와 All Actions를 동시에 출력하지 않는다.
- Unity Rig는 Generic을 사용한다. 인간형 상체가 있어도 다리 대신 꼬리가 있으므로 Humanoid Avatar로 강제하지 않는다.
- `AlienMermaid_Rig`와 Bone 이름을 바꾸면 기존 클립 경로도 다시 제작해야 한다.
- `CoralMermaid` 프리팹의 Visual/AlienMermaid Animator와 SummerMonsterEnemy.animator 연결을 유지한다.
- 원본 FBX에 재출력한 다음 곡선을 다시 옮겨야 기존 .anim 클립에 변경이 반영된다.

## 확인

Blender 정면/측면 프레임 확인, Unity 여름 씬 실행 및 실제 잠수/출현 공격 확인. 8개 클립의 바인딩 2640개 중 누락 경로 0개. Idle/Walk/Burrow 반복 시작-끝 꼬리 위치 차이 0. 8개 클립을 각 9시점에서 검사하여 NaN/Infinity 정점 없음. 최종 재실행 콘솔 오류 0개.


## 어깨와 동작 보정
refine_motion.py에서 어깨 주변 메시와 관절을 함께 낮추고 좁혔다. 기존 32개 뼈 이름을 유지하며 8개 동작을 준비/추진/반동/안정 구간으로 다시 구성했다. Unity 잠수 이동→공격 대기 전환에서 Burrow 재시작을 막고 긴 동작의 블렌드를 완화했다. 실제 재생 검사에서 Burrow 진행률 0.45가 전환 후에도 유지되며 누락 바인딩 0개를 확인했다. 보정 전 원본은 TealMermaid_BeforeRefinement.blend에 보관한다.
