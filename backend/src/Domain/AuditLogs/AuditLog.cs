using Domain.Primitives;
using Domain.Users;

namespace Domain.AuditLogs;

public sealed class AuditLog
	: AggregateRoot<AuditLogId>,
		IAuditableEntity
{
	public const int MaxReasonLength = 500;

	public UserId ActorUserId { get; private set; }

	public AuditActionType ActionType { get; private set; }

	public AuditSubjectType SubjectType { get; private set; }

	public Guid SubjectId { get; private set; }

	public string? Reason { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private AuditLog() : base(default) { }
#pragma warning restore CS8618

	private AuditLog(
		AuditLogId id,
		UserId actorUserId,
		AuditActionType actionType,
		AuditSubjectType subjectType,
		Guid subjectId,
		string? reason)
		: base(id)
	{
		ActorUserId = actorUserId;
		ActionType = actionType;
		SubjectType = subjectType;
		SubjectId = subjectId;
		Reason = reason;
	}

	public static AuditLog Create(
		UserId actorUserId,
		AuditActionType actionType,
		AuditSubjectType subjectType,
		Guid subjectId,
		string? reason = null) =>
		new(AuditLogId.New(), actorUserId, actionType, subjectType, subjectId, reason);
}
