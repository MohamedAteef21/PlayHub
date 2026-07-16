using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PlayHub.Infrastructure.Data;

public class PlayHubDbContextFactory : IDesignTimeDbContextFactory<PlayHubDbContext>
{
    public PlayHubDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../PlayHub.Api"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var tenantContext = new TenantContext { TenantId = Guid.Empty };
        var options = new DbContextOptionsBuilder<PlayHubDbContext>()
            .UseSqlServer(configuration.GetConnectionString("HrConnection"))
            .Options;

        return new PlayHubDbContext(options, tenantContext);
    }
}
