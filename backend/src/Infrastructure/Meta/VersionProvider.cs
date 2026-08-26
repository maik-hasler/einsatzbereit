using System.Reflection;
using Application.Meta;

namespace Infrastructure.Meta;

internal sealed class VersionProvider : IVersionProvider
{
	private readonly string _version =
		Assembly.GetEntryAssembly()?
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion
		?? "unknown";

	public string GetVersion() => _version;
}
