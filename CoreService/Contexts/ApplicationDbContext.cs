using CoreService.Models;
using Microsoft.EntityFrameworkCore;

namespace CoreService.Contexts;

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