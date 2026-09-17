# Summer 폭포 문
## 저장 위치
- 씬: Assets/Scenes/Stage3_Summer.unity
- 문 Hierarchy: Summer_Rooms/Summer_WaterDoors, 네 방 입구/출구 총 8개.
- 수로 Hierarchy: Summer_Rooms/Summer_Waterways, 각 방 Entrance/Exit_DoorRunoff.
- 프리팹: Assets/MapDesigne/Summer/Door/Prefabs/SummerWaterfallDoor.prefab
- Blender: SummerWaterfallDoor.blend / SummerDoor_Runoff.blend
- 원본 Summer_Door_Designe.usdz와 기존 수로 FBX는 유지했습니다.

## 디자인/최적화
- 돌/식물 문틀 30,000 → 18,000 삼각형 (40% 절감).
- 얇은 물막 768 삼각형, 문 전체 18,768 삼각형.
- 문틀 원본 UV/색/노멀 유지. 표면 거리 95백분위 0.001354, 최대 0.009909(원본 폭 약 1).
- 물막은 중심 공간에 맞춘 양면 표면이며 두꺼운 물 블록이 아닙니다.
- 일반 문 모델 크기 (16,14,5), 보스룸 출구는 (18,14,5). 루트 y=-0.65, Y 회전 90도.
- 첫 입구 EntryWall_3 및 마지막 출구 ExitWall_3/4는 문과 겹쳐 비활성 보관.

## 물 머티리얼/애니메이션
- 폭포: 기존 Assets/MapDesigne/Summer/Materials/WaterRevision/FallingWater.mat 공유.
- 얕은 물길: 기존 같은 폴더의 FlowingWater.mat 공유.
- Blender 내 Summer_Waterfall_Door 클립은 3초 동안 물막 폭을 줄입니다.
- WaterGateFade는 VineGate.Progress를 따라 해당 물막만 투명하게 만듭니다. 공유 머티리얼을 수정하거나 복제하지 않습니다.
- 기존 Waterfall 셰이더의 흔들림을 월드 기준 3.5cm로 보정하여 FBX 단위/회전에 관계없이 얇은 물막을 유지합니다.
- Flowing Water 셰이더는 정점 알파를 반영합니다. 새 물길 가장자리는 부드럽게 흐려지고, 기존 강은 정점 알파 1이라 투명도가 유지됩니다.
- VineGate.Open()을 방 클리어 이벤트에 연결하면 됩니다. 전투 진행/근접 자동 개방은 추가하지 않았습니다.
- Inspector: VineGate.Duration으로 개방 시간, WaterGateFade.Editor Preview(0~1)로 물 투명도 미리보기. 저장값은 0.
- 물의 시간 흐름은 Play에서 보거나 Scene View Animated Materials를 켜 확인하세요.

## 강 연결
- 아래의 좁은 수로 기록은 최초 버전입니다. 현재 적용 상태는 문서 마지막의 넓은 물길 수정 항목을 참고하세요.
- 각 문 밑의 물받이에서 방 안쪽으로 곡선을 그리며 기존 강으로 이어지는 좁은 물길 8개.
- 물길 시작 수면 y=0.11, 강 연결부 y=-0.245(기존 강 -0.25)로 연결.
- 강둑 4개는 기존 형태를 기반으로 연결부만 낮추고, 좁은 수로 경계의 메시를 세분화했습니다.
- 물길을 가리던 장식 9개는 같은 방 안에서 옮기고 새 바닥에 접지했습니다. 이동 전후 좌표는 Validation_Report.json에 보관.
- 원본 강둑 FBX는 보존하고 새 DoorChannelBank FBX를 MeshFilter와 MeshCollider에 함께 연결했습니다.

