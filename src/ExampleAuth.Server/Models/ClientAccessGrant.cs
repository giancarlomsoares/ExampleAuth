namespace ExampleAuth.Server.Models;

public class ClientAccessGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid GrantedByUserId { get; set; }
    public DateTimeOffset GrantedAtUtc { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
