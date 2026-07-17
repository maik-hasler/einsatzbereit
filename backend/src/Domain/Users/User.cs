using Domain.Primitives;

namespace Domain.Users;

public sealed class User
	: AggregateRoot<UserId>
{
	private List<string> _skills = [];

	private List<string> _languages = [];

	public string? AvatarUrl { get; private set; }

	public string? Bio { get; private set; }

	public IReadOnlyList<string> Skills => _skills.AsReadOnly();

	public IReadOnlyList<string> Languages => _languages.AsReadOnly();

	public PreferredContact? PreferredContact { get; private set; }

#pragma warning disable CS8618
	private User() : base(default) { }
#pragma warning restore CS8618

	private User(UserId id) : base(id) { }

	public static User Create(UserId id) => new(id);

	public void SetAvatarUrl(string? url)
	{
		AvatarUrl = url;
	}

	public void ChangeBio(string? bio)
	{
		Bio = bio;
	}

	public void UpdateSkills(IReadOnlyCollection<string> skills)
	{
		_skills = new List<string>(skills);
	}

	public void UpdateLanguages(IReadOnlyCollection<string> languages)
	{
		_languages = new List<string>(languages);
	}

	public void SetPreferredContact(PreferredContact? preferredContact)
	{
		PreferredContact = preferredContact;
	}
}
