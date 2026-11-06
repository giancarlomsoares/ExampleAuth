namespace ExampleAuth.Server.Models;

public enum LoginAuditEventType
{
    PasswordLogin,
    ClientAuthorization,
    TokenIssued,
    TokenRefreshed,
    ClientCredentialsToken,
    PasswordResetRequested,
    PasswordResetCompleted,
    Logout,
    UserDisabled,
    ClientAccessRevoked,
}

public enum LoginAuditStatus
{
    Success,
    Failure,
}

public class LoginAudit
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public string IdentifierAttempted { get; set; } = string.Empty;
    public LoginAuditEventType EventType { get; set; }
    public LoginAuditStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? ClientId { get; set; }
}
