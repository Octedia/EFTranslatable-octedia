using System.Collections.Generic;
using System.Linq;
using EFTranslatable.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFTranslatable.Tests;

public class SqliteRoundTripTests
{
    private static SqliteProductContext NewContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<SqliteProductContext>()
            .UseSqlite(connection)
            .Options;

        var ctx = new SqliteProductContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public void Insert_and_round_trip_translatable_on_sqlite()
    {
        using var ctx = NewContext(out var conn);
        try
        {
            ctx.Products.Add(new Product
            {
                Label = new Translatable(new Dictionary<string, string>
                {
                    ["en"] = "Delivery",
                    ["ar"] = "توصيل",
                })
            });
            ctx.SaveChanges();

            var loaded = ctx.Products.AsNoTracking().Single();
            Assert.Equal("Delivery", loaded.Label.Get("en"));
            Assert.Equal("توصيل", loaded.Label.Get("ar"));
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public void Null_translatable_property_persists_as_db_null()
    {
        using var ctx = NewContext(out var conn);
        try
        {
            ctx.OptionalProducts.Add(new OptionalProduct { Label = null });
            ctx.SaveChanges();

            // Verify with raw SQL that the column is NULL.
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Label FROM OptionalProducts;";
            var raw = cmd.ExecuteScalar();
            Assert.Equal(System.DBNull.Value, raw);
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public void Db_null_materializes_as_null_translatable()
    {
        using var ctx = NewContext(out var conn);
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO OptionalProducts (Label) VALUES (NULL);";
                cmd.ExecuteNonQuery();
            }

            var loaded = ctx.OptionalProducts.AsNoTracking().Single();
            Assert.Null(loaded.Label);
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public void WhereLocalizedEquals_translates_to_json_extract_on_sqlite()
    {
        using var ctx = NewContext(out var conn);
        try
        {
            ctx.Products.AddRange(
                new Product { Label = new Translatable(new Dictionary<string, string> { ["en"] = "Delivery" }) },
                new Product { Label = new Translatable(new Dictionary<string, string> { ["en"] = "Pickup" }) });
            ctx.SaveChanges();

            var query = ctx.Products.WhereLocalizedEquals(p => p.Label, "Delivery", "en");
            var sql = query.ToQueryString();

            Assert.Contains("json_extract", sql, System.StringComparison.OrdinalIgnoreCase);

            var hits = query.AsNoTracking().ToList();
            Assert.Single(hits);
            Assert.Equal("Delivery", hits[0].Label.Get("en"));
        }
        finally
        {
            conn.Dispose();
        }
    }
}
