// 说明：延迟展示案件预设观察点，仅作教学演示；取消或进入后台不回报成功。
using System;
using System.Collections;
using UnityEngine;
using Zhijian.Presentation.Media;
namespace Zhijian.UnityUI.Media
{
    public sealed class TeachingDetectionRunner : MonoBehaviour, ITeachingDetection
    {
        private MediaRequest current;
        private Coroutine pending;
        public event Action<PlaybackUpdate> Changed;
        public void Begin(MediaRequest request)
        { Cancel(); current=request; Changed?.Invoke(new PlaybackUpdate(request, PlaybackPhase.Preparing, "正在生成教学观察提示…")); pending=StartCoroutine(Complete(request)); }
        private IEnumerator Complete(MediaRequest request)
        {
            yield return new WaitForSecondsRealtime(.9f);
            pending=null;
            if (current != request) yield break;
            current=null; Changed?.Invoke(new PlaybackUpdate(request, PlaybackPhase.Completed, "教学观察提示已生成；不代表现实鉴定结果。"));
        }
        public void Cancel()
        {
            if (pending != null) { StopCoroutine(pending); pending=null; }
            var request=current; current=null;
            if (request != null) Changed?.Invoke(new PlaybackUpdate(request, PlaybackPhase.Cancelled, "教学检测已取消。"));
        }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        private void OnDisable() => Cancel();
        private void OnDestroy() { Cancel(); Changed=null; }
    }
}
