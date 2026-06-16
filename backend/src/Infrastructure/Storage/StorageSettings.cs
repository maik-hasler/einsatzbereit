namespace Infrastructure.Storage;

internal sealed class StorageSettings
{
	public string Endpoint { get; set; } = "";
	public string AccessKey { get; set; } = "";
	public string SecretKey { get; set; } = "";
	public string BucketName { get; set; } = "";
	public string? PublicEndpoint { get; set; }
}
