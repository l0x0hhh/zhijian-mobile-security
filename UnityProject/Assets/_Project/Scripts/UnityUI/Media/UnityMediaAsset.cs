// 说明：在装配边界传入实际 Unity 媒体，业务与展示契约不依赖引擎资源。
using UnityEngine;
using UnityEngine.Video;
namespace Zhijian.UnityUI.Media
{
    public sealed class UnityMediaAsset
    {
        public AudioClip Audio { get; }
        public VideoClip Video { get; }
        public Sprite Image { get; }
        public UnityMediaAsset(AudioClip audio, VideoClip video, Sprite image) { Audio=audio; Video=video; Image=image; }
    }
}
