# Summer 물길 및 벽 수정

## 적용 위치
- 씬: Assets/Scenes/Stage3_Summer.unity
- Hierarchy: Summer_Rooms/Summer_Waterways
- 각 방 ContinuousWater: FlowingWater.mat (연속 수면 4개)
- Cave_FallingWater_A/B, Room3_FallingWater_A/B: FallingWater.mat (투명한 폭포 물막)
- 머티리얼: Assets/MapDesigne/Summer/Materials/WaterRevision
- 모델: Assets/MapDesigne/Summer/Models/WaterRevision
- 셰이더: Assets/MapDesigne/Summer/Shaders

## 수정 내용
- 반복된 물 그림 바닥을 강둑과 연속 수로로 교체. 원래 Floor는 비활성 상태로 보존.
- 새 강둑 MeshCollider를 먼저 설치하고 31,512개 지점에서 충돌을 확인한 후 기존 바닥을 비활성화.
- 고립된 물 타일/웅덩이 13개 비활성 보관. 장식 115개의 바닥 접지를 조정하고 폭포/캐스케이드 10개를 수로 뒤편에 배치.
- 월드 좌표 기준 잔물결, 표면 법선 변화, 가장자리 거품 및 반투명 물막을 셰이더 시간으로 처리. Animator/매 프레임 실행 C# 스크립트 불필요.
- EntryWall_3: Blender에서 구멍 메우기와 얇은 안쪽 두께 추가. 열린 가장자리 96 → 0. MeshFilter와 MeshCollider 모두 복구 FBX를 참조.
- 사용자가 추가 승인한 Room3 벽 머티리얼 17개 적용. 최종 북쪽 5개 물 흐름, EntryWall_3 포함 나머지 13개는 파란 물과 밝은 거품을 돌 색으로 억제. 다른 벽의 메시/충돌은 유지.
- 모든 Unity 오브젝트 배치, 머티리얼 및 Inspector 참조는 native MCP로 연결.

## Inspector 조절
- FlowingWater.mat: Flow speed, Ripple strength, Smoothness, Water opacity, Deep/Shallow water.
- FallingWater.mat: Fall speed, Opacity, Water tint.
- Atlas0.mat: 3번 방 뒤쪽 폭포 벽. Atlas1.mat: 폭포/캐스케이드. Atlas4.mat: 보스룸 캐스케이드.
- Preview time은 -1이 정상값. Play에서 시간에 따라 흐르며, Scene View에서는 Animated Materials 표시 설정에 따라 갱신 여부가 달라질 수 있음.
- 0과 2를 임시 입력해 같은 구도로 촬영한 비교에서 샘플 90,000픽셀 중 18,946픽셀 변화 확인. 최종값은 모두 -1로 복구.

## 검증
- 중앙 동선 9,204점 바닥 누락 0, 최상단 높이 -0.075~0.108.
- 중앙 장식 장애물 0, 소속 방 밖으로 밀린 장식 0.
- 활성 씬 메시 인스턴스 합계 683,195 삼각형. 150만 예산 대비 816,805 여유.
- 셰이더 오류 0, Unity Console 오류 0 확인.
- 실제 플레이어/적 이동, NavMesh 및 FPS 테스트는 별도.

## 보관
- Stage3_Before_WaterRevision.unity.backup: 변경 전 백업.
- Summer_Connected_Waterways.blend, Summer_Wall_Repair.blend: Blender 작업본.
- Summer_Water_Final_Overview-1.png, Room3_Water_Final_Close.png: 최종 화면.
- 이름에 Iteration 또는 FlowCheck가 들어간 이미지는 중간 검사이며 최종 배치와 다를 수 있음.
