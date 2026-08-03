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

	// Reverse of the storage backend's own (private, backend-specific) public
	// URL builder - lets a caller that only has a previously stored public URL
	// (e.g. User.AvatarUrl) recover the exact object key to delete, instead of
	// guessing it back from a naming convention. Returns null if the URL
	// doesn't match this service's own public URL format.
	string? GetObjectKeyFromPublicUrl(string publicUrl);

	// Throws if the storage backend is unreachable; used by StorageHealthCheck
	// (Api/Common/Health) to back the "ready" readiness probe (#1081).
	Task PingAsync(CancellationToken cancellationToken = default);
}
