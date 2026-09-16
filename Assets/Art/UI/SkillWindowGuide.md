# 스킬창 수정 안내

씬: `Assets/Scenes/3D.unity` · Hierarchy 루트: `SkillUI`

## 사용

- K: 열기 / 닫기. Esc 또는 오른쪽 위 ×로도 닫습니다.
- 스킬 목록: 휠 또는 오른쪽 스크롤바로 이동합니다.
- 목록의 스킬 클릭: 오른쪽 스킬명, 설명, 획득 경로 표시.
- 스킬을 맨 왼쪽 슬롯으로 드래그: 장착. 슬롯끼리 드래그: 교환.
- 장착 슬롯 우클릭: 해제. 슬롯 밖에 놓기: 취소.
- 같은 스킬은 여러 슬롯에 중복 장착되지 않습니다.
- 창을 열면 게임을 일시 정지하고 마우스를 표시합니다. 닫으면 이전 상태를 복원합니다.

## 흰색 이미지를 실제 스킬 이미지로 바꾸기

1. 이미지 파일을 Project에 넣습니다.
2. 이미지 Inspector에서 **Texture Type → Sprite (2D and UI)**, **Sprite Mode → Single**을 선택하고 **Apply**합니다.
3. Hierarchy에서 다음 위치를 펼칩니다.

   `SkillUI / Window / Frame / Library / Scroll / Viewport / Content / Skill_01 / IconFrame / Icon`

4. `Icon`의 **Image → Source Image**에 이미지를 드래그합니다. **Color는 흰색**으로 유지하세요.
5. 다른 스킬은 `Skill_02`부터 `Skill_08`까지 같은 방식으로 바꿉니다.

오른쪽 상세 이미지, 드래그 이미지, 장착 슬롯은 해당 목록 아이콘을 사용합니다. 각각 따로 바꿀 필요가 없습니다. 이미지 비율 유지를 켜 두었습니다.

## 이름·설명·획득 경로 수정

각 `Skill_01` 등의 **Skill Card** 컴포넌트에서 수정합니다.

| Inspector 항목 | 표시 위치 |
| --- | --- |
| Skill Name | 목록, 상세 화면, 장착 슬롯의 이름 |
| Category | 목록과 상세 화면의 분류 |
| Description | 오른쪽 스킬 설명 |
| Obtain Path | 오른쪽 획득 경로 |

Play를 시작하면 목록에도 입력한 이름과 분류가 반영됩니다. 영구 수정은 Play를 끈 상태에서 하세요.

## 현재 범위

스킬 8종의 이름·설명·획득 경로는 디자인 확인용 예시입니다. 이번 작업은 스킬창과 장착 UI까지이며, 실제 스킬 발동·드롭 보상은 아직 연결하지 않았습니다. 장착 상태는 현재 플레이 중 창을 닫았다 열어도 유지되며, 플레이 종료 후 저장되지는 않습니다.

UI는 Canvas, Image, Text, ScrollRect, RectMask2D, Scrollbar 등을 Hierarchy에 직접 배치했습니다. 스크립트가 UI를 생성하거나 Inspector 참조를 자동 탐색하지 않습니다. 창 루트 `Window`는 편집할 때 보이지만 Play 시작 시 자동으로 닫힙니다.

추가 스킬을 만들 때는 Content 아래 항목을 복제하고, Skill Card의 이름·정보·Icon·라벨·Selection 참조를 확인한 다음 `SkillUI`의 **Skill Window → Skills** 목록에도 추가합니다. Content 높이와 항목의 Y 위치, 목록의 전체 개수 표시는 Inspector에서 함께 조정합니다.

한글 글꼴은 나눔고딕을 사용합니다. 배포 시 `Fonts/OFL.txt` 라이선스를 함께 보관하세요.
