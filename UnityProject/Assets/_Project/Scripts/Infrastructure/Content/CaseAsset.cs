// 说明：显式引用案件 JSON 与素材，资源存在、许可确认和开放训练分开记录。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;
using Zhijian.Application.Cases;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Outcomes;

namespace Zhijian.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Zhijian/Case Content", fileName = "Case")]
    public sealed class CaseAsset : ScriptableObject
    {
        [Serializable] private sealed class MediaReference
        {
            public string mediaId;
            public bool approvedForUse;
            public AudioClip audio;
            public VideoClip video;
            public Sprite image;
            public bool HasResource(MediaKind kind)
                => kind == MediaKind.Audio ? audio != null : kind == MediaKind.Video ? video != null : image != null;
        }
        [SerializeField] private TextAsset contentJson;
        [SerializeField] private bool trainingEnabled;
        [SerializeField] private MediaReference[] mediaReferences = Array.Empty<MediaReference>();

        public bool TryGetMedia(string mediaId, out AudioClip audio, out VideoClip video, out Sprite image)
        {
            audio = null; video = null; image = null;
            var matches = (mediaReferences ?? Array.Empty<MediaReference>()).Where(item => item != null && item.mediaId == mediaId).ToArray();
            if (matches.Length != 1 || !matches[0].approvedForUse) return false;
            audio = matches[0].audio; video = matches[0].video; image = matches[0].image;
            return audio != null || video != null || image != null;
        }

        public CaseLoadResult Load(string expectedCaseId, OutcomeResolver resolver)
        {
            var problems = new List<ContentProblem>();
            CaseContent content;
            try { content = CaseAssetMapper.Map(contentJson); }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is InvalidOperationException)
            {
                problems.Add(new ContentProblem(ContentProblemKind.Definition, "content.parse", expectedCaseId, error.Message));
                return new CaseLoadResult(expectedCaseId, null, problems);
            }
            if (content.Definition.Id != expectedCaseId)
                problems.Add(new ContentProblem(ContentProblemKind.Definition, "content.case-id", expectedCaseId, "目录 ID 与内容 ID 不一致。"));
            foreach (var error in CaseDefinitionValidator.Validate(content.Definition, resolver))
                problems.Add(new ContentProblem(ContentProblemKind.Definition, error.Code, error.ItemId, error.Message));
            var refs = mediaReferences ?? Array.Empty<MediaReference>();
            foreach (var reference in refs)
                if (reference == null || !content.Definition.Media.Any(item => item.Id == reference.mediaId))
                    problems.Add(new ContentProblem(ContentProblemKind.Resource, "resource.unknown", reference?.mediaId, "素材绑定为空或引用未知媒体。"));
            foreach (var media in content.Definition.Media)
            {
                var matches = refs.Where(item => item != null && item.mediaId == media.Id).ToArray();
                if (matches.Length != 1)
                { problems.Add(new ContentProblem(ContentProblemKind.Resource, "resource.binding", media.Id, "每个媒体必须有且只有一个绑定。")); continue; }
                if (!matches[0].HasResource(media.Kind))
                    problems.Add(new ContentProblem(ContentProblemKind.Resource, "resource.missing", media.Id, "实际媒体文件尚未引入。"));
                if (!matches[0].approvedForUse)
                    problems.Add(new ContentProblem(ContentProblemKind.Resource, "resource.approval", media.Id, "素材来源与使用许可尚未确认。"));
            }
            if (!trainingEnabled)
                problems.Add(new ContentProblem(ContentProblemKind.Release, "case.preview", expectedCaseId, "完整练习尚未开放。"));
            return new CaseLoadResult(expectedCaseId, content, problems);
        }
    }
}
