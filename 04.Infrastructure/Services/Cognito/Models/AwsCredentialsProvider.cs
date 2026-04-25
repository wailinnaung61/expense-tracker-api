using Amazon.Runtime;

namespace expense_tracker_backend.Infrastructure.AWS.Configuration;

/// <summary>
/// Resolves AWS credentials: optional static keys in config, otherwise the SDK default chain
/// (shared credentials file, env vars, web identity / IRSA, EC2 instance profile, ECS task role, etc.).
/// </summary>
public static class AwsCredentialsProvider
{
    public static AWSCredentials Resolve(AwsSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.AccessKey) &&
            !string.IsNullOrWhiteSpace(settings.SecretKey))
            return new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);

        return FallbackCredentialsFactory.GetCredentials();
    }
}
