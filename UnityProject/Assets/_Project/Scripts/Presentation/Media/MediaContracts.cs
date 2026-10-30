// 说明：媒体与教学检测通过会话和请求双标识回报，只有真实完成才能确认播放。
using System;
namespace Zhijian.Presentation.Media
{
    public enum MediaDisplayKind { Audio, Video, Image }
    public enum PlaybackPhase { Preparing, Playing, Completed, Cancelled, Failed }
    public sealed class MediaRequest
    {
        public string CaseId { get; }
        public string MediaId { get; }
        public MediaDisplayKind Kind { get; }
        public Guid SessionId { get; }
        public Guid RequestId { get; }
        public MediaRequest(string caseId, string mediaId, MediaDisplayKind kind, Guid sessionId, Guid requestId)
        { CaseId = caseId; MediaId = mediaId; Kind = kind; SessionId = sessionId; RequestId = requestId; }
    }
    public sealed class PlaybackUpdate
    {
        public MediaRequest Request { get; }
        public PlaybackPhase Phase { get; }
        public string Message { get; }
        public PlaybackUpdate(MediaRequest request, PlaybackPhase phase, string message)
        { Request = request; Phase = phase; Message = message; }
    }
    public interface IMediaPlayback
    {
        event Action<PlaybackUpdate> Changed;
        void Play(MediaRequest request);
        void Stop();
    }
    public interface ITeachingDetection
    {
        event Action<PlaybackUpdate> Changed;
        void Begin(MediaRequest request);
        void Cancel();
    }
}
