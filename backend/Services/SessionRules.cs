using Cia.Api.Entities;
using Cia.Api.Enums;

namespace Cia.Api.Services;

public static class SessionRules
{
    public static bool IsHumanSession(ConversationSession session)
    {
        if (session.Status is SessionStatus.WaitingForAgent or SessionStatus.Transferred)
        {
            return true;
        }

        var requestStatus = session.HumanAgentRequests?
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (HumanAgentRequestStatus?)r.Status)
            .FirstOrDefault();

        return requestStatus is HumanAgentRequestStatus.Waiting or HumanAgentRequestStatus.Assigned;
    }

    public static bool IsAiActive(ConversationSession session)
        => session.Status == SessionStatus.Active && !IsHumanSession(session);

    public static bool IsCompletedClosure(ConversationSession session)
        => session.ClosureReason is null or SessionClosureReason.Completed;

    public static bool CanRate(ConversationSession session)
        => session.Status == SessionStatus.Resolved
           && session.Rating is null
           && IsCompletedClosure(session);

    public static void Close(ConversationSession session, SessionClosureReason reason)
    {
        session.Status = SessionStatus.Resolved;
        session.ClosureReason = reason;
        session.UpdatedAt = DateTime.UtcNow;
    }
}
