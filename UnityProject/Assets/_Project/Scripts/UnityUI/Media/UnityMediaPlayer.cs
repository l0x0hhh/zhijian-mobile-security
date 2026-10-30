// 说明：图片等待页面呈现就绪并实际可见才回报；音视频退出和后台取消仍立即执行。
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Zhijian.Presentation.Media;
namespace Zhijian.UnityUI.Media
{
    public sealed class UnityMediaPlayer : MonoBehaviour, IMediaPlayback
    {
        [SerializeField] private RawImage preview;
        [SerializeField] private TMP_Text clock;
        private Func<MediaRequest, UnityMediaAsset> resolve;
        private AudioSource audioOutput;
        private VideoPlayer video;
        private RenderTexture texture;
        private MediaRequest current;
        private Coroutine pending;
        private double audioEnd;
        private float startedAt, prepareAt;
        private bool audioStarted, videoPrepared, presentationReady;
        private RectTransform visibleViewport;
        private CanvasGroup[] presentationGroups;
        private readonly Vector3[] imageCorners = new Vector3[4];
        public event Action<PlaybackUpdate> Changed;
        public void Initialize(Func<MediaRequest, UnityMediaAsset> resolver, RectTransform viewport)
        {
            resolve=resolver; visibleViewport=viewport;
            if (clock != null) clock.text="";
            audioOutput=gameObject.AddComponent<AudioSource>();
            audioOutput.playOnAwake=false; audioOutput.loop=false; audioOutput.spatialBlend=0;
        }
        public void Play(MediaRequest request)
        {
            Stop(); current=request;
            var asset=resolve?.Invoke(request);
            if (asset == null) { Finish(PlaybackPhase.Failed, "素材未就绪或尚未批准使用，请稍后再试。"); return; }
            prepareAt=Time.realtimeSinceStartup;
            Emit(PlaybackPhase.Preparing, "正在准备实际媒体…");
            if (request.Kind == MediaDisplayKind.Image)
            {
                if (asset.Image == null) { Finish(PlaybackPhase.Failed, "图片资源缺失。"); return; }
                var rect=asset.Image.textureRect;
                preview.texture=asset.Image.texture;
                preview.uvRect=new Rect(rect.x/asset.Image.texture.width, rect.y/asset.Image.texture.height, rect.width/asset.Image.texture.width, rect.height/asset.Image.texture.height);
                preview.color=Color.white; SetAspect(rect.width/rect.height);
                pending=StartCoroutine(ImageShown(request)); return;
            }
            if (request.Kind == MediaDisplayKind.Audio)
            {
                if (asset.Audio == null) { Finish(PlaybackPhase.Failed, "音频资源缺失。"); return; }
                pending=StartCoroutine(StartAudio(request, asset.Audio)); return;
            }
            if (asset.Video == null) { Finish(PlaybackPhase.Failed, "视频资源缺失。"); return; }
            video=gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake=false; video.isLooping=false; video.waitForFirstFrame=true; video.skipOnDrop=false;
            video.source=VideoSource.VideoClip; video.clip=asset.Video;
            video.renderMode=VideoRenderMode.RenderTexture; video.audioOutputMode=VideoAudioOutputMode.AudioSource;
            video.controlledAudioTrackCount=1; video.SetTargetAudioSource(0, audioOutput);
            var ownVideo=video;
            video.prepareCompleted += source => { if (current != request || source != ownVideo) return;
                texture=new RenderTexture(Mathf.Max(1, (int)source.width), Mathf.Max(1, (int)source.height), 0, RenderTextureFormat.ARGB32);
                texture.Create(); source.targetTexture=texture; preview.texture=texture; preview.uvRect=new Rect(0,0,1,1); preview.color=Color.white;
                SetAspect((float)source.width/Mathf.Max(1, source.height)); videoPrepared=true; source.Play(); Emit(PlaybackPhase.Playing, "正在播放 · 教学模拟"); };
            video.loopPointReached += source => { if (current == request && source == ownVideo && videoPrepared) Finish(PlaybackPhase.Completed, "完整播放完成。"); };
            video.errorReceived += (source, message) => { if (current == request && source == ownVideo) Finish(PlaybackPhase.Failed, "视频无法播放，请检查导入格式。"); };
            video.Prepare();
        }
        public void SetPresentationReady(bool ready)
        {
            presentationReady = ready;
            if (ready) presentationGroups = GetComponentsInParent<CanvasGroup>(true);
        }
        private IEnumerator ImageShown(MediaRequest request)
        {
            while (current == request)
            {
                yield return new WaitForEndOfFrame();
                if (current != request) yield break;
                if (!ImageVisible()) continue;
                pending = null;
                Finish(PlaybackPhase.Completed, "教学图片已显示。", true);
                yield break;
            }
        }
        private bool ImageVisible()
        {
            if (!presentationReady || !preview.isActiveAndEnabled || preview.texture == null || preview.canvasRenderer.cull || visibleViewport == null) return false;
            if (presentationGroups != null)
                foreach (var group in presentationGroups) if (group != null && group.alpha < .99f) return false;
            preview.rectTransform.GetWorldCorners(imageCorners);
            var bottom = visibleViewport.InverseTransformPoint(imageCorners[0]);
            var top = visibleViewport.InverseTransformPoint(imageCorners[2]);
            var viewport = visibleViewport.rect;
            var width = Mathf.Min(top.x, viewport.xMax) - Mathf.Max(bottom.x, viewport.xMin);
            var height = Mathf.Min(top.y, viewport.yMax) - Mathf.Max(bottom.y, viewport.yMin);
            // 大图至少覆盖可显示区域的九成，避免只露出边缘就登记图片消息已读。
            return width > 0 && height > 0 && width >= Mathf.Min(top.x - bottom.x, viewport.width) * .9f &&
                height >= Mathf.Min(top.y - bottom.y, viewport.height) * .9f;
        }
        private IEnumerator StartAudio(MediaRequest request, AudioClip clip)
        {
            clip.LoadAudioData();
            while (current == request && clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup-prepareAt < 15) yield return null;
            pending=null;
            if (current != request) yield break;
            if (clip.loadState != AudioDataLoadState.Loaded || clip.samples <= 0) { Finish(PlaybackPhase.Failed, "音频数据无法读取。"); yield break; }
            audioOutput.clip=clip; audioOutput.Play(); audioStarted=true; startedAt=Time.realtimeSinceStartup;
            audioEnd=AudioSettings.dspTime+clip.length;
            Emit(PlaybackPhase.Playing, "正在播放 · 教学模拟");
        }
        private void SetAspect(float ratio)
        { var fitter=preview.GetComponent<AspectRatioFitter>(); if (fitter != null) fitter.aspectRatio=ratio; }
        private void Update()
        {
            if (current == null) return;
            if (audioStarted)
            {
                if (clock != null) clock.text=Mathf.Min(audioOutput.clip.length, Time.realtimeSinceStartup-startedAt).ToString("0.0")+" / "+audioOutput.clip.length.ToString("0.0")+" 秒";
                if (!audioOutput.isPlaying && Time.realtimeSinceStartup-startedAt > .3f)
                    Finish(AudioSettings.dspTime >= audioEnd-.12 ? PlaybackPhase.Completed : PlaybackPhase.Failed,
                        AudioSettings.dspTime >= audioEnd-.12 ? "完整播放完成。" : "播放中断，尚未计为完成。");
            }
            else if (videoPrepared && video != null && clock != null) clock.text=video.time.ToString("0.0")+" / "+video.length.ToString("0.0")+" 秒";
            else if (current.Kind != MediaDisplayKind.Image && Time.realtimeSinceStartup-prepareAt > 20) Finish(PlaybackPhase.Failed, "媒体准备超时，请重试。");
        }
        private void Emit(PlaybackPhase phase, string message) { if (current != null) Changed?.Invoke(new PlaybackUpdate(current, phase, message)); }
        private void Finish(PlaybackPhase phase, string message, bool keepImage=false)
        {
            var request=current; current=null; Cleanup(keepImage);
            if (request != null) Changed?.Invoke(new PlaybackUpdate(request, phase, message));
        }
        private void Cleanup(bool keepImage)
        {
            if (pending != null) { StopCoroutine(pending); pending=null; }
            audioStarted=false; videoPrepared=false;
            if (audioOutput != null) { audioOutput.Stop(); audioOutput.clip=null; }
            if (video != null) { video.Stop(); video.targetTexture=null; Destroy(video); video=null; }
            if (!keepImage && preview != null) { preview.texture=null; preview.color=Color.clear; }
            if (texture != null) { texture.Release(); Destroy(texture); texture=null; }
        }
        public void Stop() { if (current != null) Finish(PlaybackPhase.Cancelled, "播放已取消，未计为完成。"); else Cleanup(false); }
        private void OnApplicationPause(bool paused) { if (paused) Stop(); }
        private void OnDisable() => Stop();
        private void OnDestroy() { Stop(); Changed=null; }
    }
}
