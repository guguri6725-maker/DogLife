# 계절별 시간대 조명

- 봄: 아침. 낮은 따뜻한 햇빛, 부드러운 푸른 하늘.
- 여름: 오후. 높은 햇빛, 선명한 푸른 하늘과 밝은 수면 반사.
- 가을: 노을지는 저녁. 낮은 주황 햇빛, 노을 하늘, 긴 그림자.
- 겨울: 새벽. 짙은 푸른 하늘, 차가운 약한 빛, 그림자 속 시야를 위한 환경광.

## 조정 위치
- 각 계절 메인 씬의 Directional Light: 햇빛 방향, 색, 밝기, 부드러운 그림자.
- Lighting > Environment: Skybox Material, 3색 환경광, 옅은 Linear Fog, 반사 강도.
- Assets/Art/SeasonLighting: Spring_Sky / Summer_Sky / Fall_Sky / Winter_Sky 머티리얼.
- SeasonGameplay/Main Camera 및 각 Overview Camera: Clear Flags를 Skybox로 변경.
- 계절 메인 씬이 Active인 상태로 방을 추가 로드하므로 해당 계절 환경 설정을 유지합니다.
- 씬 생성/조명 제어 스크립트는 추가하지 않았으며, Unity MCP의 기본 설정 도구로 저장했습니다.

## 확인
- 각 계절 첫 방을 추가 로드한 뒤 같은 시점에서 화면을 확인했습니다.
- Winter_Dawn_Gameplay.png: Play 중 실제 Main Camera 화면.
- 미리보기 PNG 4개 및 게임 화면은 이 폴더에 보관합니다.
- 조명은 실시간 Directional Light + 환경광이며 라이트맵 베이크는 수행하지 않았습니다.
