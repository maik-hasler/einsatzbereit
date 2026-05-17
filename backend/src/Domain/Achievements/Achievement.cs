using Domain.Primitives;
using Domain.Users;

namespace Domain.Achievements;

public sealed class Achievement
	: AggregateRoot<AchievementId>,
		IAuditableEntity
{
	public UserId UserId { get; private set; }

	public AchievementType Type { get; private set; }

	public string Name { get; private set; }

	public string Description { get; private set; }

	public DateTimeOffset UnlockedAt { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private Achievement() : base(default) { }
#pragma warning restore CS8618

	private Achievement(
		AchievementId id,
		UserId userId,
		AchievementType type,
		string name,
		string description)
		: base(id)
	{
		UserId = userId;
		Type = type;
		Name = name;
		Description = description;
		UnlockedAt = DateTimeOffset.UtcNow;
	}

	public static Achievement Create(
		UserId userId,
		AchievementType type,
		string name,
		string description) =>
		new(
			new AchievementId(Guid.CreateVersion7()),
			userId,
			type,
			name,
			description);
}
