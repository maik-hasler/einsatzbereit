namespace Infrastructure.Common;

internal sealed class ApiOptions
{
	public string PublicBaseUrl { get; set; } = "http://localhost:5000";

	public string FrontendBaseUrl { get; set; } = "http://localhost:4321";
}
