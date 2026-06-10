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
}
