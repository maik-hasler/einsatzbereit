using Application.Common.Messaging;

namespace Application.Meta.GetVersion.v1;

public sealed record GetVersionQuery : IQuery<string>;
