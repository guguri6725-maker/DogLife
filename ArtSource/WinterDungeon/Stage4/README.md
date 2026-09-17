# Stage4 Winter — 기본 방 구조

씬: `Assets/Scenes/Stage4_Winter.unity`

## 순서와 크기
1. Room_01_SnowForest — 눈숲, 24 × 24
2. Room_02_FrozenCave — 얼어붙은 동굴, 24 × 24
3. Room_03_SnowCanyon — 설산 협곡, 24 × 24
4. Room_04_IceRegion_Boss — 얼음 지역 보스 방, 32 × 30

크기는 바닥 배치 기준이며 실제 벽 안쪽 공간은 벽 두께만큼 작습니다. 방은 기존 3D 씬처럼 +X 방향으로 이어집니다. 방 사이 간격은 6, 중앙 통로는 약 6 폭입니다.

Hierarchy의 `Winter_Rooms` 아래에 방 4개와 `Connections`가 있습니다. 각 방의 `Floor`, `Walls`, `Layout_Markers`를 직접 편집할 수 있습니다. `Layout_Markers`는 렌더링되지 않는 배치 기준점이며 보스 방에는 `Boss_Spawn_Reserved`가 있습니다.

현재는 벽·바닥·연결 통로만 만들었습니다. 플레이어/몬스터/장식/전투 진행/방 스트리밍/NavMesh는 추가하지 않았습니다. 동굴도 편집 및 추후 카메라 설계를 위해 천장을 열어두었습니다. 기본 카메라는 전체 배치 확인용입니다.

## 재사용 에셋
`Assets/MapDesigne/Winter/`
- Models: 분리하고 최적화한 10개 FBX
- Prefabs: 재질과 정적 MeshCollider를 연결한 10개 프리팹
- Textures: 원본 BaseColor 및 Normal 텍스처
- Materials: 공유 Winter_Surface 재질

Glacier(빙벽) 2종도 최적화해 보관했으며 이번 4개 방 컨셉에는 사용하지 않았습니다. 원본 USDZ는 그대로 유지했습니다.

## 최적화
- 원본 합계 10,000 → 8,762 삼각형, 12.38% 감소.
- 원본부터 폴리곤이 적어 형태 보존을 우선했습니다. 각 부품에 75%, 85%, 93% 유지 비율을 순서대로 시험하고 기준을 만족하는 결과만 채택했습니다.
- 원본/최적화 메시의 꼭짓점에서 상대 메시 표면까지 양방향 거리를 측정했습니다. 부품 최대 길이 대비 최대 측정 오차는 0.4% 미만입니다. 모든 표면 점에 대한 수학적 보장은 아닙니다.
- UV와 원본 텍스처를 유지하고, 단위 크기와 원점을 정리했습니다. 바닥은 원래 기울어진 외곽선 때문에 각도와 겹침을 조절하여 빈틈을 덮었습니다.
- Blender 원본과 비교 렌더, 상세 수치는 `ArtSource/WinterDungeon/Optimization`에 있습니다.

## 확인 결과
- 방 내부 바닥 7,024지점, 중앙 경로 4,176지점에서 아래쪽 레이로 MeshCollider 표면 확인: 누락 0.
- 누락 재질/메시/스크립트 0. Unity 콘솔 오류 0.
- 전체 배치에는 166개 렌더러, 142,603개 삼각형이 있습니다. 실제 플레이 성능과 이동감 검사는 플레이어 연결 단계에서 진행해야 합니다.
- `Stage4_Final_Overview.png`: 최종 전체 배치.
- `Layout_Verification.json`: 검사 기록.
