// 说明：按稳定行标识局部刷新并保留阅读位置；布局合并更新，呈现就绪后才登记可见资料。
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zhijian.Presentation.Investigation;
using Zhijian.Presentation.Media;
using Zhijian.UnityUI.Media;

namespace Zhijian.UnityUI.Views.Investigation
{
    public sealed class InvestigationPageView : MonoBehaviour, IInvestigationPageView
    {
        private const int MaximumPooledRows = 16;
        private sealed class ScrollAnchor
        {
            public string RowKey;
            public RectTransform FixedTarget;
            public float Top;
            public float ContentY;
        }

        [SerializeField] private TMP_Text heading, lead, status, body;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private InvestigationRowView rowPrefab;
        [SerializeField] private GameObject mediaHolder, imageHolder;
        [SerializeField] private LayoutElement mediaSize;
        [SerializeField] private UnityMediaPlayer player;
        [SerializeField] private TeachingDetectionRunner detection;
        private readonly Dictionary<string, InvestigationRowView> rows = new Dictionary<string, InvestigationRowView>(StringComparer.Ordinal);
        private readonly List<string> order = new List<string>();
        private readonly Stack<InvestigationRowView> pool = new Stack<InvestigationRowView>();
        private readonly HashSet<string> read = new HashSet<string>(StringComparer.Ordinal);
        private readonly Vector3[] corners = new Vector3[4];
        private Transform poolRoot;
        private ScrollDragState drag;
        private CanvasGroup[] presentationGroups;
        private InvestigationPageData data;
        private ScrollAnchor pendingAnchor;
        private int firstRowIndex, readAfterFrame;
        private bool layoutPending, resetToTop, presentationReady;
        public IMediaPlayback Playback => player;
        public ITeachingDetection Detection => detection;
        public event Action<PageAction> ActionRequested;
        public event Action<string> SourceRead;
        public event Action<GameObject> ContentChanged;

        private void Awake()
        {
            firstRowIndex = content.childCount;
            drag = scroll.GetComponent<ScrollDragState>() ?? scroll.gameObject.AddComponent<ScrollDragState>();
            drag.Initialize(scroll);
            var inactive = new GameObject("InactiveRows", typeof(RectTransform));
            inactive.transform.SetParent(transform, false);
            inactive.SetActive(false);
            poolRoot = inactive.transform;
        }

        public void Initialize(Func<MediaRequest, UnityMediaAsset> resolver) => player.Initialize(resolver, scroll.viewport);

        public void SetPresentationReady(bool ready)
        {
            if (presentationReady == ready) return;
            presentationReady = ready;
            player.SetPresentationReady(ready);
            if (!ready) { scroll.StopMovement(); drag.Cancel(); }
            if (ready)
            {
                presentationGroups = GetComponentsInParent<CanvasGroup>(true);
                readAfterFrame = Time.frameCount;
            }
        }

        public void Render(InvestigationPageData next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            var same = data != null && data.Key == next.Key;
            var anchor = layoutPending ? pendingAnchor : CaptureAnchor();
            var dirty = !same;
            if (!same)
            {
                foreach (var row in rows.Values) Recycle(row);
                rows.Clear(); order.Clear(); read.Clear();
                scroll.StopMovement(); resetToTop = true; anchor = null;
            }

            data = next;
            dirty |= SetText(heading, next.Title);
            dirty |= SetText(lead, next.Lead);
            dirty |= SetText(status, next.Status);
            dirty |= SetText(body, next.Body);
            dirty |= SetActive(body.gameObject, !string.IsNullOrEmpty(next.Body));
            dirty |= SetActive(mediaHolder, next.Media != null);
            dirty |= SetActive(imageHolder, next.Media != null && next.Media.Kind != MediaDisplayKind.Audio);
            var height = next.Media?.Kind == MediaDisplayKind.Audio ? 96 : next.Media?.Kind == MediaDisplayKind.Image ? 1080 : 1360;
            if (!Mathf.Approximately(mediaSize.preferredHeight, height))
            {
                mediaSize.preferredHeight = height;
                if (mediaHolder.activeSelf) dirty = true;
            }

            var retained = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in next.Rows) retained.Add(item.Key);
            foreach (var key in new List<string>(rows.Keys))
                if (!retained.Contains(key))
                {
                    Recycle(rows[key]); rows.Remove(key); dirty = true;
                }

            var changedVisuals = new List<GameObject>();
            order.Clear();
            for (var index = 0; index < next.Rows.Count; index++)
            {
                var item = next.Rows[index];
                var added = !rows.TryGetValue(item.Key, out var row);
                if (added) { row = Acquire(); rows.Add(item.Key, row); dirty = true; }
                var update = row.Render(item, ForwardAction);
                if ((update & RowUpdate.Layout) != 0) dirty = true;
                if (added || (update & RowUpdate.Appearance) != 0) changedVisuals.Add(row.gameObject);
                if (row.transform.GetSiblingIndex() != firstRowIndex + index)
                { row.transform.SetSiblingIndex(firstRowIndex + index); dirty = true; }
                dirty |= SetActive(row.gameObject, true);
                order.Add(item.Key);
            }

