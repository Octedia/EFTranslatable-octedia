using EFTranslatable.Extensions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFTranslatable.Tests;

[Collection(NonParallelCollection.Name)]
public class ObsoleteOverloadTests
{
    private sealed class LegacyContext : DbContext
    {
        public LegacyContext(DbContextOptions<LegacyContext> options) : base(options) { }
        public DbSet<Product> Products => Set<Product>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().ToTable("Products");
            // Use the obsolete positional overload.
#pragma warning disable CS0618 // intentional: testing the obsolete overload still works
            modelBuilder.WithTranslatable(this, "nvarchar(max)", "ar");
#pragma warning restore CS0618
        }
    }

    [Fact]
    public void Obsolete_positional_overload_still_configures_column_type_and_fallback_locale()
    {
        var options = new DbContextOptionsBuilder<LegacyContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=ignored;Trusted_Connection=True;")
            .Options;

        using var ctx = new LegacyContext(options);
        var labelProperty = ctx.Model
            .FindEntityType(typeof(Product))!
            .FindProperty(nameof(Product.Label))!;

        Assert.Equal("nvarchar(max)", labelProperty.GetColumnType());
        Assert.Equal("ar", Translatable.FallbackLocale);
    }
}
