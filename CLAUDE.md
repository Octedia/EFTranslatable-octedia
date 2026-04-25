# CLAUDE.md

## Project Overview

EFTranslatable is a small .NET library that lets Entity Framework model properties hold per-locale translations as JSON in the database. Consumers add a `Translatable` property (or `Translatable?`) to an entity, call `modelBuilder.WithTranslatable(this)` in `OnModelCreating`, and the library wires up:

- A value converter that serializes `Translatable` ↔ JSON string.
- A provider-aware column type (`nvarchar(max)` on SQL Server, `jsonb` on Postgres, `json` on MySQL, `text` on SQLite).
- A SQL function (`JSON_VALUE` on SQL Server, `json_extract` elsewhere) used by `WhereLocalizedEquals` / `WhereLocalizedContains` to filter by a specific locale.

The library is published as a NuGet package; `README.md` is the consumer-facing doc.

## Development Commands

```bash
# Build everything (multi-targets net6.0;net8.0;net10.0)
dotnet build EFTranslatable.csproj

# Build a single TFM
dotnet build EFTranslatable.csproj -f net8.0

# Run tests (xUnit, builds against net8.0)
dotnet test EFTranslatable.Tests/EFTranslatable.Tests.csproj

# Build the test project alone
dotnet build EFTranslatable.Tests/EFTranslatable.Tests.csproj

# Pack a NuGet
dotnet pack EFTranslatable.csproj -c Release
```

## Solution / Project Structure

```
EFTranslatable.sln
├── EFTranslatable.csproj           — the library, multi-targets net6/8/10
│   ├── Translatable.cs             — the public Translatable class + JsonConverter
│   ├── HasTranslations.cs          — base class providing Translate(locale)
│   └── Extensions/
│       ├── TranslatableExtensions.cs   — WithTranslatable + TranslatableOptions
│       └── QueryableExtensions.cs      — WhereLocalizedEquals / WhereLocalizedContains / ToListWithTranslations
└── EFTranslatable.Tests/           — xUnit, targets net8.0 only
    ├── TestModels.cs               — shared Product / OptionalProduct entities + DbContexts
    ├── LocalDbFixture.cs           — per-test-class LocalDB database
    ├── ModelMappingTests.cs        — column-type / FallbackLocale assertions
    ├── SqliteRoundTripTests.cs     — in-memory SQLite round-trip
    ├── SqlServerRoundTripTests.cs  — LocalDB round-trip (uses [SkippableFact])
    └── ObsoleteOverloadTests.cs    — verifies the legacy positional overload still works
```

The library `.csproj` lives at the repo root, so it explicitly excludes `EFTranslatable.Tests\**` from its compile glob.

## Core Technologies

- .NET — multi-target `net6.0;net8.0;net10.0`.
- Entity Framework Core — conditional package references per TFM (`6.0.1`, `8.0.0`, `10.0.0`).
- `System.Text.Json` for serialization, with a custom `JsonConverter<Translatable>`.
- xUnit + `Xunit.SkippableFact` for tests.
- `Microsoft.EntityFrameworkCore.Sqlite` (in-memory) and `Microsoft.EntityFrameworkCore.SqlServer` (LocalDB) for the test providers.

The library is provider-agnostic; it explicitly targets EF Core's relational layer and detects the active provider via `DbContext.Database.ProviderName`.

## Configuration

- `<Nullable>enable</Nullable>` is on for both projects. Public APIs are annotated.
- `EFTranslatable.csproj` carries the NuGet metadata: `<Version>`, `<PackageId>`, `<PackageReleaseNotes>`, `<PackageReadmeFile>`. Bump `<Version>` on every shipped change.
- `<DocumentationFile>.\EFTranslatable.xml</DocumentationFile>` is set on Debug only.

## Important Patterns & Conventions

- **`Translatable` is a `sealed class`** (was a struct in v1). `default(Translatable)` is no longer valid; use `Translatable.Empty` or `null` for `Translatable?`.
- **Reference-type semantics matter.** `Set` / `WithLocale` mutate `this` and return it; copying must be explicit (`new Translatable(new Dictionary<string, string>(other.Translations))`). `HasTranslations<T>.Translate` clones to avoid leaking the locale mutation back into the source entity.
- **Storage ≠ query type.** SQL Server stores JSON as `nvarchar(max)`, *not* a `json` column type (SQL Server has no such type, even in 2025 RTM). Queries still use `JSON_VALUE` over the text column.
- **Options builder is the recommended API.** The legacy positional `WithTranslatable(this, columnType, fallbackLocale)` overload is `[Obsolete]` and slated for removal in v3.
- **`modelBuilder.Ignore<Translatable>()`** is called inside `WithTranslatable` to stop EF from discovering the class as an owned/related entity.
- **EF caches `IModel` per DbContext type.** Static state set inside `OnModelCreating` (e.g. `Translatable.FallbackLocale`) only runs once per process per context type. Tests that need a fresh model build use a unique private context class.

## Quick Reference — Where Things Live

