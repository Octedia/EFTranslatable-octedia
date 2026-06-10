# Migration to v2

v2 is a breaking release. This page lists every change a 1.x consumer is likely to hit.

## At a glance

| Concern | v1 | v2 |
|---|---|---|
| `Translatable` kind | `struct` | `sealed class` |
| `default(Translatable)` | valid (with null `Translations`) | not valid; use `Translatable.Empty` or `null` |
| Optional columns | awkward — no clean null story | `Translatable?` round-trips DB NULL |
| `WithTranslatable` API | positional `(string columnType = "json", string fallbackLocale = null)` | options builder `WithTranslatable(this, options => { ... })` |
| Default column type | `"json"` for every provider | provider-aware, see [providers](providers.md) |
| Default fallback locale | `null` | `"en"` |
| `<Nullable>enable</Nullable>` | off | on; public APIs annotated |
| Target frameworks | `net6.0` | `net6.0;net8.0;net10.0` |

## Why `Translatable` is now a class

A struct cannot represent "no value." `default(Translatable)` left `Translations` as a null dictionary, which forced defensive null checks everywhere and blocked any meaningful use of nullable reference types. As a class:

- `Translatable?` is a real, distinct type — you can model optional columns.
- DB NULL ↔ C# null round-trips through EF's standard null handling (no more `convertsNulls: true`).
- The `Translations` dictionary is always initialized.

What this means for callers:

- Replace `default(Translatable)` with `Translatable.Empty` or `null` (for `Translatable?` properties).
- `Set` and `WithLocale` still chain (they return `this`), but they now mutate the same instance instead of returning a struct copy. If you previously relied on getting back a fresh copy, construct a new `Translatable` explicitly.
- `HasTranslations<T>.Translate` clones the `Translatable` on the result entity so applying the locale doesn't leak back into the source.

## SQL Server: column-type migration

In v1, `WithTranslatable(this)` defaulted to `columnType = "json"`. SQL Server 2016 (and every SQL Server release through 2025 RTM) does not have a `json` column type; the default migration was broken on SQL Server. v2 resolves the column type from the EF provider:

- **SQL Server** → `nvarchar(max)`
- PostgreSQL → `jsonb`
- MySQL → `json`
- SQLite → `text`

If you previously pinned `nvarchar(max)` (or any other type) explicitly, your migrations are unaffected. If you relied on the default, generating a fresh migration after upgrading will produce an `ALTER COLUMN` to switch from `json` to `nvarchar(max)`. Stored values are JSON text either way; only the column type changes.

For a deeper write-up, see [providers.md](providers.md).

## API change: positional → options builder

```csharp
// v1
modelBuilder.WithTranslatable(this, columnType: "nvarchar(max)", fallbackLocale: "en");

// v2 (recommended)
modelBuilder.WithTranslatable(this, options =>
{
    options.ColumnType    = "nvarchar(max)"; // or null to use the provider default
    options.FallbackLocale = "en";
});
```

The v1 positional overload still compiles in v2 but emits a `[Obsolete]` warning and will be removed in v3. The first parameter (`columnType`) is no longer optional in the obsolete overload — if you only want to set `FallbackLocale`, switch to the options builder.

## Target frameworks

The library now multi-targets `net6.0`, `net8.0`, and `net10.0`. EF Core dependency versions are conditional per TFM (6.0.1 / 8.0.0 / 10.0.0).
