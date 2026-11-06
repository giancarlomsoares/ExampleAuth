using ExampleAuth.Server.Data;
using ExampleAuth.Server.Models;

namespace ExampleAuth.Server.Services;

public class LoginAuditService(ExampleAuthDbContext db)
{
    public async Task RecordAsync(
        LoginAuditEventType eventType,
        LoginAuditStatus status,
        string ipAddress,
        string? userAgent,
        string identifierAttempted,
        Guid? userId = null,
        string? clientId = null,
        string? failureReason = null)
    {
        db.LoginAudits.Add(new LoginAudit
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            UserId = userId,
            IdentifierAttempted = identifierAttempted,
            EventType = eventType,
            Status = status,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ClientId = clientId,
        });

        await db.SaveChangesAsync();
    }
}
