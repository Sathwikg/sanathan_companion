using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence;

/// <summary>
/// Enables design-time tooling (`dotnet ef migrations`) to build the context without booting the API.
/// Uses the local dev connection string and a no-op current-user service.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=sanathana_companion;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options, new DesignTimeCurrentUserService());
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => null;
        public bool IsAuthenticated => false;
    }
}
