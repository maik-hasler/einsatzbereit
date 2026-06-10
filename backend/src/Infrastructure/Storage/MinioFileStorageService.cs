using Application.Common.Storage;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.Storage;

internal sealed class MinioFileStorageService : IFileStorageService
{
	private readonly IMinioClient _minio;
	private readonly StorageSettings _settings;
	private static readonly SemaphoreSlim _initLock = new(1, 1);
	private static bool _bucketReady;

	public MinioFileStorageService(IOptions<StorageSettings> settings)
	{
		_settings = settings.Value;

		var endpoint = _settings.Endpoint.TrimEnd('/');
		if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
		{
			_minio = new MinioClient()
				.WithEndpoint(uri.Host, uri.Port)
				.WithCredentials(_settings.AccessKey, _settings.SecretKey)
				.WithSSL(uri.Scheme == "https")
				.Build();
		}
		else
		{
			_minio = new MinioClient()
				.WithEndpoint(endpoint)
				.WithCredentials(_settings.AccessKey, _settings.SecretKey)
				.Build();
		}
	}

	public async Task<string> UploadAsync(
		string objectKey,
		Stream content,
		long size,
		string contentType,
		CancellationToken cancellationToken = default)
	{
		await EnsureBucketReadyAsync(cancellationToken);

		await _minio.PutObjectAsync(
			new PutObjectArgs()
				.WithBucket(_settings.BucketName)
				.WithObject(objectKey)
				.WithStreamData(content)
				.WithObjectSize(size)
				.WithContentType(contentType),
			cancellationToken);

		return GetPublicUrl(objectKey);
	}

	public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
	{
		await EnsureBucketReadyAsync(cancellationToken);

		await _minio.RemoveObjectAsync(
			new RemoveObjectArgs()
				.WithBucket(_settings.BucketName)
				.WithObject(objectKey),
			cancellationToken);
	}

	public string GetPublicUrl(string objectKey)
	{
		var baseUrl = (_settings.PublicEndpoint ?? _settings.Endpoint).TrimEnd('/');
		return $"{baseUrl}/{_settings.BucketName}/{objectKey}";
	}

	private async Task EnsureBucketReadyAsync(CancellationToken cancellationToken)
	{
		if (_bucketReady) return;

		await _initLock.WaitAsync(cancellationToken);
		try
		{
			if (_bucketReady) return;

			var exists = await _minio.BucketExistsAsync(
				new BucketExistsArgs().WithBucket(_settings.BucketName),
				cancellationToken);

			if (!exists)
			{
				await _minio.MakeBucketAsync(
					new MakeBucketArgs().WithBucket(_settings.BucketName),
					cancellationToken);
			}

			var policy = $$"""
				{
				  "Version": "2012-10-17",
				  "Statement": [{
				    "Effect": "Allow",
				    "Principal": "*",
				    "Action": ["s3:GetObject"],
				    "Resource": ["arn:aws:s3:::{{_settings.BucketName}}/*"]
				  }]
				}
				""";

			await _minio.SetPolicyAsync(
				new SetPolicyArgs()
					.WithBucket(_settings.BucketName)
					.WithPolicy(policy),
				cancellationToken);

			_bucketReady = true;
		}
		finally
		{
			_initLock.Release();
		}
	}
}
