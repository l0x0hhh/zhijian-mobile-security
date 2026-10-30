// 说明：布局修正后重设当前拖动起点，避免下一次指针移动把视口拉回旧位置。
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zhijian.UnityUI.Views.Investigation
{
    [DisallowMultipleComponent]
    public sealed class ScrollDragState : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ScrollRect scroll;
        private PointerEventData pointer;
        public void Initialize(ScrollRect target) { scroll = target; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pointer = eventData;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pointer != null && pointer.pointerId == eventData.pointerId) pointer = eventData;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (pointer != null && pointer.pointerId == eventData.pointerId) pointer = null;
        }

        public void Rebase()
        {
            // 直接更新 ScrollRect 的拖动起点，不再次派发 UI 事件。
            if (pointer != null && scroll != null && scroll.isActiveAndEnabled) scroll.OnBeginDrag(pointer);
        }

        public void Cancel()
        {
            if (pointer != null && scroll != null) scroll.OnEndDrag(pointer);
            pointer = null;
        }

        private void OnDisable() => Cancel();
    }
}
