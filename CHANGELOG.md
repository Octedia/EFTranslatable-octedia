# Changelog

All notable changes to the EFTranslatable library will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed - Null Safety Improvements

#### Critical Null Reference Exception Fixes

This release includes comprehensive null safety improvements that fix `NullReferenceException` crashes when Translatable properties contain NULL values from the database.

**Files Modified:**
- `Translatable.cs` - Core struct with improved null handling
- `HasTranslations.cs` - Base class translation method with null safety
- `Extensions/TranslatableExtensions.cs` - EF Core integration with defensive null checks

#### Detailed Changes

##### 1. Translatable Constructor (`Translatable.cs`)
- **Problem**: Constructor would throw `NullReferenceException` when deserializing null or empty JSON strings
- **Fix**: Added null/whitespace checks and try-catch block for `JsonException`
- **Behavior**: Now initializes with empty dictionary for null, empty, or malformed JSON
- **Impact**: Prevents crashes when reading NULL database values through EF Core's value converter

```csharp
// Before: Would throw on null JSON
public Translatable(string json)
{
    CurrentLocale = null;
    Translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json); // Throws!
}

// After: Safe initialization
public Translatable(string json)
{
    CurrentLocale = null;
    if (string.IsNullOrWhiteSpace(json))
    {
        Translations = new Dictionary<string, string>();
        return;
    }
    try
    {
        Translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
    }
    catch (JsonException)
    {
        Translations = new Dictionary<string, string>();
    }
}
```

##### 2. ToJson() Method (`Translatable.cs`)
- **Problem**: Could throw if Translations field was null (edge case)
- **Fix**: Added null check before serialization
- **Behavior**: Returns `"{}"` (empty JSON object) if Translations is null
- **Impact**: Safe serialization even with invalid struct state

##### 3. Get() Method (`Translatable.cs`)
- **Problem**: `Translations.ContainsKey()` would throw if Translations was null
- **Fix**: Added defensive null check at method start
- **Behavior**: Returns empty string if Translations is null
- **Impact**: Never throws, always returns valid string (empty string is safe default)

##### 4. Set() Method (`Translatable.cs`)
- **Problem**: Would throw when trying to add/update in null dictionary
- **Fix**: Added null check that returns early if Translations is null
- **Behavior**: Safely no-ops on null Translations
- **Impact**: Prevents crashes when attempting to update invalid struct

##### 5. HasTranslations.Translate() Method (`HasTranslations.cs`)
- **Problem**: Used null-forgiving operator (`!`) and cast without checking, throwing on null properties
- **Fix**:
  - Removed null-forgiving operator
  - Added explicit null check before casting
  - Creates empty Translatable for null properties
- **Behavior**: Null Translatable properties become empty Translatable instances in translated entity
- **Impact**: Entities with NULL Translatable columns can now be safely translated
- **Breaking**: None - maintains existing behavior for non-null properties

```csharp
// Before: Would crash on null
var value = (Translatable)property.GetValue(this)!;

// After: Safe handling
var propertyValue = property.GetValue(this);
if (propertyValue == null)
{
    property.SetValue(result, new Translatable(new Dictionary<string, string>()));
    continue;
}
var value = (Translatable)propertyValue;
```

##### 6. ValueComparer in TranslatableExtensions (`Extensions/TranslatableExtensions.cs`)
- **Problem**:
  - Equality comparison would throw if either struct had null Translations
  - Hash code generation would throw on null
  - Snapshot creation would throw on null
- **Fix**: Added null checks to all three lambda expressions
- **Behavior**:
  - Two nulls are considered equal
  - One null vs non-null = not equal
  - Null generates hash code of 0
  - Null creates empty Translatable in snapshot
- **Impact**: EF Core change tracking won't crash with invalid struct state

### Added - Documentation

#### XML Documentation
- Enhanced XML docs for `Translatable(string json)` constructor with null safety remarks
- Enhanced XML docs for `ToJson()` method explaining null handling
- Enhanced XML docs for `Get()` method with comprehensive fallback logic explanation
- Enhanced XML docs for `Set()` method with null safety notes
- Enhanced XML docs for `HasTranslations<T>.Translate()` method with detailed remarks

#### Inline Comments
- Added inline comments to ValueConverter in `TranslatableExtensions.cs`
- Added detailed inline comments to ValueComparer explaining null safety logic
- Added comments explaining each lambda in the ValueComparer (equality, hash code, snapshot)

#### This Changelog
- Created comprehensive changelog documenting all null safety fixes

### Changed - Dependencies
- Added `using System.Collections.Generic;` to `HasTranslations.cs`
- Added `using System.Collections.Generic;` to `Extensions/TranslatableExtensions.cs`

