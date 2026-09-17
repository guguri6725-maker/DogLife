# Forest 덩굴 문

## 적용 범위
- `Assets/Scenes/ForestRooms/Forest_Room_01.unity` ~ `Forest_Room_07.unity`
- 기존 큰 Cube 벽의 MeshRenderer/MeshFilter와 중앙 Cube 문을 제거했습니다.
- 기존 석재 기둥/문틀, 맵 구조, 측면 이동 제한은 유지했습니다.
- 양옆 수풀은 고정되어 있고 중앙 덩굴 14줄이 시간차를 두고 양옆으로 걷힙니다.
- 전투 종료 후 다음 방이 준비되면 2.4초 동안 개방합니다. 완전히 열려야 통과됩니다.
- 다음 방에 들어가면 뒤쪽 덩굴이 역재생으로 닫힙니다. 닫힌 후 이전 방을 해제합니다.
- 맵 밖 진입을 막는 보이지 않는 충돌 경계는 남아 있습니다.

## Unity Inspector에서 조절
- 방 Hierarchy의 `Gate_번호/VineDoor` → `VineGate` → **Duration**: 개방 시간(기본 2.4초).
- `RearSeal/RearVines/VineDoor` → **Duration**: 뒤쪽 문 닫힘 시간.
- `Opening Clip`: Blender FBX에 포함된 `Forest_VineGate_Design` 클립.
- `Animation Root`, `Visual Root`, `Blockers`, `Navigation Blockers`는 Inspector에 연결되어 있습니다.
- 2~7번 방 뒤쪽 문은 `Start Open`, `Hide When Initially Open`이 켜져 있습니다. 이전 방 문과 겹치지 않게 하려는 설정입니다.
- 재사용 프리팹: `Assets/Art/ForestVineGates/ForestVineGate.prefab`, `ForestVineThicket.prefab`.
- 재질: `Assets/Art/ForestVineGates/Materials/ForestVine.mat`. 잎과 줄기의 색은 메시의 Vertex Color에 저장되어 있습니다.

## Blender 원본 및 포트폴리오 자료
- `ForestVineGate.blend`: 모델과 개방 애니메이션 원본. 30fps, 1~73프레임.
- `build_vine_gate.py`, `pack_vertex_palette.py`: 모델 생성 및 내보내기 과정 기록.
- `Before_CubeWalls.png`: 교체 전.
- `After_Vines_Closed.png`: 교체 후 닫힌 문.
- `After_Vines_Open.png`: 교체 후 열린 문.
- `PlayModeVerification.json`: 7개 방 실제 플레이 통합 검사 결과.

Blender에서 수정할 경우 모델의 개별 덩굴 이름과 계층을 유지해야 Unity 애니메이션 바인딩이 유지됩니다.
