# 3D_Stage2 여름 바닷가 던전

- 작업 씬: `Assets/Scenes/3D_Stage2.unity`
- 참고: `Assets/Scenes/3D.unity`의 3D XZ 이동 공간, X축 방향 23m 간격 6개 구역과 X=155 부근의 넓은 마지막 광장.
- 구성: 해변 입구 → 야자수 쉼터 → 바위 협곡 → 폐허 안뜰 → 화물 야적장 → 난파선 해안 → 마지막 광장.
- 제공된 Summer FBX 8종 모두 사용. 원본은 바탕화면에 유지하고 프로젝트의 `Assets/Environment/SummerCoast`로 가져왔습니다.
- 현재 프로젝트는 Built-in 렌더링이므로 원본 텍스처로 Standard 재질을 별도 생성했습니다. 색상/노멀/AO/MetallicSmoothness를 연결하고 사용 텍스처의 최대 해상도를 2048로 설정했습니다.
- 바닥 타일은 높이 방향을 낮춰 이동 바닥과 맞췄고, 연속된 모래 하부 메시로 이음새를 보완했습니다. 바닥·장애물·외곽에는 레이어 8의 단순 충돌체를 배치했습니다.
- 맵 루트: `Stage2_3D_SummerCoast`. CoastFloor, Shoreline, Canopy, Rocks, Ruins, Landmarks, Platforms, BossArena, Boundaries로 분리했습니다.
- LayoutMarkers의 PlayerStart, EncounterSpace, Doorway, RecoverySpace, StageExit는 배치 참고용 빈 오브젝트입니다. 보스/적/플레이어/전투/UI/클리어 로직은 추가하지 않았습니다.
- 고정 레이아웃 맵이며 실행 중 무작위 생성이나 NavMesh 베이크는 포함하지 않습니다.
- 에디터 생성 도구: `Assets/Scripts/Editor/Builders/SummerCoastMapBuilder.cs`. 기존 맵 루트가 있으면 재생성을 막아 수동 편집을 보호합니다.
- 시각 확인: 전체 지도와 난파선 구역 미리보기를 `Documentation/Previews`에 저장했습니다.
- 이동 검증: 중심선과 좌우 2m 경로의 264개 지점을 플레이어 크기의 캡슐과 바닥 레이로 검사했습니다. 출구 벽을 넓힌 후 막힘·바닥 누락 0개입니다. 재질 오류·누락 스크립트 0개, 사용 모델 종류 8개를 확인했습니다. 플레이어를 추가한 수동 이동 테스트는 수행하지 않았습니다.
- 기존 콘솔에 VARCO 패키지의 다른 PC 경로(`C:/Users/mbc/Desktop/VARCO3D-Bridge-for-Unity`) 누락 오류가 있습니다. 맵 제작 범위와 별개여서 패키지 설정을 변경하지 않았습니다.
