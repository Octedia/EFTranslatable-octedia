using System.Linq;
using EFTranslatable.Extensions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFTranslatable.Tests;

[Collection(NonParallelCollection.Name)]
public class ModelMappingTests
{
    [Fact]
    public void SqlServer_provider_default_column_type_is_nvarchar_max()
    {
        var options = new DbContextOptionsBuilder<SqlServerProductContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=ignored;Trusted_Connection=True;")
            .Options;

        using var ctx = new SqlServerProductContext(options);
        var labelProperty = ctx.Model
            .FindEntityType(typeof(Product))!
            .FindProperty(nameof(Product.Label))!;

        Assert.Equal("nvarchar(max)", labelProperty.GetColumnType());
        Assert.NotEqual("json", labelProperty.GetColumnType());
    }

    [Fact]
    public void Sqlite_provider_default_column_type_is_text()
    {
        var options = new DbContextOptionsBuilder<SqliteProductContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new SqliteProductContext(options);
        var labelProperty = ctx.Model
            .FindEntityType(typeof(Product))!
            .FindProperty(nameof(Product.Label))!;

        Assert.Equal("text", labelProperty.GetColumnType());
    }

    [Fact]
    public void Explicit_ColumnType_override_is_propagated()
    {
        var options = new DbContextOptionsBuilder<SqlServerExplicitTypeContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=ignored;Trusted_Connection=True;")
            .Options;

        using var ctx = new SqlServerExplicitTypeContext(options);
        var labelProperty = ctx.Model
            .FindEntityType(typeof(Product))!
            .FindProperty(nameof(Product.Label))!;

        Assert.Equal("nvarchar(4000)", labelProperty.GetColumnType());
    }

    [Fact]
    public void Default_FallbackLocale_is_en()
    {
        // Reset to a non-default value first to prove the call sets it back.
        Translatable.FallbackLocale = "xx";

        // Use a context class unique to this test so EF's per-context-type model cache
        // doesn't short-circuit OnModelCreating.
        var options = new DbContextOptionsBuilder<FallbackLocaleProbeContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var ctx = new FallbackLocaleProbeContext(options);
        _ = ctx.Model;

        Assert.Equal("en", Translatable.FallbackLocale);
    }

    private sealed class FallbackLocaleProbeContext : DbContext
    {
        public FallbackLocaleProbeContext(DbContextOptions<FallbackLocaleProbeContext> options) : base(options) { }
        public DbSet<Product> Products => Set<Product>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().ToTable("Products");
            modelBuilder.WithTranslatable(this);
        }
    }
}
