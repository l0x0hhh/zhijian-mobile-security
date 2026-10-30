// 说明：同步处理调查事务；未选用途时可先拒绝，终局与历史事实保持一致。
using System;
using System.Collections.Generic;
using System.Linq;
using Zhijian.Domain.Cases;
using Zhijian.Domain.Outcomes;
using Zhijian.Domain.Results;
using Zhijian.Domain.Rules;

namespace Zhijian.Domain.Sessions
{
    public sealed class InvestigationSession
    {
        private readonly CaseDefinition definition;
        private readonly OutcomeResolver outcomeResolver;
        private readonly HashSet<string> viewedSources = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> evidence = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> playedMedia = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> detectedMedia = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> historicalOverreach = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, PermissionDecision> permissions = new Dictionary<string, PermissionDecision>(StringComparer.Ordinal);
        private readonly Dictionary<string, MediaTaskState> playbackTasks = new Dictionary<string, MediaTaskState>(StringComparer.Ordinal);
        private readonly Dictionary<string, MediaTaskState> detectionTasks = new Dictionary<string, MediaTaskState>(StringComparer.Ordinal);
        private readonly HashSet<Guid> usedRequestIds = new HashSet<Guid>();
        private readonly List<SessionAction> actions = new List<SessionAction>();
        private long revision;
        private SessionPhase phase = SessionPhase.Investigating;
        private string featureIntentId;
        private string verificationOptionId;
        private string riskOptionId;
        private string dispositionOptionId;
        private SimulatedInstallState installState = SimulatedInstallState.NotInstalled;
        private int currentPrivacyRisk;
        private CaseResult result;

        public Guid SessionId { get; }
        public CaseResult Result => result;

        public InvestigationSession(CaseDefinition definition, OutcomeResolver outcomeResolver)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (outcomeResolver == null) throw new ArgumentNullException(nameof(outcomeResolver));
            var errors = CaseDefinitionValidator.Validate(definition, outcomeResolver);
            if (errors.Count > 0)
                throw new ArgumentException("案件配置不可用：" + string.Join("; ", errors.Select(error => error.Code + ": " + error.Message)), nameof(definition));
            this.definition = definition;
            this.outcomeResolver = outcomeResolver;
            SessionId = Guid.NewGuid();
            featureIntentId = definition.DefaultFeatureIntentId;
            foreach (var permission in definition.Permissions)
                permissions.Add(permission.Id, PermissionDecision.Unreviewed);
            foreach (var media in definition.Media)
            {
                playbackTasks.Add(media.Id, new MediaTaskState(Guid.Empty, MediaTaskPhase.Idle, false));
                detectionTasks.Add(media.Id, new MediaTaskState(Guid.Empty, MediaTaskPhase.Idle, false));
            }
        }

        public SessionState CaptureState()
        {
            return new SessionState(SessionId, revision, definition, phase, featureIntentId, viewedSources,
                evidence, playedMedia, detectedMedia, historicalOverreach, permissions, playbackTasks,
                detectionTasks, verificationOptionId, riskOptionId, dispositionOptionId, installState,
                currentPrivacyRisk, actions, result);
        }

