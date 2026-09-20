using UnityEngine;
using UnityEngine.EventSystems;
namespace DogLife
{
    public sealed class InventoryDropArea : MonoBehaviour, IDropHandler
    {
        public SkillWindow window;
        public void OnDrop(PointerEventData e){if(window.DragSource)window.Unequip(window.DragSource);window.EndDrag();}
    }
}
