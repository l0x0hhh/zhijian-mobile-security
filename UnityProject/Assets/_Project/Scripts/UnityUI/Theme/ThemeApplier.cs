// 说明：统一应用配色与动态中文字体，并释放本次运行创建的字体资源。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Zhijian.Presentation.Info;

namespace Zhijian.UnityUI.Theme
{
    public sealed class ThemeApplier : IThemePreferences, IDisposable
    {
        private readonly ThemeAsset theme;
        private TMP_FontAsset font;
        public bool IsDark { get; private set; }
        public event Action Changed;
        public ThemeApplier(ThemeAsset theme)
        {
            this.theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            if (theme.chineseFont == null || theme.fontShader == null) throw new InvalidOperationException("Theme font or shader reference is missing.");
            font = TMP_FontAsset.CreateFontAsset(theme.chineseFont, 72, 7, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new InvalidOperationException("Chinese font could not be loaded. Check Include Font Data.");
            font.name = "Zhijian Runtime Chinese";
            font.material.shader = theme.fontShader;
        }
        public void Apply(GameObject root)
        {
            var palette = IsDark ? theme.dark : theme.light;
            foreach (var binding in root.GetComponentsInChildren<ThemeBinding>(true)) binding.Apply(palette, font);
        }
        public void SetDark(bool value)
        {
            if (IsDark == value) return;
            IsDark = value;
            Changed?.Invoke();
        }
        public void Dispose()
        {
            Changed = null;
            if (font == null) return;
            foreach (var atlas in font.atlasTextures) if (atlas != null) UnityEngine.Object.Destroy(atlas);
            if (font.material != null) UnityEngine.Object.Destroy(font.material);
            UnityEngine.Object.Destroy(font);
            font = null;
        }
    }
}
