using Microsoft.EntityFrameworkCore;
using Service.Models;

namespace Service.Contexts;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>()
                    .ToTable("ApiKey")
                    .HasIndex(x => x.KeyPrefix)
                    .IsUnique();
    }
}