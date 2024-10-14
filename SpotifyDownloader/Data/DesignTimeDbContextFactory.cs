using Microsoft.EntityFrameworkCore.Design;

namespace SpotifyDownloader.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new();
}

