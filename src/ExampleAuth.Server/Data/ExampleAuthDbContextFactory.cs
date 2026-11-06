using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExampleAuth.Server.Data;

// Used only by `dotnet ef` design-time tools (migrations add/update) without
// starting the full application. Mirrors Program.cs's configuration layering
// (appsettings.json -> appsettings.{env}.json -> env vars)
// so `dotnet ef` picks up the same connection string the app would use.
public class ExampleAuthDbContextFactory : IDesignTimeDbContextFactory<ExampleAuthDbContext>
{
    public ExampleAuthDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Database=exampleauth;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ExampleAuthDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseOpenIddict<Guid>();
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new ExampleAuthDbContext(optionsBuilder.Options);
    }
}