        public SessionCommandResult ViewSource(string sourceId)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            var source = definition.Sources.FirstOrDefault(item => item.Id == sourceId);
            if (source == null) return Reply(SessionCommandStatus.InvalidAction, "source.unknown");
            if (source.Kind == SourceKind.Media || source.Kind == SourceKind.Detection)
                return Reply(SessionCommandStatus.InvalidAction, "source.requires-successful-callback");
            if (!viewedSources.Add(sourceId)) return Reply(SessionCommandStatus.AlreadyApplied, "source.already-viewed");
            return Commit("source.view", sourceId);
        }

        public SessionCommandResult CollectEvidence(string evidenceId)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            if (!EvidenceRules.CanCollect(CaptureState(), evidenceId, out var reasonId))
                return Reply(SessionCommandStatus.InvalidAction, reasonId);
            if (!evidence.Add(evidenceId)) return Reply(SessionCommandStatus.AlreadyApplied, "evidence.already-collected");
            return Commit("evidence.collect", evidenceId);
        }

        public SessionCommandResult SelectFeatureIntent(string intentId)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            if (!definition.FeatureIntents.Any(item => item.Id == intentId))
                return Reply(SessionCommandStatus.InvalidAction, "intent.unknown");
            if (featureIntentId == intentId) return Reply(SessionCommandStatus.AlreadyApplied, "intent.unchanged");
            featureIntentId = intentId;
            return Commit("intent.select", intentId);
        }

        public SessionCommandResult SetPermission(string permissionId, PermissionDecision decision)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            var permission = definition.Permissions.FirstOrDefault(item => item.Id == permissionId);
            if (permission == null) return Reply(SessionCommandStatus.InvalidAction, "permission.unknown");
            if (decision == null || decision.State == PermissionDecisionState.Unreviewed ||
                !PermissionRules.IsAllowedScopeValid(permission, decision))
                return Reply(SessionCommandStatus.InvalidAction, "permission.invalid-decision");
            if (string.IsNullOrEmpty(featureIntentId) && decision.State == PermissionDecisionState.Allowed)
                return Reply(SessionCommandStatus.InvalidAction, "permission.select-intent-first");
            var previous = permissions[permissionId];
            if (previous.State == decision.State && previous.Scope == decision.Scope)
                return Reply(SessionCommandStatus.AlreadyApplied, "permission.unchanged");
            permissions[permissionId] = decision;
            return Commit("permission.set", permissionId, decision.State + ":" + decision.Scope);
        }

        public SessionCommandResult SelectVerification(string optionId)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            if (!definition.VerificationOptions.Any(item => item.Id == optionId))
                return Reply(SessionCommandStatus.InvalidAction, "verification.unknown");
            if (verificationOptionId == optionId) return Reply(SessionCommandStatus.AlreadyApplied, "verification.unchanged");
            verificationOptionId = optionId;
            return Commit("verification.select", optionId);
        }

        public SessionCommandResult SelectRisk(string optionId)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            if (!definition.RiskOptions.Any(item => item.Id == optionId))
                return Reply(SessionCommandStatus.InvalidAction, "risk.unknown");
            if (riskOptionId == optionId) return Reply(SessionCommandStatus.AlreadyApplied, "risk.unchanged");
            riskOptionId = optionId;
            return Commit("risk.select", optionId);
        }

        public SessionCommandResult SelectDisposition(string optionId)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            if (!definition.DispositionOptions.Any(item => item.Id == optionId))
                return Reply(SessionCommandStatus.InvalidAction, "disposition.unknown");
            if (dispositionOptionId == optionId) return Reply(SessionCommandStatus.AlreadyApplied, "disposition.unchanged");
            dispositionOptionId = optionId;
            return Commit("disposition.select", optionId);
        }

        public SessionCommandResult SetInstallation(SimulatedInstallState state)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            if (definition.Kind != CaseKind.FaceMeeting || !Enum.IsDefined(typeof(SimulatedInstallState), state))
                return Reply(SessionCommandStatus.InvalidAction, "installation.not-applicable");
            if (installState == state) return Reply(SessionCommandStatus.AlreadyApplied, "installation.unchanged");
            installState = state;
            var warning = state == SimulatedInstallState.NotInstalled
                ? "已取消下载、删除安装包或移除模拟应用；历史危险操作仍保留。"
                : "未知来源安装包存在风险；这里只改变模拟状态，安全结案前应取消、删除或移除。";
            return Commit("installation.set", state.ToString(), warning);
        }

        public SessionCommandResult BeginPlayback(Guid callbackSessionId, string mediaId, Guid requestId)
        {
            return BeginMediaTask(callbackSessionId, mediaId, requestId, false);
        }

        public SessionCommandResult CompletePlayback(Guid callbackSessionId, string mediaId, Guid requestId, bool succeeded)
        {
            return CompleteMediaTask(callbackSessionId, mediaId, requestId, succeeded, false);
        }

        public SessionCommandResult CancelPlayback(Guid callbackSessionId, string mediaId, Guid requestId)
        {
            return CancelMediaTask(callbackSessionId, mediaId, requestId, false);
        }

        public SessionCommandResult BeginDetection(Guid callbackSessionId, string mediaId, Guid requestId)
        {
            return BeginMediaTask(callbackSessionId, mediaId, requestId, true);
        }

        public SessionCommandResult CompleteDetection(Guid callbackSessionId, string mediaId, Guid requestId, bool succeeded)
        {
            return CompleteMediaTask(callbackSessionId, mediaId, requestId, succeeded, true);
        }

        public SessionCommandResult CancelDetection(Guid callbackSessionId, string mediaId, Guid requestId)
        {
            return CancelMediaTask(callbackSessionId, mediaId, requestId, true);
        }

        public SessionCommandResult Submit(long expectedRevision, bool confirmMissingEvidence = false)
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            if (expectedRevision != revision) return Reply(SessionCommandStatus.StaleRevision, "submission.stale-revision");
            var immediate = outcomeResolver.EvaluateImmediate(CaptureState());
            if (immediate != null)
            {
                Record("submission.immediate", null, null);
                Freeze(immediate);
                return Reply(SessionCommandStatus.Accepted, "session.resolved");
            }
            var check = SubmissionValidator.Validate(CaptureState());
            if (!check.IsReady)
                return SubmissionReply(SessionCommandStatus.MissingRequirements, "submission.missing-requirements", check);
            if (check.MissingKeyEvidenceCount > 0 && !confirmMissingEvidence)
                return SubmissionReply(SessionCommandStatus.ConfirmationRequired, "submission.confirm-current-evidence", check);
            Record("submission.submit", null, confirmMissingEvidence ? "已确认按当前证据提交。" : null);
            Freeze(outcomeResolver.EvaluateFinal(CaptureState()));
            return Reply(SessionCommandStatus.Accepted, "session.resolved");
        }

        public SessionCommandResult Abort()
        {
            var rejected = GuardOpen();
            if (rejected != null) return rejected;
            phase = SessionPhase.Aborted;
            InvalidateRunningTasks();
            Record("session.abort", null, "调查未结算，不产生 CaseResult；重开必须创建新会话。");
            return Reply(SessionCommandStatus.Accepted, "session.aborted");
        }

        private SessionCommandResult BeginMediaTask(Guid callbackSessionId, string mediaId, Guid requestId, bool detection)
        {
            var rejected = GuardCallbackSession(callbackSessionId);
            if (rejected != null) return rejected;
            var media = definition.Media.FirstOrDefault(item => item.Id == mediaId);
            if (media == null || (detection && media.DetectionIndicators.Count == 0))
                return Reply(SessionCommandStatus.InvalidAction, "media.task-not-applicable");
            if (requestId == Guid.Empty) return Reply(SessionCommandStatus.InvalidAction, "media.empty-request-id");
            var tasks = detection ? detectionTasks : playbackTasks;
            var completed = detection ? detectedMedia : playedMedia;
            var previous = tasks[mediaId];
            if (previous.Phase == MediaTaskPhase.Running && previous.RequestId == requestId)
                return Reply(SessionCommandStatus.AlreadyApplied, "media.request-already-running");
            if (!usedRequestIds.Add(requestId))
                return Reply(SessionCommandStatus.StaleRequest, "media.request-id-already-used");
            tasks[mediaId] = new MediaTaskState(requestId, MediaTaskPhase.Running, completed.Contains(mediaId));
            return Commit(detection ? "detection.begin" : "playback.begin", mediaId, requestId.ToString());
        }

        private SessionCommandResult CompleteMediaTask(Guid callbackSessionId, string mediaId, Guid requestId,
            bool succeeded, bool detection)
        {
            var rejected = GuardCallbackSession(callbackSessionId);
            if (rejected != null) return rejected;
            var tasks = detection ? detectionTasks : playbackTasks;
            if (string.IsNullOrEmpty(mediaId) || !tasks.TryGetValue(mediaId, out var task) ||
                requestId == Guid.Empty || task.RequestId != requestId)
                return Reply(SessionCommandStatus.StaleRequest, "media.stale-request");
            if (task.Phase != MediaTaskPhase.Running)
                return Reply(task.Phase == MediaTaskPhase.Completed && succeeded
                    ? SessionCommandStatus.AlreadyApplied : SessionCommandStatus.StaleRequest, "media.request-already-ended");
            var completed = detection ? detectedMedia : playedMedia;
            if (succeeded)
            {
                completed.Add(mediaId);
                var sourceKind = detection ? SourceKind.Detection : SourceKind.Media;
                foreach (var source in definition.Sources.Where(item => item.Kind == sourceKind && item.MediaId == mediaId))
                    viewedSources.Add(source.Id);
            }
            tasks[mediaId] = new MediaTaskState(requestId,
                succeeded ? MediaTaskPhase.Completed : MediaTaskPhase.Failed, completed.Contains(mediaId));
            return Commit(detection ? "detection.complete" : "playback.complete", mediaId,
                succeeded ? "成功；仅确认教学素材或模拟检测结果可用，不自动收集证据。" : "失败；未伪造已完成标记。");
        }

        private SessionCommandResult CancelMediaTask(Guid callbackSessionId, string mediaId, Guid requestId, bool detection)
        {
            var rejected = GuardCallbackSession(callbackSessionId);
            if (rejected != null) return rejected;
            var tasks = detection ? detectionTasks : playbackTasks;
            if (string.IsNullOrEmpty(mediaId) || !tasks.TryGetValue(mediaId, out var task) ||
                requestId == Guid.Empty || task.RequestId != requestId || task.Phase != MediaTaskPhase.Running)
                return Reply(SessionCommandStatus.StaleRequest, "media.stale-request");
            tasks[mediaId] = new MediaTaskState(Guid.Empty,
                task.HasCompletedResult ? MediaTaskPhase.Completed : MediaTaskPhase.Idle, task.HasCompletedResult);
            return Commit(detection ? "detection.cancel" : "playback.cancel", mediaId, "旧请求失效，保留此前成功完成标记与已收集证据。");
        }

        private SessionCommandResult Commit(string actionId, string itemId, string detail = null)
        {
            Record(actionId, itemId, detail);
            foreach (var permission in definition.Permissions)
            {
                if (PermissionRules.IsOverreach(permission, featureIntentId, permissions[permission.Id]) &&
                    historicalOverreach.Add(permission.Id))
                    actions.Add(new SessionAction(revision, "permission.first-overreach", permission.Id,
                        "首次超出当前用途或范围；权限维度扣 5 分，撤销或切换用途不会删除此事实。"));
            }
            currentPrivacyRisk = PermissionRules.CalculateRisk(definition, featureIntentId, permissions);
            var immediate = outcomeResolver.EvaluateImmediate(CaptureState());
            if (immediate != null) Freeze(immediate);
            return Reply(SessionCommandStatus.Accepted, result == null ? "action.accepted" : "session.resolved");
        }

        private void Record(string actionId, string itemId, string detail)
        {
            revision++;
            actions.Add(new SessionAction(revision, actionId, itemId, detail));
        }

        private void Freeze(OutcomeDecision decision)
        {
            if (result != null) return;
            if (decision == null) throw new InvalidOperationException("终局策略未返回结局。");
            phase = SessionPhase.Resolved;
            InvalidateRunningTasks();
            var frozenState = CaptureState();
            result = new CaseResult(Guid.NewGuid(), frozenState, decision, ScoringRules.Calculate(frozenState));
        }

        private void InvalidateRunningTasks()
        {
            foreach (var tasks in new[] { playbackTasks, detectionTasks })
            {
                foreach (var mediaId in tasks.Keys.ToArray())
                {
                    var task = tasks[mediaId];
                    if (task.Phase == MediaTaskPhase.Running)
                        tasks[mediaId] = new MediaTaskState(Guid.Empty,
                            task.HasCompletedResult ? MediaTaskPhase.Completed : MediaTaskPhase.Idle, task.HasCompletedResult);
                }
            }
        }

        private SessionCommandResult GuardOpen()
        {
            return phase == SessionPhase.Investigating ? null : Reply(SessionCommandStatus.SessionClosed, "session.closed");
        }

        private SessionCommandResult GuardCallbackSession(Guid callbackSessionId)
        {
            return callbackSessionId != SessionId
                ? Reply(SessionCommandStatus.StaleSession, "session.stale-id") : GuardOpen();
        }

        private SessionCommandResult Reply(SessionCommandStatus status, string reasonId)
        {
            return new SessionCommandResult(status, reasonId, revision, result);
        }

        private SessionCommandResult SubmissionReply(SessionCommandStatus status, string reasonId, SubmissionCheck check)
        {
            return new SessionCommandResult(status, reasonId, revision, result, check.MissingRequirements,
                check.MissingKeyEvidenceCount, check.PendingSourceIds);
        }
    }
}
