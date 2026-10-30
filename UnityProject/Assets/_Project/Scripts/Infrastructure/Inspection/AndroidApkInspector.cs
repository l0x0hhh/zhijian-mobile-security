// 说明：轮询 Android 原生检查结果，取消与旧请求隔离；编辑器明确不可用，不返回示例数据。
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zhijian.Application.Inspection;

namespace Zhijian.Infrastructure.Inspection
{
    public sealed class AndroidApkInspector : MonoBehaviour, IApkInspector, IDisposable
    {
        [Serializable] private sealed class NativeResult
        { public string requestId, status, message; public ApkFactsDto facts; }
        private TaskCompletionSource<ApkPackageFacts> pending;
        private CancellationTokenRegistration registration;
        private string request;
        private float nextPoll, deadline;
        private int cancelRequested;
        private bool disposed;
        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return !disposed;
#else
                return false;
#endif
            }
        }
        public Task<ApkPackageFacts> SelectAndInspectAsync(CancellationToken cancellation)
        {
            if (disposed) throw new ObjectDisposedException(nameof(AndroidApkInspector));
            if (!IsSupported) throw new NotSupportedException("真实 APK 检查需要在 Android 设备运行；编辑器不提供模拟结果。");
            if (pending != null) throw new InvalidOperationException("已有检查正在进行。");
            cancellation.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<ApkPackageFacts>();
            pending = completion; request = Guid.NewGuid().ToString("D"); cancelRequested = 0;
            deadline = Time.realtimeSinceStartup + 600f; nextPoll = 0;
            registration = cancellation.Register(() => Interlocked.Exchange(ref cancelRequested, 1));
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var native = new AndroidJavaClass("com.zhijian.inspection.ApkInspectionActivity"))
                    native.CallStatic("begin", activity, request);
#endif
            }
            catch (Exception e) { Finish(null, e); }
            return completion.Task;
        }
        private void Update()
        {
            if (pending == null) return;
            if (Volatile.Read(ref cancelRequested) != 0) { CancelNative(); Finish(null, null, true); return; }
            if (Time.realtimeSinceStartup > deadline) { CancelNative(); Finish(null, new TimeoutException("检查已超时，请重新选择文件。")); return; }
            if (Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 0.12f;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                string json;
                using (var native = new AndroidJavaClass("com.zhijian.inspection.ApkInspectionActivity"))
                    json = native.CallStatic<string>("takeResult", request);
                if (string.IsNullOrEmpty(json)) return;
                if (json.Length > 524288) throw new FormatException("原生结果超出支持范围。");
                var result = JsonUtility.FromJson<NativeResult>(json);
                if (result == null || result.requestId != request) throw new FormatException("检查结果与当前请求不匹配。");
                if (result.status == "cancelled") Finish(null, null, true);
                else if (result.status == "ok" && result.facts != null) Finish(result.facts.ToFacts(), null);
                else throw new InvalidOperationException(string.IsNullOrEmpty(result.message) ? "文件无法检查。" : result.message);
#endif
            }
            catch (Exception e) { CancelNative(); Finish(null, e); }
        }
        private void Finish(ApkPackageFacts facts, Exception error, bool cancelled = false)
        {
            var completion = pending; pending = null; request = null; registration.Dispose();
            if (completion == null) return;
            if (cancelled) completion.TrySetCanceled();
            else if (error != null) completion.TrySetException(error);
            else completion.TrySetResult(facts);
        }
        private void CancelNative()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var native = new AndroidJavaClass("com.zhijian.inspection.ApkInspectionActivity"))
                    native.CallStatic("cancel", request);
            }
            catch (Exception e) { Debug.LogWarning("APK 检查取消：" + e.GetType().Name); }
#endif
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (pending != null) { CancelNative(); Finish(null, null, true); }
        }
        private void OnDestroy() => Dispose();
    }
}