## 검증
- 실제 재생 Open(): 8개 모두 개방 완료, 물막 opacity=0 / renderer 비활성 / 통로 충돌 해제.
- 중간 진행도 약 0.281에서 opacity=0.581 확인. 공유 폭포 머티리얼 opacity는 0.72 유지.
- 열린 문 통로 48개 광선 검사에서 충돌 0.
- 새 수로 중앙 408개 지점에서 지형에 파묻힌 곳 0. 장식이 물길을 가리는 구간 0(문 자체 제외).
- 중앙 바닥 820개 지점 충돌 누락 0.
- 활성 씬 메시 합계 988,242 삼각형. 카메라에 실제 보이는 수나 FPS 측정값은 아닙니다.
- 누락 재질 0, 셰이더 오류 0, Unity Console 오류 0.
- 검사 후 재생 종료, 모든 문 닫힘 / Editor Preview=0으로 저장.
- 실제 플레이어 이동 및 방 클리어 시스템 연동은 별도 작업입니다.

## 최종 이미지
Summer_Door_Final_Closed.png / Summer_Door_Open_Runtime.png / Summer_Door_Connected_Stream.png.
Initial/Preview/Refined 표기는 중간 작업 기록입니다.

## 넓은 물길 수정 (2026-09-17)
- 4개 방의 입구/출구 지류 8개를 약 2.6~5.5m 폭의 자연스러운 곡선으로 확장했습니다. 끝부분은 둥글게 줄어듭니다.
- 각 방의 기존 강과 새 지류를 UnifiedDoorRiver 단일 메시로 합쳐 겹침과 연결 경계를 없앴습니다.
- 구조물 아래 통과를 허용하며 새 물길에 맞춰 WideChannelBank 지형과 충돌 메시를 함께 적용했습니다.
- 기존 좁은 DoorRunoff 8개는 Hierarchy에서 비활성으로 보존했습니다.
- 어둡게 보이던 원인은 이전 수로의 뒤집힌 표면 방향이었습니다. 모든 새 수면을 위쪽으로 교정했고 기존 FlowingWater 공유 머티리얼을 그대로 사용합니다.
- 물길 내부 1,712개 지점에서 바닥에 파묻힘 0, 중앙 이동 바닥 충돌 누락 0. 네 방 모두 지형 렌더/충돌 메시 일치, Unity Console 오류 0.
- 앞 절의 삼각형 수 및 좁은 수로 검증은 이전 버전 기록입니다. 새 메시별 삼각형 수는 WideRiver_Report.json에 기록했습니다.
- Blender 원본: Summer_Merged_Natural_Rivers.blend / 재생성 코드: build_wide_merged_rivers.py
- 확인 이미지: Wide_River_Room1.png, Wide_River_Room3.png
- 별도 런타임 이동/FPS 검사는 이번 수정에서 수행하지 않았습니다.

## 방 전체 얕은 물 변경 (2026-09-17, 현재 적용)
- 사용자 요청에 따라 좁은 강/지류 방식 대신 모든 Summer 방과 연결 통로에 얕은 물을 적용했습니다.
- 바닥 평균 Y=-0.18, 수면 Y=-0.03. 실제 수심 약 0.14~0.16m.
- 각 방의 Riverbank/ContinuousWater는 ShallowRooms 폴더의 새 메시를 참조합니다. 이전 모델 파일은 보관합니다.
- 방 4개와 통로 3개 수면: BoxCollider Is Trigger=true, Water 레이어. 바닥: MeshCollider Is Trigger=false, Ground 레이어.
- ShallowRoomWater.mat는 기존 흐르는 물 머티리얼의 전용 복사본으로 불투명도 0.28, 잔물결 0.035, 흐름 속도 0.16을 적용했습니다.
- 방 사이 기존 바닥 3개는 비활성으로 보관하고 높이가 맞는 통로 바닥으로 교체했습니다.
- Decor 루트 4개를 Y=-0.26으로 내려 낮아진 바닥에 맞췄습니다.
- 1,888개 지점에서 바닥 충돌 누락 0, 모든 물 표면 위쪽 방향, 물 Trigger 상단과 수면 일치 확인.
- 물 접촉 시 물보라/발소리/이동 속도 변화 로직은 추가하지 않았습니다. Trigger 영역 준비 상태입니다.
- 플레이어 접지 검사는 Trigger를 무시하도록 기존 코드가 설정되어 있습니다. 실제 플레이 조작 검증은 수행하지 않았습니다.
- Blender 원본: Summer_Shallow_Rooms.blend. 작업 전 씬: Stage3_Before_ShallowRooms.unity.backup.
