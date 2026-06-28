using Microsoft.EntityFrameworkCore.Design;

namespace SpotCrate.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new();
}

