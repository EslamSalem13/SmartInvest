using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartInvest.Infrastructure.Data;

/// <summary>
/// Keeps EF tools independent from the running API host and its Windows event-log
/// provider while still reading exactly the API's runtime connection configuration.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var apiDirectory = new[]
        {
            Path.Combine(currentDirectory, "Backend", "src", "SmartInvest.API"),
            Path.Combine(currentDirectory, "src", "SmartInvest.API"),
            Path.GetFullPath(Path.Combine(currentDirectory, "..", "SmartInvest.API")),
            currentDirectory,
        }.FirstOrDefault(path => File.Exists(Path.Combine(path, "appsettings.json")))
            ?? throw new InvalidOperationException("Could not locate SmartInvest.API/appsettings.json for EF design-time operations.");

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
