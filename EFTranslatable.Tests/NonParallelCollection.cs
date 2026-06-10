using Xunit;

namespace EFTranslatable.Tests;

/// <summary>
/// Tests that mutate <see cref="Translatable.FallbackLocale"/> (a static field) must run serially.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class NonParallelCollection
{
    public const string Name = "non-parallel";
}
