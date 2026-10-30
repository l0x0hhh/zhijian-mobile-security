// 说明：统一保存日夜配色与中文字体来源，业务层不引用此资产。
using System;
using UnityEngine;

namespace Zhijian.UnityUI.Theme
{
    public enum ThemeRole { Background, Paper, Text, Secondary, Divider, Accent, OnAccent, Soft, Neutral }
    [Serializable]
    public sealed class ThemePalette
    {
        public Color background, paper, text, secondary, divider, accent, onAccent, soft, neutral;
        public Color Get(ThemeRole role)
        {
            switch (role)
            {
                case ThemeRole.Background: return background;
                case ThemeRole.Paper: return paper;
                case ThemeRole.Secondary: return secondary;
                case ThemeRole.Divider: return divider;
                case ThemeRole.Accent: return accent;
                case ThemeRole.OnAccent: return onAccent;
                case ThemeRole.Soft: return soft;
                case ThemeRole.Neutral: return neutral;
                default: return text;
            }
        }
    }
    [CreateAssetMenu(menuName = "Zhijian/UI Theme", fileName = "Theme")]
    public sealed class ThemeAsset : ScriptableObject
    {
        public Font chineseFont;
        public Shader fontShader;
        public ThemePalette light;
        public ThemePalette dark;
    }
}
