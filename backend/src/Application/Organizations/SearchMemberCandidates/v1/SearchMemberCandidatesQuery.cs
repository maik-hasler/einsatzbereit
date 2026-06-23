using Application.Common.Messaging;

namespace Application.Organizations.SearchMemberCandidates.v1;

public sealed record SearchMemberCandidatesQuery(
	Guid OrganizationId,
	string Search)
	: IQuery<IReadOnlyList<MemberCandidateDto>>;

public sealed record MemberCandidateDto(
	Guid UserId,
	string Username,
	string? FirstName,
	string? LastName,
	string Email);
