namespace expense_tracker_backend.Infrastructure.AWS.Configuration;

public class AwsSettings
{
    public const string SectionName = "AWS";

    public string Region { get; set; } = "us-east-1";

    /// <summary>When both this and <see cref="SecretKey"/> are non-empty, static keys are used; otherwise the AWS SDK default credential chain (IAM role on EC2/ECS, shared profile, etc.).</summary>
    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;
    public CognitoSettings Cognito { get; set; } = new();
    public EventBridgeSettings EventBridge { get; set; } = new();
    public S3Settings S3 { get; set; } = new();
}

public class CognitoSettings
{
    public string UserPoolId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}

public class EventBridgeSettings
{
    public string EventBusName { get; set; } = "default";
    public string Source { get; set; } = "expense-tracker.export";
}

public class S3Settings
{
    public string ExportBucketName { get; set; } = string.Empty;
    public int PresignedUrlExpiryMinutes { get; set; } = 5;
}
