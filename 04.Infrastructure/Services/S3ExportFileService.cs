using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _04.Infrastructure.Services;

public class S3ExportFileService : IExportFileService
{
    private readonly IAmazonS3 _s3;
    private readonly AwsSettings _aws;
    private readonly ILogger<S3ExportFileService> _logger;

    public S3ExportFileService(
        IAmazonS3 s3,
        IOptions<AwsSettings> awsOptions,
        ILogger<S3ExportFileService> logger)
    {
        _s3 = s3;
        _aws = awsOptions.Value;
        _logger = logger;
    }

    public async Task<string?> GenerateDownloadUrlAsync(string s3Key, int expiryMinutes)
    {
        try
        {
            var url = await _s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = _aws.S3.ExportBucketName,
                Key        = s3Key,
                Expires    = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Verb       = HttpVerb.GET
            });
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate pre-signed URL for {S3Key}", s3Key);
            return null;
        }
    }

    public async Task UploadObjectAsync(string key, byte[] body, string contentType,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream(body, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _aws.S3.ExportBucketName,
            Key = key,
            InputStream = ms,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        var response = await _s3.PutObjectAsync(request, cancellationToken);
        if (response.HttpStatusCode != HttpStatusCode.OK)
            _logger.LogWarning("S3 PutObject returned {Status} for key {Key}", response.HttpStatusCode, key);
    }
}