            // 新行或换色只处理对应子树；纯保存状态更新不遍历整个 Shell。
            if (!same) ContentChanged?.Invoke(gameObject);
            else foreach (var target in changedVisuals) ContentChanged?.Invoke(target);
            if (dirty)
            {
                if (!layoutPending || !same) pendingAnchor = anchor;
                layoutPending = true;
            }
        }

        private InvestigationRowView Acquire()
        {
            if (pool.Count == 0) return Instantiate(rowPrefab, content, false);
            var row = pool.Pop();
            row.transform.SetParent(content, false);
            return row;
        }

        private void Recycle(InvestigationRowView row)
        {
            // 先停用再换父级，旧按钮操作不进入下一次绑定。
            row.gameObject.SetActive(false);
            row.Unbind();
            row.transform.SetParent(poolRoot, false);
            if (pool.Count < MaximumPooledRows) pool.Push(row);
            else Destroy(row.gameObject);
        }

        private void ForwardAction(PageAction action)
        {
            if (presentationReady) ActionRequested?.Invoke(action);
        }

        private void LateUpdate()
        {
            if (data == null) return;
            if (layoutPending)
            {
                RebuildLayout();
                return;
            }
            if (!CanRead()) return;
            var key = data.Key;
            var pending = new List<string>();
            foreach (var rowKey in order)
            {
                var row = rows[rowKey];
                if (row.SourceId != null && !read.Contains(row.SourceId) && FullyVisible(row.Rect)) pending.Add(row.SourceId);
            }
            if (data.ReadSourceId != null && body.gameObject.activeInHierarchy && !read.Contains(data.ReadSourceId) && BottomVisible(body.rectTransform)) pending.Add(data.ReadSourceId);
            foreach (var id in pending)
            {
                // 回调可能局部刷新或导航；没有真正发出的项留待下一帧，不能提前全部标已读。
                if (data == null || data.Key != key || !CanRead()) break;
                if (read.Add(id)) SourceRead?.Invoke(id);
            }
        }

        private void RebuildLayout()
        {
            var anchor = pendingAnchor;
            var movement = anchor == null ? 0 : content.anchoredPosition.y - anchor.ContentY;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (resetToTop) scroll.verticalNormalizedPosition = 1;
            else if (anchor != null)
            {
                RectTransform target = anchor.FixedTarget;
                if (anchor.RowKey != null) target = rows.TryGetValue(anchor.RowKey, out var row) ? row.Rect : null;
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    var position = content.anchoredPosition;
                    position.y -= Top(target) - anchor.Top - movement;
                    content.anchoredPosition = position;
                }
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
            }
            resetToTop = false; layoutPending = false; pendingAnchor = null;
            drag.Rebase();
            readAfterFrame = Time.frameCount;
        }

        private ScrollAnchor CaptureAnchor()
        {
            foreach (var key in order)
                if (Intersects(rows[key].Rect)) return Anchor(rows[key].Rect, key);
            if (body.gameObject.activeInHierarchy && Intersects(body.rectTransform)) return Anchor(body.rectTransform);
            var media = (RectTransform)mediaHolder.transform;
            if (mediaHolder.activeInHierarchy && Intersects(media)) return Anchor(media);
            return new ScrollAnchor { ContentY = content.anchoredPosition.y };
        }

        private ScrollAnchor Anchor(RectTransform target, string key = null)
            => new ScrollAnchor { RowKey = key, FixedTarget = key == null ? target : null, Top = Top(target), ContentY = content.anchoredPosition.y };

        private bool CanRead()
        {
            if (!presentationReady || layoutPending || !isActiveAndEnabled || Time.frameCount <= readAfterFrame) return false;
            if (presentationGroups != null)
                foreach (var group in presentationGroups) if (group != null && group.alpha < .99f) return false;
            return true;
        }

        private float Top(RectTransform rect)
        { rect.GetWorldCorners(corners); return scroll.viewport.InverseTransformPoint(corners[1]).y; }

        private bool Intersects(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            var bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            var top = scroll.viewport.InverseTransformPoint(corners[1]).y;
            return bottom <= scroll.viewport.rect.yMax && top >= scroll.viewport.rect.yMin;
        }

        private bool FullyVisible(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            var bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            var top = scroll.viewport.InverseTransformPoint(corners[1]).y;
            return bottom >= scroll.viewport.rect.yMin - 1 && top <= scroll.viewport.rect.yMax + 1;
        }

        private bool BottomVisible(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            var bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            return bottom >= scroll.viewport.rect.yMin - 1 && bottom <= scroll.viewport.rect.yMax + 1;
        }

        private static bool SetText(TMP_Text target, string text)
        {
            text = text ?? string.Empty;
            if (target.text == text) return false;
            target.text = text;
            return true;
        }

        private static bool SetActive(GameObject target, bool value)
        {
            if (target.activeSelf == value) return false;
            target.SetActive(value);
            return true;
        }

        private void OnDestroy() { ActionRequested = null; SourceRead = null; ContentChanged = null; }
    }
}
