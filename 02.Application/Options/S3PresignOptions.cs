namespace expense_tracker_backend.Application.Options;

/// <summary>Binds from configuration section <c>AWS:S3</c> (shares keys with infrastructure S3 settings).</summary>
public class S3PresignOptions
{
    public int PresignedUrlExpiryMinutes { get; set; } = 5;
}
