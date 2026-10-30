// 说明：选关页面只负责显示目录和发出选择意图。
using System;

namespace Zhijian.Presentation.Home
{
    public interface ICaseSelectionView
    {
        event Action<string> CaseSelected;
        void Render(CaseSelectionViewData data);
    }
}
