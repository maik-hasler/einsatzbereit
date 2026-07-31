using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.ExportMyData.v1;

public sealed record ExportMyDataQuery(UserId UserId) : IQuery<UserDataExportResponse>;
