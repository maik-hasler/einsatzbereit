using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

internal sealed class StorageSettingsSetup(IConfiguration configuration)
	: IConfigureOptions<StorageSettings>
{
	public void Configure(StorageSettings options) =>
		configuration.GetSection("Storage").Bind(options);
}
