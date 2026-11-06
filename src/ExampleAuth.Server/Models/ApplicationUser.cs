using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ExampleAuth.Server.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
    public Guid? DisabledByUserId { get; set; }

    [NotMapped]
    public bool IsDisabled => DisabledAtUtc.HasValue;
}
