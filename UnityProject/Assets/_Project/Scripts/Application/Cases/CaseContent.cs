// 说明：保存只读案件显示材料与分开的配置、资源和发布问题，不伪造素材已就绪。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zhijian.Domain.Cases;

namespace Zhijian.Application.Cases
{
    public enum ContentProblemKind { Definition, Resource, Release }
    public enum DispositionEffect { None, RemoveSimulatedApp, InstallAndGrantSensitive }
    public sealed class ContentProblem
    {
        public ContentProblemKind Kind { get; }
        public string Code { get; }
        public string ItemId { get; }
        public string Message { get; }
        public ContentProblem(ContentProblemKind kind, string code, string itemId, string message)
        { Kind = kind; Code = code; ItemId = itemId; Message = message; }
    }
    public sealed class SourceContent
    {
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public SourceContent(string id, string title, string body) { Id = id; Title = title; Body = body; }
    }
    public sealed class DetectionFinding
    {
        public string MediaId { get; }
        public DetectionIndicatorKind Indicator { get; }
        public string Label { get; }
        public string Explanation { get; }
        public DetectionFinding(string mediaId, DetectionIndicatorKind indicator, string label, string explanation)
        { MediaId = mediaId; Indicator = indicator; Label = label; Explanation = explanation; }
    }
    public sealed class CaseContent
    {
        public CaseDefinition Definition { get; }
        public string Summary { get; }
        public IReadOnlyDictionary<string, SourceContent> Sources { get; }
        public IReadOnlyList<DetectionFinding> Findings { get; }
        public IReadOnlyDictionary<string, DispositionEffect> DispositionEffects { get; }
        public CaseContent(CaseDefinition definition, string summary, IEnumerable<SourceContent> sources,
            IEnumerable<DetectionFinding> findings, IDictionary<string, DispositionEffect> dispositionEffects)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Summary = summary ?? string.Empty;
            Sources = new ReadOnlyDictionary<string, SourceContent>(sources.ToDictionary(item => item.Id, StringComparer.Ordinal));
            Findings = Array.AsReadOnly(findings.ToArray());
            DispositionEffects = new ReadOnlyDictionary<string, DispositionEffect>(new Dictionary<string, DispositionEffect>(dispositionEffects, StringComparer.Ordinal));
        }
    }
    public sealed class CaseLoadResult
    {
        public string CaseId { get; }
        public CaseContent Content { get; }
        public IReadOnlyList<ContentProblem> Problems { get; }
        public bool DefinitionValid => Content != null && !Problems.Any(item => item.Kind == ContentProblemKind.Definition);
        public bool CanStartTraining => DefinitionValid && Problems.Count == 0;
        public CaseLoadResult(string caseId, CaseContent content, IEnumerable<ContentProblem> problems)
        { CaseId = caseId; Content = content; Problems = Array.AsReadOnly(problems.ToArray()); }
    }
}
