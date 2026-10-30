// 说明：将单个图形或文字绑定到主题语义；布局与字号保留在预制体中。
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zhijian.UnityUI.Theme
{
    [DisallowMultipleComponent]
    public sealed class ThemeBinding : MonoBehaviour
    {
        [SerializeField] private Graphic target;
        [SerializeField] private ThemeRole role;
        public void SetRole(ThemeRole value) { role = value; }
        public void Apply(ThemePalette palette, TMP_FontAsset font)
        {
            if (target == null) throw new MissingReferenceException(name + " has no theme target.");
            target.color = palette.Get(role);
            if (target is TMP_Text label) label.font = font;
        }
    }
}
