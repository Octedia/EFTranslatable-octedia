using System.Collections.Generic;
using System.Linq;
using EFTranslatable.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFTranslatable.Tests;

public class SqlServerRoundTripTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;

    public SqlServerRoundTripTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
    }

    private SqlServerProductContext NewContext()
    {
        var options = new DbContextOptionsBuilder<SqlServerProductContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        var ctx = new SqlServerProductContext(options);
        ctx.Database.EnsureCreated();
        // Reset state between tests in the shared LocalDb fixture.
        ctx.Database.ExecuteSqlRaw("DELETE FROM Products; DELETE FROM OptionalProducts;");
        return ctx;
    }

    [SkippableFact]
    public void Insert_writes_nvarchar_max_column_without_error()
    {
        Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

        using var ctx = NewContext();
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

    [SkippableFact]
    public void Column_is_actually_nvarchar_max_in_information_schema()
    {
        Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

        using var ctx = NewContext();
        ctx.Database.EnsureCreated();

        using var conn = new SqlConnection(_fixture.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            @"SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
              FROM INFORMATION_SCHEMA.COLUMNS
              WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'Label';";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("nvarchar", reader.GetString(0));
        Assert.Equal(-1, reader.GetInt32(1)); // -1 means MAX
    }

    [SkippableFact]
    public void Unicode_arabic_round_trips_intact()
    {
        Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

        using var ctx = NewContext();
        const string arabic = "توصيل المنتجات";

        ctx.Products.Add(new Product
        {
            Label = new Translatable(new Dictionary<string, string>
            {
                ["ar"] = arabic,
            })
        });
        ctx.SaveChanges();

        var loaded = ctx.Products.AsNoTracking().Single();
        Assert.Equal(arabic, loaded.Label.Get("ar"));
    }

    [SkippableFact]
    public void WhereLocalizedEquals_translates_to_JSON_VALUE_on_sql_server()
    {
        Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

        using var ctx = NewContext();
        ctx.Products.AddRange(
            new Product { Label = new Translatable(new Dictionary<string, string> { ["en"] = "Delivery" }) },
            new Product { Label = new Translatable(new Dictionary<string, string> { ["en"] = "Pickup" }) });
        ctx.SaveChanges();

        var query = ctx.Products.WhereLocalizedEquals(p => p.Label, "Delivery", "en");
        var sql = query.ToQueryString();

        Assert.Contains("JSON_VALUE", sql, System.StringComparison.OrdinalIgnoreCase);

        var hits = query.AsNoTracking().ToList();
        Assert.Single(hits);
        Assert.Equal("Delivery", hits[0].Label.Get("en"));
    }

    [SkippableFact]
    public void WhereLocalizedContains_translates_to_JSON_VALUE_on_sql_server()
    {
        Skip.If(!_fixture.IsAvailable, _fixture.UnavailableReason);

        using var ctx = NewContext();
        ctx.Products.AddRange(
            new Product { Label = new Translatable(new Dictionary<string, string> { ["en"] = "Express Delivery" }) },
            new Product { Label = new Translatable(new Dictionary<string, string> { ["en"] = "Pickup" }) });
        ctx.SaveChanges();

        var query = ctx.Products.WhereLocalizedContains(p => p.Label, "Deliv", "en");
        var sql = query.ToQueryString();

        Assert.Contains("JSON_VALUE", sql, System.StringComparison.OrdinalIgnoreCase);

        var hits = query.AsNoTracking().ToList();
        Assert.Single(hits);
        Assert.Equal("Express Delivery", hits[0].Label.Get("en"));
    }
}
