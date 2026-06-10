using EFTranslatable;
using EFTranslatable.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EFTranslatable.Tests;

public class Product : HasTranslations<Product>
{
    public int Id { get; set; }
    public Translatable Label { get; set; } = new();
}

public class OptionalProduct : HasTranslations<OptionalProduct>
{
    public int Id { get; set; }
    public Translatable? Label { get; set; }
}

public sealed class SqliteProductContext : DbContext
{
    public SqliteProductContext(DbContextOptions<SqliteProductContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<OptionalProduct> OptionalProducts => Set<OptionalProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().ToTable("Products");
        modelBuilder.Entity<OptionalProduct>().ToTable("OptionalProducts");
        modelBuilder.WithTranslatable(this);
    }
}

public sealed class SqlServerProductContext : DbContext
{
    public SqlServerProductContext(DbContextOptions<SqlServerProductContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<OptionalProduct> OptionalProducts => Set<OptionalProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().ToTable("Products");
        modelBuilder.Entity<OptionalProduct>().ToTable("OptionalProducts");
        modelBuilder.WithTranslatable(this);
    }
}

public sealed class SqlServerExplicitTypeContext : DbContext
{
    public SqlServerExplicitTypeContext(DbContextOptions<SqlServerExplicitTypeContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().ToTable("Products");
        modelBuilder.WithTranslatable(this, options =>
        {
            options.ColumnType = "nvarchar(4000)";
        });
    }
}
