namespace Application.Common.Storage;

public interface IFileStorageService
{
	Task<string> UploadAsync(
		string objectKey,
		Stream content,
		long size,
		string contentType,
		CancellationToken cancellationToken = default);

	Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

	string GetPublicUrl(string objectKey);

	// Throws if the storage backend is unreachable; used by StorageHealthCheck
	// (Api/Common/Health) to back the "ready" readiness probe (#1081).
	Task PingAsync(CancellationToken cancellationToken = default);
}
