using Microsoft.EntityFrameworkCore;
using SpotifyDownloader.Helpers;

namespace SpotifyDownloader.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<Artist> Artists { get; set; }
    public DbSet<Album> Albums { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite($"Data Source={GlobalConfiguration.DB_PATH}");
}
