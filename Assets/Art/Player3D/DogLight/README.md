# DogLight 플레이어

- `Models/DogLight_Rigged.fbx`: 23개 뼈대, 분리된 `DogLight_Body` / `DogLight_Scarf` 메시.
- `Animations/DogLight_Idle_Original.anim`: 제공된 USDZ의 Idle 원본을 독립 클립으로 복사.
- `Animations/DogLight_Idle.anim`: 변환된 FBX 뼈대에 맞는 몸 Idle, 약 8.9초 반복.
- `Animations/DogLight_ScarfFlutter.anim`: 스카프 변형만 포함하는 반복 클립.
- `Animations/DogLight.controller`: 기본 Idle 레이어와 독립적인 `Scarf Wind` 레이어.
- `Prefabs/DogLight_Animated.prefab`: 몸과 스카프의 애니메이션이 연결된 외형 프리팹.

실제 플레이어는 `Assets/Prefabs/Player.prefab`의 `VisualRoot/DogLight_PlayerVisual`에 연결되어 있습니다. 이동·점프·웅크리기·대시·무기 관련 구성은 공통 Player에 유지됩니다. 현재 몸 애니메이션은 제공된 Idle입니다.

스카프는 목 주변을 고정하고 늘어진 끝에만 두 개의 블렌드셰이프를 적용합니다. `Scarf Wind` 레이어의 가중치를 0으로 바꾸면 바람 효과를 끌 수 있습니다. `DogLight_ScarfFlutter.anim`의 두 곡선 크기로 흔들림 세기를 조정할 수 있습니다.

원본 모델은 `Assets/VARCO3DImports/Dog_LightModel.usdz`입니다. ArtSource 복사본은 만들지 않았습니다.
