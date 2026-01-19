



# A simple way to make Entity Framework models translatable!

![Nuget](https://img.shields.io/nuget/v/EFTranslatable?style=plastic) ![Nuget](https://img.shields.io/nuget/dt/EFTranslatable?color=green&style=plastic)

EFTranslatable is a light weight simple struct/type to allow your Entity/Model properties to be easily translated.

### Installation
EFTranslatable is available on [NuGet](https://www.nuget.org/packages/EFTranslatable/) , [GitHub](https://github.com/AbanoubNassem/EFTranslatable)

```sh
dotnet add package EFTranslatable
```
## Basic usage

The following code demonstrates basic usage of EFTranslatable:-

### Making a model translatable

The required steps to make a model translatable are:

- First, you need to allow `DbContext` to know about `EFTranslatable`,
  in your `DataContext` file, in `OnModelCreating` method add the following:-

```C#
    using EFTranslatable;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.WithTranslatable(this);
    }

    public DbSet<Post> Posts { get; set; }
```

- Next, you should Drive from `HasTranslations<T>` where `T` is the current `Model`.
- Next, Add `Translatable` to the property you want to be translatable.
- Finally, you should make sure that all translatable properties are set to the `text`-datatype in your database. If your database supports `json`-columns, use that.

```C#
    using EFTranslatable;

   public class Post : HasTranslations<Post>
   {
       public uint Id { get; set; }

       public Translatable Title { get; set; } = new();

       public Translatable Content { get; set; } = new();
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

If you're upgrading from a version that crashed on NULL values:
- ✅ **No code changes required** - existing code will work
- ✅ **No database migration required** - NULL columns now handled gracefully
- ✅ **Backward compatible** - all existing functionality preserved

For more details on the null safety improvements, see [CHANGELOG.md](CHANGELOG.md).

## License

The MIT License (MIT). Please see [License File](https://github.com/AbanoubNassem/EFTranslatable/blob/master/LICENSE.md) for more information.