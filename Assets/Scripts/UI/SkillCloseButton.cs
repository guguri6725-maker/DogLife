using UnityEngine;
using UnityEngine.EventSystems;
namespace DogLife
{
    // 닫기 버튼의 창 참조는 Inspector에서 직접 연결합니다.
    public sealed class SkillCloseButton : MonoBehaviour, IPointerClickHandler
    {
        public SkillWindow window;
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) window.Close(); }
    }
}
