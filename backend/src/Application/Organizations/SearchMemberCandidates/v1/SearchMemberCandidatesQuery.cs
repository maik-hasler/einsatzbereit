using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.SearchMemberCandidates.v1;

public sealed record SearchMemberCandidatesQuery(
	Guid OrganizationId,
	string Search,
	UserId RequestingUserId)
	: IQuery<IReadOnlyList<MemberCandidateDto>>;

public sealed record MemberCandidateDto(
	Guid UserId,
	string Username,
	string? FirstName,
	string? LastName,
	string Email);
