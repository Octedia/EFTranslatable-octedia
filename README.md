



# A simple way to make Entity Framework models translatable!

![Nuget](https://img.shields.io/nuget/v/EFTranslatable?style=plastic) ![Nuget](https://img.shields.io/nuget/dt/EFTranslatable?color=green&style=plastic)

EFTranslatable is a lightweight library that lets your Entity Framework model properties hold per-locale translations as JSON in the database, with provider-aware column types and JSON-function-based filtering on the SQL side.

> **v2.0** is a breaking release. `Translatable` is now a `class` (was a `struct`), the column-type default is provider-aware (SQL Server uses `nvarchar(max)`, not `json`), and configuration moved to an options-builder pattern. See the [Migration from v1](#migration-from-v1) section below and [CHANGELOG.md](CHANGELOG.md) for the full list.

### Installation
EFTranslatable is available on [NuGet](https://www.nuget.org/packages/EFTranslatable/) , [GitHub](https://github.com/AbanoubNassem/EFTranslatable)

```sh
dotnet add package EFTranslatable
```

Targets `net6.0`, `net8.0`, and `net10.0`.

## Basic usage

The following code demonstrates basic usage of EFTranslatable:-

### Making a model translatable

- First, register EFTranslatable in `OnModelCreating`. By default the column type is chosen per provider (SQL Server → `nvarchar(max)`, PostgreSQL → `jsonb`, MySQL → `json`, SQLite → `text`) and the fallback locale defaults to `"en"`.

```C#
    using EFTranslatable;
    using EFTranslatable.Extensions;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Register entities first.
        modelBuilder.Entity<Post>().ToTable("Posts");

        // Then wire up Translatable. With no options this uses the provider default.
        modelBuilder.WithTranslatable(this);

        // Or override:
        // modelBuilder.WithTranslatable(this, options =>
        // {
        //     options.FallbackLocale = "ar";
        //     options.ColumnType    = "nvarchar(max)"; // null = provider default
        // });
    }

    public DbSet<Post> Posts { get; set; }
```

- Then derive your entity from `HasTranslations<T>` where `T` is the entity itself.
- Use `Translatable` for any property you want to translate. For an optional column, use `Translatable?`.

```C#
    using EFTranslatable;

   public class Post : HasTranslations<Post>
   {
       public uint Id { get; set; }

       public Translatable Title { get; set; } = new();

       public Translatable Content { get; set; } = new();

       // Optional column: null in the database, null in C#.
       public Translatable? Subtitle { get; set; }
   }
```

### Creating a model with `Translatable` properties

There is two ways of making a `Translatable` :
    
 1. By passing a `Dictionary` where is the `Key` is the locale and the `Value` is the transaltion.
 2. Or by passing `Json` string directly .

```C#
    public void Create()
    {
        _context.Posts.Add(new Post()
        {
            Title = new Translatable(new Dictionary<string, string>()
            {
                {"en","Good Title"},
                {"ar", "عنون جيد"}
            }),
            Content = new Translatable("{\"en\": \"Good Content!\",\"ar\": \"شغال\"}"),                
        });

        _context.SaveChanges();
    }
```

### Updating a model with `Translatable` properties

```C#
    public void Update()
    {
        var post = _context.Posts.First();

        post.Title.Set("Different Title", "en").Set("New Locale", "fr");

        // If locale is null , the Current `Thread` locale will be used
        post.Content.Set("This is the current locale content");

        _context.SaveChanges();
    }
```

### Quering/Filtering a model with `Translatable` properties

```C#
    using EFTranslatable.Extensions;

    public void Equality()
    {
        // Will use the Current `Thread` locale
        var post = _context.Posts.WhereLocalizedEquals(x => x.Title, "Good Title").SingleOrDefault();

        //Will use the giving Locale
        var post2 = _context.Posts.WhereLocalizedEquals(x => x.Title, "عنون جيد", "ar").SingleOrDefault();
    }

    public void Contains()
    {
        // Will use the whole `Json` string for checking
        var post = _context.Posts.WhereLocalizedContains(x => x.Title, "Good Title").SingleOrDefault();

        //Will use the giving Locale string value for checking
        var post2 = _context.Posts.WhereLocalizedContains(x => x.Title, "عنون جيد", "ar").SingleOrDefault();
    }
```

### Retrieving/Returning a model with a specific translation

```C#
    using EFTranslatable.Extensions;

    [HttpGet]
    public IActionResult Single()
    {
        var post = _context.Posts.WhereLocalizedEquals(x => x.Title, "Good Title").SingleOrDefault();

        return Ok(post?.Translate("ar"));
    }

    [HttpGet]
    public IActionResult Multiple()
    {
        var posts = _context.Posts.Select(x => x.Translate("ar")).ToList();

        return Ok(posts);
    }
```

### Using Raw SQL Queries and Stored Procedures

When using `FromSqlRaw()` or `FromSqlInterpolated()` with Translatable properties, use the `ToListWithTranslationsAsync()` extension method for cleaner code:

#### Recommended Approach

```C#
using EFTranslatable.Extensions;

// Async version (recommended)
var doctors = await _context.Doctors
    .FromSqlRaw("EXEC sp_Doctors_Module_centralized @Param",
        new SqlParameter("@Param", value))
    .ToListWithTranslationsAsync("en");

// doctors[0].Title is already translated to English

// Sync version
var posts = _context.Posts
    .FromSqlRaw("SELECT * FROM Posts")
    .ToListWithTranslations("ar");
```

#### Alternative: Manual Translation

If you need more control, materialize first then translate:

```C#
var doctors = await _context.Doctors
    .FromSqlRaw("EXEC sp_GetDoctors")
    .ToListAsync();

// Manually translate each property
var translated = doctors.Select(d => new
{
    d.Id,
    Title = d.Title.Get("en"),
    Description = d.Description.Get("en")
}).ToList();

// Or use .Translate() for whole entity
var translated = doctors.Select(d => d.Translate("en")).ToList();
```

#### Important Notes

1. **NULL values are safe**: The library now handles NULL Translatable columns gracefully - they become empty Translatables with no translations.

2. **Cannot use .Select() in query pipeline**: This will NOT work:
   ```C#
   // ❌ FAILS: InvalidOperationException - non-composable SQL
   var result = await context.Doctors
       .FromSqlRaw("EXEC sp_GetDoctors")
       .Select(d => d.Translate("en"))  // Cannot compose over FromSqlRaw
       .ToListAsync();
   ```
   Use `ToListWithTranslationsAsync()` instead (shown above).

3. **Works with all providers**: SQL Server, PostgreSQL, MySQL, SQLite - any database with JSON support.

## Null Safety

EFTranslatable includes comprehensive null safety features to handle edge cases gracefully:

### Safe Handling of NULL Database Values

If your database contains NULL values in Translatable columns (which can happen with legacy data or optional fields), EFTranslatable now handles them safely:

```C#
// Even if Summary is NULL in the database, this won't crash
var doctor = await _context.Doctors.FindAsync(id);
var translated = doctor.Translate("en"); // ✅ Safe - null properties become empty Translatable

// Empty Translatable returns empty string (not null)
string summary = translated.Summary; // Returns "" instead of throwing
```

### Constructor Null Safety

The `Translatable` constructor safely handles null, empty, or malformed JSON:

```C#
// All of these are safe and create valid Translatable instances
var t1 = new Translatable(null);                    // ✅ Creates empty Translatable
var t2 = new Translatable("");                      // ✅ Creates empty Translatable
var t3 = new Translatable("{invalid json}");        // ✅ Creates empty Translatable
var t4 = new Translatable("{\"en\":\"Hello\"}");   // ✅ Parses correctly
```

### Safe String Conversion

The implicit string conversion and `Get()` method always return a valid string:

```C#
var emptyTranslatable = new Translatable(new Dictionary<string, string>());

// These never throw, always return empty string if translation not found
string text1 = emptyTranslatable;           // Returns ""
string text2 = emptyTranslatable.Get("en"); // Returns ""
```

### Best Practices

While EFTranslatable handles null values safely, we recommend:

1. **Initialize properties** when creating new entities:
   ```C#
   public class Post : HasTranslations<Post>
   {
       public Translatable Title { get; set; } = new();  // ✅ Good practice
       public Translatable Content { get; set; } = new(); // ✅ Good practice
   }
   ```

2. **Use NOT NULL constraints** in your database schema for better data quality (optional):
   ```SQL
   ALTER TABLE Posts ALTER COLUMN Title NVARCHAR(MAX) NOT NULL DEFAULT '{}';
   ```

3. **Handle empty translations** in your UI:
   ```C#
   var translatedTitle = post.Title.Get("en");
   if (string.IsNullOrEmpty(translatedTitle))
   {
       // Show placeholder or fallback content
       translatedTitle = "Untitled";
   }
   ```

### Migration from Older Versions

If you're upgrading from a 1.x version that crashed on NULL values:
- ✅ **No code changes required** - existing code will work
- ✅ **No database migration required** - NULL columns now handled gracefully
- ✅ **Backward compatible** - all existing functionality preserved

## Migration from v1

v2 is a breaking release. The summary:

| Concern | v1 | v2 |
|---|---|---|
| `Translatable` kind | `struct` | `sealed class` |
| `default(Translatable)` | valid (with null `Translations`) | not valid; use `Translatable.Empty` or `null` |
| Optional columns | awkward — no clean null story | `Translatable?` round-trips DB NULL |
| `WithTranslatable` API | positional `(string columnType = "json", string fallbackLocale = null)` | options builder `WithTranslatable(this, options => { ... })` |
| Default column type | `"json"` for every provider (broken on SQL Server) | provider-aware: SQL Server `nvarchar(max)`, PostgreSQL `jsonb`, MySQL `json`, SQLite `text` |
| Default fallback locale | `null` | `"en"` |
| `<Nullable>enable</Nullable>` | off | on; public APIs annotated |
| Target frameworks | `net6.0` | `net6.0;net8.0;net10.0` |

**SQL Server users**: a generated migration will switch the column type from `json` to `nvarchar(max)`. Stored values stay as JSON text — only the column type changes. If you already pinned `nvarchar(max)` manually you are unaffected.

**The legacy positional overload still compiles but emits an `[Obsolete]` warning** and will be removed in v3:

```C#
// v1 (still works in v2, marked obsolete):
modelBuilder.WithTranslatable(this, "nvarchar(max)", "en");

// v2 recommended:
modelBuilder.WithTranslatable(this, options =>
{
    options.ColumnType    = "nvarchar(max)"; // or null for provider default
    options.FallbackLocale = "en";
});
```

For more details, see [CHANGELOG.md](CHANGELOG.md).

## License

The MIT License (MIT). Please see [License File](https://github.com/AbanoubNassem/EFTranslatable/blob/master/LICENSE.md) for more information.