## Benefits of This Release

### 1. Robustness
- No more `NullReferenceException` crashes when database columns contain NULL
- Graceful handling of malformed JSON data
- Safe defaults (empty dictionary/string) prevent error propagation

### 2. Backward Compatibility
- **Zero breaking changes** to public API
- Existing code continues to work unchanged
- NULL database values that previously crashed now work correctly

### 3. Database Flexibility
- Works with existing NULL columns (no migration required)
- Can handle databases with missing or incomplete translation data
- Supports gradual data migration scenarios

### 4. Developer Experience
- Better XML documentation for IntelliSense
- Clearer inline comments for maintainability
- Comprehensive error prevention (defense in depth)

## Migration Guide

### Upgrading from Previous Versions

**Good News**: No code changes required! This is a fully backward-compatible bug fix release.

#### If You Were Experiencing Crashes:
```csharp
// This code that previously crashed on NULL Translatable properties:
var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == id);
var translated = doctor.Translate("en"); // Previously: NullReferenceException

// Now works safely - null properties become empty Translatable instances
```

#### If You Were Working Around the Issue:
```csharp
// Before: Had to check for null manually
if (doctor.Summary != null)
{
    var translated = doctor.Translate("en");
}

// After: No longer necessary, but still safe to do
var translated = doctor.Translate("en"); // Always safe now
```

#### Database Schema Considerations:
- **No migration required** - works with existing schemas
- NULL columns are now handled gracefully
- Empty translations return empty strings (not null)
- Consider adding default values or NOT NULL constraints for better data quality (optional)

## Testing Recommendations

### Scenarios to Test:
1. ✅ Query entities with NULL Translatable properties and call `.Translate()`
2. ✅ Create new entities without initializing Translatable properties
3. ✅ Deserialize JSON with null translation values
4. ✅ Call `.Get()` on empty Translatable instances
5. ✅ Update entities with NULL Translatable properties

### Example Test Case:
```csharp
// Create entity with NULL Translatable in database
var entity = new Doctor { Name = "Dr. Smith", Summary = null };
_context.Doctors.Add(entity);
await _context.SaveChangesAsync();

// Query and translate (previously crashed, now safe)
var doctor = await _context.Doctors.FindAsync(entity.Id);
var translated = doctor.Translate("en");

// Verify: Summary should be empty Translatable, not crash
Assert.NotNull(translated);
Assert.NotNull(translated.Summary);
Assert.Equal(string.Empty, translated.Summary.Get("en"));
```

## Technical Details

### Null Safety Strategy: Defense in Depth

This release implements multiple layers of null protection:

1. **Constructor Level**: Prevents null Translations field at creation time
2. **Method Level**: All public methods check for null before operations
3. **Integration Level**: EF Core ValueConverter and ValueComparer handle null gracefully
4. **Translation Level**: HasTranslations handles null property values during translation

### Performance Impact
- **Negligible**: Added null checks are simple conditional branches
- **No reflection overhead added**: Existing reflection patterns unchanged
- **No allocations added**: Only creates empty dictionaries when necessary (failure path)
- **Branch prediction**: Modern CPUs handle these simple null checks efficiently

### Struct Initialization Guarantees
After these changes, the `Translatable` struct guarantees:
- `Translations` field is **never null** when created via constructor
- All public methods handle null gracefully (defensive layer)
- Implicit string conversion always returns non-null string
- Empty dictionary is the canonical representation of "no translations"

## Known Issues & Limitations

### Not Fixed in This Release:
1. **Dictionary comparison is order-dependent** in ValueComparer - uses `SequenceEqual()` which is sensitive to key insertion order
2. **Performance**: Still uses reflection in `Translate()` method (not optimized)
3. **Fallback locale non-deterministic**: `Translations.Keys.FirstOrDefault()` when FallbackLocale not found
4. **No nullable reference type annotations**: Library doesn't use C# 8+ nullable reference types

### Future Enhancements (Not Included):
- Caching reflection metadata for better performance
- Content-based dictionary comparison (order-independent)
- Nullable reference type annotations
- Comprehensive unit test suite
- Source generator for compile-time property access

## Contributing

If you're contributing back to the main project, please note:
- All changes maintain backward compatibility
- XML documentation follows Microsoft standards
- Defensive programming approach (multiple null check layers)
- No performance regressions introduced
- Works with EF Core 6.0.1+

## Acknowledgments

These fixes address critical production issues where NULL database values would cause application crashes. The changes ensure robust operation in real-world scenarios with incomplete or missing translation data.

---

## [1.0.4] - Previous Release

See project history for details on previous versions.
