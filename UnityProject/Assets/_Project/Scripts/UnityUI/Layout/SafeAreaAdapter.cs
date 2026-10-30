// 说明：将可操作内容限制在屏幕安全区，屏幕尺寸或方向变化后重新计算。
using UnityEngine;

namespace Zhijian.UnityUI.Layout
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaAdapter : MonoBehaviour
    {
        private RectTransform target;
        private Rect previousArea;
        private Vector2Int previousSize;
        private void Awake() { target = (RectTransform)transform; Apply(); }
        private void Update()
        {
            if (previousArea != Screen.safeArea || previousSize.x != Screen.width || previousSize.y != Screen.height) Apply();
        }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var area = Screen.safeArea;
            target.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / Screen.width), Mathf.Clamp01(area.yMin / Screen.height));
            target.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / Screen.width), Mathf.Clamp01(area.yMax / Screen.height));
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            previousArea = area;
            previousSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
