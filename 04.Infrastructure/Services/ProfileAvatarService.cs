using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace _04.Infrastructure.Services;

public class ProfileAvatarService : IProfileAvatarService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private const long MaxBytes = 2 * 1024 * 1024;

    private readonly IExportFileService _s3;
    private readonly AwsSettings _aws;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProfileAvatarService> _logger;

    public ProfileAvatarService(
        IExportFileService s3,
        IOptions<AwsSettings> awsOptions,
        IWebHostEnvironment env,
        ILogger<ProfileAvatarService> logger)
    {
        _s3 = s3;
        _aws = awsOptions.Value;
        _env = env;
        _logger = logger;
    }

    public IReadOnlyList<AvatarPresetDto> ListPresets(string publicBaseUrl) =>
        AvatarPresets.All
            .Select(p => new AvatarPresetDto(
                p.Id,
                p.Label,
                p.AccentColor,
                Combine(publicBaseUrl, AvatarPresets.RelativePath(p.Id))))
            .ToList();

    public async Task<AvatarDto> ResolveAsync(MemberProfile profile, string publicBaseUrl)
    {
        if (string.Equals(profile.AvatarSource, AvatarSources.Upload, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(profile.AvatarStorageKey))
        {
            var url = await ResolveUploadUrlAsync(profile.AvatarStorageKey!, publicBaseUrl);
            if (!string.IsNullOrWhiteSpace(url))
                return new AvatarDto(AvatarSources.Upload, profile.AvatarPresetId, url);
        }

        var presetId = AvatarPresets.IsValid(profile.AvatarPresetId)
            ? profile.AvatarPresetId
            : AvatarPresets.DefaultId;

        return new AvatarDto(
            AvatarSources.Preset,
            presetId,
            Combine(publicBaseUrl, AvatarPresets.RelativePath(presetId)));
    }

    public Task<MemberProfile> SelectPresetAsync(MemberProfile profile, string presetId)
    {
        if (!AvatarPresets.IsValid(presetId))
            throw new ArgumentException("Invalid avatar preset id.");

        profile.AvatarSource = AvatarSources.Preset;
        profile.AvatarPresetId = presetId;
        profile.AvatarStorageKey = null;
        return Task.FromResult(profile);
    }

    public async Task<MemberProfile> UploadAsync(
        MemberProfile profile, Stream fileStream, string contentType, string fileName)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException("Only JPEG, PNG, WebP, or GIF images are allowed.");

        await using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        if (ms.Length == 0)
            throw new ArgumentException("Empty image file.");
        if (ms.Length > MaxBytes)
            throw new ArgumentException("Image must be 2 MB or smaller.");

        var ext = contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        var key = $"avatars/{profile.UserId}/{Guid.NewGuid():N}{ext}";
        var bytes = ms.ToArray();

        if (!string.IsNullOrWhiteSpace(_aws.S3.ExportBucketName))
        {
            await _s3.UploadObjectAsync(key, bytes, contentType);
            profile.AvatarStorageKey = key;
            _logger.LogInformation("Uploaded avatar to S3 key {Key} for user {UserId}", key, profile.UserId);
        }
        else
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var physicalDir = Path.Combine(webRoot, "uploads", "avatars", profile.UserId);
            Directory.CreateDirectory(physicalDir);
            var physicalPath = Path.Combine(physicalDir, Path.GetFileName(key));
            await File.WriteAllBytesAsync(physicalPath, bytes);
            profile.AvatarStorageKey = $"/uploads/{key}";
            _logger.LogInformation("Saved avatar locally for user {UserId}", profile.UserId);
        }

        profile.AvatarSource = AvatarSources.Upload;
        return profile;
    }

    public Task<MemberProfile> ClearUploadAsync(MemberProfile profile)
    {
        profile.AvatarSource = AvatarSources.Preset;
        if (!AvatarPresets.IsValid(profile.AvatarPresetId))
            profile.AvatarPresetId = AvatarPresets.DefaultId;
        profile.AvatarStorageKey = null;
        return Task.FromResult(profile);
    }

    private async Task<string?> ResolveUploadUrlAsync(string storageKey, string publicBaseUrl)
    {
        if (storageKey.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || storageKey.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var path = storageKey.StartsWith('/') ? storageKey : "/" + storageKey;
            return Combine(publicBaseUrl, path);
        }

        if (!string.IsNullOrWhiteSpace(_aws.S3.ExportBucketName))
        {
            var expiry = Math.Clamp(_aws.S3.PresignedUrlExpiryMinutes, 5, 10080);
            return await _s3.GenerateDownloadUrlAsync(storageKey, expiry);
        }

        return Combine(publicBaseUrl, storageKey.StartsWith('/') ? storageKey : "/" + storageKey);
    }

    private static string Combine(string baseUrl, string path)
    {
        var b = (baseUrl ?? string.Empty).TrimEnd('/');
        var p = path.StartsWith('/') ? path : "/" + path;
        return string.IsNullOrEmpty(b) ? p : b + p;
    }
}
