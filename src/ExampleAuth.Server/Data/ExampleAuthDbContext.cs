using ExampleAuth.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExampleAuth.Server.Data;

public class ExampleAuthDbContext(DbContextOptions<ExampleAuthDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<ClientAccessGrant> ClientAccessGrants => Set<ClientAccessGrant>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Identity and OpenIddict set their table names explicitly (via ToTable),
        // which EFCore.NamingConventions leaves untouched by design (it only rewrites
        // convention-derived names). Force those to snake_case too so every table in
        // the schema follows the same convention, not just the ones we define ourselves.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is not null)
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }
        }

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(u => u.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClientAccessGrant>(entity =>
        {
            // Filtrado (não um índice único simples): só há no máximo um grant ATIVO por
            // par usuário+client, mas grants revogados se acumulam como histórico e não
            // podem colidir com esse índice.
            entity.HasIndex(g => new { g.UserId, g.ApplicationId })
                .IsUnique()
                .HasFilter("revoked_at_utc IS NULL");
            entity.HasIndex(g => g.ApplicationId);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(g => g.GrantedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(g => g.RevokedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoginAudit>(entity =>
        {
            entity.Property(a => a.EventType).HasConversion<string>();
            entity.Property(a => a.Status).HasConversion<string>();

            entity.HasIndex(a => a.OccurredAtUtc);
            entity.HasIndex(a => a.UserId);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static string ToSnakeCase(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToLowerInvariant();
}