| Thing | File |
|---|---|
| `Translatable` class + JsonConverter | [Translatable.cs](Translatable.cs) |
| Static `FallbackLocale` | [Translatable.cs:25](Translatable.cs#L25) |
| `Translatable.Empty` | [Translatable.cs:30](Translatable.cs#L30) |
| `LocaleExtract` marker (SQL function binding) | [Translatable.cs](Translatable.cs) (private static, bottom of class) |
| `HasTranslations<T>.Translate` | [HasTranslations.cs](HasTranslations.cs) |
| `TranslatableOptions` | [Extensions/TranslatableExtensions.cs](Extensions/TranslatableExtensions.cs) |
| `WithTranslatable` (new) + `[Obsolete]` overload | [Extensions/TranslatableExtensions.cs](Extensions/TranslatableExtensions.cs) |
| Provider → column-type resolver | `GetDefaultColumnType` in [Extensions/TranslatableExtensions.cs](Extensions/TranslatableExtensions.cs) |
| `WhereLocalizedEquals` / `WhereLocalizedContains` | [Extensions/QueryableExtensions.cs](Extensions/QueryableExtensions.cs) |
| `ToListWithTranslations` / `ToListWithTranslationsAsync` | [Extensions/QueryableExtensions.cs](Extensions/QueryableExtensions.cs) |
| Consumer docs | [README.md](README.md), [docs/](docs/) |
| Reference docs | [docs/configuration.md](docs/configuration.md), [docs/providers.md](docs/providers.md), [docs/migration-v2.md](docs/migration-v2.md) |
| Changelog | [CHANGELOG.md](CHANGELOG.md) |

## Key Utilities & Helpers

- `Translatable.Get(locale)` — resolution order: requested locale → `CurrentLocale` → thread UI culture → `FallbackLocale` → first available. Never returns null.
- `Translatable.Set(value, locale)` — chainable, mutates in place.
- `Translatable.WithLocale(locale)` — sets `CurrentLocale` and returns `this`.
- Implicit `string` operator — calls `Get()`. Safe on null Translatable (returns empty string).
- `HasTranslations<T>.Translate(locale)` — returns a fresh entity with each Translatable property cloned and pinned to the requested locale.

## External Integrations

None at runtime. The library only depends on `Microsoft.EntityFrameworkCore` + `Microsoft.EntityFrameworkCore.Relational`. Tests additionally pull in the SQLite and SqlServer providers, plus `Xunit.SkippableFact`.

## Important Notes

- **Main branch is `master`**.
- The package targets `net6.0;net8.0;net10.0`. Don't drop a TFM without bumping the major version.
- EF Core dependency versions are conditional per TFM (6.0.1 / 8.0.0 / 10.0.0). When updating one, consider whether the others should move too.
- The library project's compile glob excludes `EFTranslatable.Tests\**` because the .csproj sits at the repo root next to the tests folder.

## Common Gotchas

- **EF Core treats `Translatable` as a candidate entity** if you don't call `WithTranslatable`. The extension method calls `modelBuilder.Ignore<Translatable>()` for you, but if you write code that touches the model before `WithTranslatable` runs, EF may have already begun discovering the type.
- **The model is cached per DbContext type.** If you change `Translatable.FallbackLocale` (a static), it's only re-set on first model build for that context type in the process.
- **SQL Server has no `json` column type** — even in current versions. Always use `nvarchar(max)`.
- **Stored values must be `nvarchar`, not `varchar`** on SQL Server, or non-Latin translations (Arabic, CJK, …) will be mangled.
- **Static `FallbackLocale` is process-wide.** If two contexts in the same process want different fallback locales, that doesn't work — the last `WithTranslatable` call wins.
- **The `[Obsolete]` overload's first arg is no longer optional.** v1 callers that omitted both `columnType` and `fallbackLocale` won't bind to the obsolete overload — they'll bind to the new options-builder overload, which is what we want.

## Working Style & Collaboration Rules

# Global Working Style

These rules apply in every project, every session, unconditionally.

## Code Search
- Never grep the codebase broadly. Ask the user to point to the relevant file or method first.
- Only use targeted greps for a single, specific symbol name where the result is fast and precise.
- For open-ended exploration, spawn an Explore subagent — never flood the main context with wide search results.

## Git
- Commit but never push. Stage and commit when asked; the user pushes manually.
- No `Co-Authored-By` or any Claude attribution in commit messages — ever.

## Approval Gates
- Ask clarifying questions before starting any task.
- Present a step-by-step plan and wait for explicit approval before writing code.
- After each step, report results and wait for approval before continuing.
- **Exception:** If the user says "go ahead", "do it all", "complete it", or similar — skip the gates and execute fully.

## Wrap-up Gate
- After finishing code changes, never close with a summary. Instead ask: "Are we done? Ready to update docs + CHANGELOG?"
- CHANGELOG.md and docs/ updates are part of the definition of done, not optional extras.

## Docs & Changelog
- Keep `CHANGELOG.md` (root) updated with a dated entry for every meaningful change.
- New endpoints or features need a doc file under `docs/` per the project's CONTRIBUTING.md.
- Update the `docs/README.md` index when adding a new folder or file.
