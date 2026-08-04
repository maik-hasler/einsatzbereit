using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.SearchAlerts;

// One active alert per user (einsatzbereit#1090) - re-saving replaces the previous
// criteria in place via ReplaceCriteria rather than deleting+recreating the row, and
// resets LastNotifiedAt so nothing that already existed under the old (or same)
// criteria before the save is reported as a "new" match afterwards.
public sealed class SearchAlert
	: AggregateRoot<SearchAlertId>,
		IAuditableEntity
{
	public UserId UserId { get; private set; }

	public Occurrence? Occurrence { get; private set; }

	public ParticipationType? ParticipationType { get; private set; }

	public bool? IsRemote { get; private set; }

	public double? CenterLatitude { get; private set; }

	public double? CenterLongitude { get; private set; }

	public double? RadiusKm { get; private set; }

	private List<string> _categories = [];

	public IReadOnlyList<string> Categories => _categories.AsReadOnly();

	public string? Tag { get; private set; }

	// Cursor for SearchAlertDigestJob (#1090): only opportunities published after this
	// point are considered "new" for this alert. Advanced on every digest tick
	// regardless of whether that tick found a match, not just when it did - otherwise
	// a never-matching alert would force the job to keep rescanning its entire history
	// forever instead of a bounded recent window.
	public DateTimeOffset LastNotifiedAt { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private SearchAlert() : base(default) { }
#pragma warning restore CS8618

	private SearchAlert(
		SearchAlertId id,
		UserId userId,
		Occurrence? occurrence,
		ParticipationType? participationType,
		bool? isRemote,
		double? centerLatitude,
		double? centerLongitude,
		double? radiusKm,
		IReadOnlyCollection<string> categories,
		string? tag,
		DateTimeOffset now)
		: base(id)
	{
		UserId = userId;
		Occurrence = occurrence;
		ParticipationType = participationType;
		IsRemote = isRemote;
		CenterLatitude = centerLatitude;
		CenterLongitude = centerLongitude;
		RadiusKm = radiusKm;
		_categories = [.. categories];
		Tag = tag;
		LastNotifiedAt = now;
	}

	public static SearchAlert Create(
		UserId userId,
		Occurrence? occurrence,
		ParticipationType? participationType,
		bool? isRemote,
		double? centerLatitude,
		double? centerLongitude,
		double? radiusKm,
		IReadOnlyCollection<string>? categories = null,
		string? tag = null,
		DateTimeOffset? now = null) =>
		new(
			SearchAlertId.New(),
			userId,
			occurrence,
			participationType,
			isRemote,
			centerLatitude,
			centerLongitude,
			radiusKm,
			categories ?? [],
			tag,
			now ?? DateTimeOffset.UtcNow);

	public void ReplaceCriteria(
		Occurrence? occurrence,
		ParticipationType? participationType,
		bool? isRemote,
		double? centerLatitude,
		double? centerLongitude,
		double? radiusKm,
		IReadOnlyCollection<string>? categories,
		string? tag,
		DateTimeOffset? now = null)
	{
		Occurrence = occurrence;
		ParticipationType = participationType;
		IsRemote = isRemote;
		CenterLatitude = centerLatitude;
		CenterLongitude = centerLongitude;
		RadiusKm = radiusKm;
		_categories = [.. categories ?? []];
		Tag = tag;
		LastNotifiedAt = now ?? DateTimeOffset.UtcNow;
	}

	private bool HasRadius => CenterLatitude is not null && CenterLongitude is not null && RadiusKm is not null;

	// Date filters (DateFrom/DateTo) are deliberately not part of this criteria set
	// (#1090) - a date window fixed at save time would go stale for opportunities
	// published afterwards, since there's no relative "next N days" to re-anchor.
	public bool Matches(VolunteerOpportunity opportunity)
	{
		if (Occurrence is { } occurrence && opportunity.Occurrence != occurrence)
			return false;

		if (ParticipationType is { } participationType && opportunity.ParticipationType != participationType)
			return false;

		if (IsRemote is { } isRemote && opportunity.IsRemote != isRemote)
			return false;

		if (_categories.Count > 0 &&
			(opportunity.Category is null || !_categories.Contains(opportunity.Category.Value.ToString())))
			return false;

		if (Tag is { } tag && !opportunity.Tags.Contains(tag))
			return false;

		if (HasRadius)
		{
			if (opportunity.Address?.Latitude is not double latitude || opportunity.Address?.Longitude is not double longitude)
				return false;

			var distanceKm = GeoMath.DistanceKm(CenterLatitude!.Value, CenterLongitude!.Value, latitude, longitude);
			if (distanceKm > RadiusKm!.Value)
				return false;
		}

		return true;
	}
}
