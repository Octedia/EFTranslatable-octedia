# Configuration: `WithTranslatable` & `TranslatableOptions`

`WithTranslatable` is the entry point that wires Translatable properties into your EF model. v2 introduces an options-builder overload; the legacy positional overload from v1 still compiles but is `[Obsolete]`.

## Recommended overload

```csharp
public static ModelBuilder WithTranslatable(
    this ModelBuilder modelBuilder,
    DbContext context,
    Action<TranslatableOptions>? configure = null);
```

```csharp
modelBuilder.WithTranslatable(this, options =>
{
    options.ColumnType    = null;   // null ⇒ provider default
    options.FallbackLocale = "en";
});
```

When `configure` is omitted, the provider default column type is used and `FallbackLocale` defaults to `"en"`.

## `TranslatableOptions`

| Property | Type | Default | Effect |
|---|---|---|---|
| `ColumnType` | `string?` | `null` | When `null`, the provider-aware default is resolved (see [providers](providers.md)). When set, applied verbatim via `HasColumnType`. |
| `FallbackLocale` | `string` | `"en"` | Stored on the static `Translatable.FallbackLocale`. Used by `Translatable.Get` and `WhereLocalizedEquals` when no locale is supplied. |

## Order in `OnModelCreating`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // 1. Register entities.
    modelBuilder.Entity<Post>().ToTable("Posts");

    // 2. Then call WithTranslatable.
    modelBuilder.WithTranslatable(this);
}
```

`WithTranslatable` does the following:

1. Sets `Translatable.FallbackLocale` from options.
2. Calls `modelBuilder.Ignore<Translatable>()` so EF does not attempt to discover the class as an owned/related entity. (Translatable is a reference type as of v2.0; without this EF would walk into its CLR properties.)
3. Registers `Translatable.LocaleExtract` as a database function whose SQL translation is `JSON_VALUE(...)` on SQL Server and `json_extract(...)` elsewhere.
4. Iterates every entity type in the model and, for each property typed as `Translatable`, applies the value converter, the value comparer, and the resolved column type.

## Legacy positional overload

```csharp
[Obsolete("Use the Action<TranslatableOptions> overload instead. This overload will be removed in v3.")]
public static ModelBuilder WithTranslatable(
    this ModelBuilder modelBuilder,
    DbContext context,
    string? columnType,
    string? fallbackLocale = null);
```

It forwards to the new overload. Accepted for backward compatibility; all new code should use the options builder.

## See also

- [Provider notes](providers.md) — column-type defaults per provider.
- [Migration to v2](migration-v2.md) — full v1 → v2 changes.
