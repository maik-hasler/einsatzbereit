using Domain.Primitives;

namespace Domain.Users;

public sealed class User
	: AggregateRoot<UserId>
{
	public string? Bio { get; private set; }

	public IReadOnlyList<string> Skills { get; private set; } = [];

	public IReadOnlyList<string> Languages { get; private set; } = [];

	public PreferredContact? PreferredContact { get; private set; }

#pragma warning disable CS8618
	private User() : base(default) { }
#pragma warning restore CS8618

	private User(UserId id) : base(id) { }

	public static User Create(UserId id) => new(id);

	public void Update(
		string? bio,
		IReadOnlyList<string> skills,
		IReadOnlyList<string> languages,
		PreferredContact? preferredContact)
	{
		Bio = bio;
		Skills = skills;
		Languages = languages;
		PreferredContact = preferredContact;
	}
}
