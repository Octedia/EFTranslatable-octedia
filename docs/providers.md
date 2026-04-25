# Provider notes

EFTranslatable v2 chooses storage and JSON query translation per EF provider.

## Default column type by provider

| EF provider name (`Database.ProviderName`) | Default `ColumnType` |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | `nvarchar(max)` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `jsonb` |
| `Pomelo.EntityFrameworkCore.MySql` | `json` |
| `MySql.EntityFrameworkCore` | `json` |
| `Microsoft.EntityFrameworkCore.Sqlite` | `text` |
| anything else | `text` |

You can override on a per-call basis with `options.ColumnType = "..."`.

## SQL Server: storage vs. query

SQL Server **2016 and later** can run JSON functions (`JSON_VALUE`, `JSON_QUERY`, `ISJSON`) over plain text columns. SQL Server has no `json` column type. EFTranslatable therefore stores translations as `nvarchar(max)` and queries them with `JSON_VALUE`:

```sql
-- Storage
Title nvarchar(max) NULL

-- Optional integrity constraint (not added by the library; you can add it manually)
CHECK (Title IS NULL OR ISJSON(Title) > 0)

-- Filter
SELECT * FROM Posts WHERE JSON_VALUE([Title], '$.en') = N'Hello'
```

`varchar(max)` would lose Unicode (Arabic, CJK) — always use `nvarchar(max)`.

## SQLite

SQLite stores translations as `text` and queries them with the built-in `json_extract` function:

```sql
SELECT * FROM Posts WHERE json_extract("Title", '$.en') = 'Hello'
```

SQLite has had JSON1 functions in the standard build since 3.38 (2022); recent EF Core providers ship with it enabled.

## PostgreSQL & MySQL

PostgreSQL stores as `jsonb` (preferred over `json` for indexed lookups). MySQL/MariaDB use the `json` type. The library translates `LocaleExtract` calls to `json_extract` for both, which works under EF Core's translation layer with the Npgsql and Pomelo providers respectively.

## Adding a new provider

If you use a provider that's not in the list above, the column type falls back to `text`. To get a more specific default, override per call:

```csharp
modelBuilder.WithTranslatable(this, options =>
{
    options.ColumnType = "<your provider type>";
});
```
