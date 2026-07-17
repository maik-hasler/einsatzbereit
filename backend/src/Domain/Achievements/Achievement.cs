using Domain.Primitives;
using Domain.Users;

namespace Domain.Achievements;

public sealed class Achievement
	: AggregateRoot<AchievementId>,
		IAuditableEntity
{
	public UserId UserId { get; private set; }

	public AchievementType Type { get; private set; }

	public string? Key { get; private set; }

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
		string? key,
		string name,
		string description,
		DateTimeOffset unlockedAt)
		: base(id)
	{
		UserId = userId;
		Type = type;
		Key = key;
		Name = name;
		Description = description;
		UnlockedAt = unlockedAt;
	}

	public static Achievement Create(
		UserId userId,
		AchievementType type,
		string? key,
		string name,
		string description,
		DateTimeOffset unlockedAt) =>
		new(
			AchievementId.New(),
			userId,
			type,
			key,
			name,
			description,
			unlockedAt);
}
