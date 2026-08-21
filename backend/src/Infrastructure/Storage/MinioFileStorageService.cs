using Application.Common.Storage;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Infrastructure.Storage;

internal sealed class MinioFileStorageService : IFileStorageService
{
	// Object keys are stable for some callers (e.g. "organization-logos/{OrganizationId}{ext}"),
	// so a long max-age would serve a stale image after re-upload. A moderate TTL plus
	// the "?v=" cache-busting query param below (which changes on every
	// upload) keeps caching effective without that staleness risk.
	internal const string CacheControlHeaderValue = "public, max-age=3600";

	// Bucket policy only grants public read under this prefix, not the whole bucket, so a
	// future non-public object type (participant lists, ID scans) can be added outside it
	// with no policy change. Transparent to callers - GetPublicUrl already returns the
	// full URL - but changing this value only affects future uploads; existing objects
	// stay under the old prefix, which is acceptable on this repo's disposable,
	// resettable demo environment (not worth a migration script pre-1.0).
	private const string PublicPrefix = "public/";

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
				.WithObject(PublicPrefix + objectKey)
				.WithStreamData(content)
				.WithObjectSize(size)
				.WithContentType(contentType)
				.WithHeaders(new Dictionary<string, string>
				{
					["Cache-Control"] = CacheControlHeaderValue,
				}),
			cancellationToken);

		return AppendVersionQuery(GetPublicUrl(objectKey), DateTimeOffset.UtcNow);
	}

	public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
	{
		await EnsureBucketReadyAsync(cancellationToken);

		await _minio.RemoveObjectAsync(
			new RemoveObjectArgs()
				.WithBucket(_settings.BucketName)
				.WithObject(PublicPrefix + objectKey),
			cancellationToken);
	}

	// Not on IFileStorageService (#1011 - no callers through the interface);
	// internal rather than private so GetObjectKeyFromPublicUrl's inverse-of-this
	// relationship stays directly testable from IntegrationTests.
	internal string GetPublicUrl(string objectKey)
	{
		var baseUrl = (_settings.PublicEndpoint ?? _settings.Endpoint).TrimEnd('/');
		return $"{baseUrl}/{_settings.BucketName}/{PublicPrefix}{objectKey}";
	}

	public string? GetObjectKeyFromPublicUrl(string publicUrl)
	{
		var prefix = GetPublicUrl(string.Empty);
		if (!publicUrl.StartsWith(prefix, StringComparison.Ordinal))
			return null;

		var withoutPrefix = publicUrl[prefix.Length..];
		var queryIndex = withoutPrefix.IndexOf('?');
		return queryIndex >= 0 ? withoutPrefix[..queryIndex] : withoutPrefix;
	}

	// BucketExistsAsync's own bool result only distinguishes "bucket present"
	// from "bucket absent" - both are a reachable server and neither should fail
	// a readiness probe on a fresh environment before EnsureBucketReadyAsync has
	// ever run. Unreachability (the thing the probe actually cares about) surfaces
	// as a thrown exception instead, which callers are expected to catch.
	public async Task PingAsync(CancellationToken cancellationToken = default) =>
		await _minio.BucketExistsAsync(
			new BucketExistsArgs().WithBucket(_settings.BucketName),
			cancellationToken);

	// Object keys don't change on re-upload, so the version query param is
	// what invalidates a browser's cached copy once the underlying object
	// changes - without it, CacheControlHeaderValue's max-age would let a
	// stale image survive a re-upload until it happened to expire.
	internal static string AppendVersionQuery(string url, DateTimeOffset uploadedOn) =>
		$"{url}?v={uploadedOn.ToUnixTimeSeconds()}";

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

			// Scoped to PublicPrefix, not the whole bucket - see that constant's
			// comment for why.
			var policy = $"{{\"Version\":\"2012-10-17\",\"Statement\":[{{\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":[\"s3:GetObject\"],\"Resource\":[\"arn:aws:s3:::{_settings.BucketName}/{PublicPrefix}*\"]}}]}}";

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
