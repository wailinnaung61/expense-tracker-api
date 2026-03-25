namespace expense_tracker_backend.Infrastructure.AWS.Configuration;

public class AwsSettings
{
    public const string SectionName = "AWS";

    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public CognitoSettings Cognito { get; set; } = new();
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
