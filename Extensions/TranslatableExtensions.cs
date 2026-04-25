#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;


namespace EFTranslatable.Extensions
{
    /// <summary>
    /// Configuration for <see cref="TranslatableExtensions.WithTranslatable(ModelBuilder, DbContext, Action{TranslatableOptions}?)"/>.
    /// </summary>
    public sealed class TranslatableOptions
    {
        /// <summary>
        /// The database column type used for Translatable properties.
        /// When null, a provider-aware default is used:
        /// SQL Server → <c>nvarchar(max)</c>, PostgreSQL → <c>jsonb</c>,
        /// MySQL → <c>json</c>, SQLite → <c>text</c>, unknown → <c>text</c>.
        /// </summary>
        public string? ColumnType { get; set; }

        /// <summary>
        /// The fallback locale used when a translation for the requested locale is missing.
        /// Defaults to "en".
        /// </summary>
        public string FallbackLocale { get; set; } = "en";
    }

    /// <summary>
    /// Adds the database functions and value conversions required for Translatable properties
    /// to a <see cref="ModelBuilder"/>.
    /// </summary>
    public static class TranslatableExtensions
    {
        /// <summary>
        /// Configures all Translatable properties on the model.
        /// </summary>
        /// <param name="modelBuilder">The current ModelBuilder.</param>
        /// <param name="context">The DbContext (used to detect the provider for column-type defaults and JSON function translation).</param>
        /// <param name="configure">Optional configuration callback. When omitted, the provider-aware column type is used and FallbackLocale defaults to "en".</param>
        public static ModelBuilder WithTranslatable(
            this ModelBuilder modelBuilder,
            DbContext context,
            Action<TranslatableOptions>? configure = null)
        {
            var options = new TranslatableOptions();
            configure?.Invoke(options);

            Translatable.FallbackLocale = options.FallbackLocale;

            // Translatable is a reference type; without this, EF would try to discover it as an
            // owned/related entity when it appears as a property type. We map each Translatable
            // property as a scalar via HasConversion below, so EF must not treat the type itself
            // as an entity.
            modelBuilder.Ignore<Translatable>();

            var resolvedColumnType = options.ColumnType ?? GetDefaultColumnType(context.Database);

            var localeExtractMethod = typeof(Translatable).GetMethod("LocaleExtract", BindingFlags.NonPublic | BindingFlags.Static);
            if (localeExtractMethod == null)
            {
                throw new InvalidOperationException(
                    "Failed to find LocaleExtract method on Translatable type. " +
                    "This is a critical internal error - the EFTranslatable library may be corrupted or incompatible.");
            }

            var sqlFunction = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer"
                ? "JSON_VALUE"
                : "json_extract";

            modelBuilder.HasDbFunction(localeExtractMethod)
                .HasTranslation(args => new SqlFunctionExpression(
                    sqlFunction,
                    args,
                    nullable: true,
                    argumentsPropagateNullability: new[] { false, false },
                    typeof(string),
                    null));

            // Translatable is now a reference type. EF handles null model values / null DB values
            // out of the box, so convertsNulls is left at its default (false).
            var converter = new ValueConverter<Translatable, string>(
                v => v.ToJson(),
                v => new Translatable(v));

            var comparer = new ValueComparer<Translatable>(
                (a, b) =>
                    ReferenceEquals(a, b) ||
                    (a != null && b != null && a.Translations.SequenceEqual(b.Translations)),
                c => c == null
                    ? 0
                    : c.Translations.Aggregate(0, (acc, kvp) =>
                        HashCode.Combine(acc, kvp.Key.GetHashCode(), kvp.Value == null ? 0 : kvp.Value.GetHashCode())),
                c => c == null
                    ? new Translatable()
                    : new Translatable(new Dictionary<string, string>(c.Translations)));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var entityBuilder = modelBuilder.Entity(entityType.Name);

                var properties = entityType.ClrType
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(Translatable));

                foreach (var property in properties)
                {
                    entityBuilder
                        .Property(property.Name)
                        .HasConversion(converter, comparer)
                        .HasColumnType(resolvedColumnType);
                }
            }

            return modelBuilder;
        }

        /// <summary>
        /// Legacy positional overload preserved from v1. Use the
        /// <see cref="WithTranslatable(ModelBuilder, DbContext, Action{TranslatableOptions}?)"/>
        /// overload instead.
        /// </summary>
        [Obsolete("Use the Action<TranslatableOptions> overload instead. This overload will be removed in v3.")]
        public static ModelBuilder WithTranslatable(
            this ModelBuilder modelBuilder,
            DbContext context,
            string? columnType,
            string? fallbackLocale = null)
        {
            return modelBuilder.WithTranslatable(context, options =>
            {
                options.ColumnType = columnType;
                if (!string.IsNullOrEmpty(fallbackLocale))
                {
                    options.FallbackLocale = fallbackLocale;
                }
            });
        }

        private static string GetDefaultColumnType(DatabaseFacade database) =>
            database.ProviderName switch
            {
                "Microsoft.EntityFrameworkCore.SqlServer"   => "nvarchar(max)",
                "Npgsql.EntityFrameworkCore.PostgreSQL"     => "jsonb",
                "Pomelo.EntityFrameworkCore.MySql"          => "json",
                "MySql.EntityFrameworkCore"                 => "json",
                "Microsoft.EntityFrameworkCore.Sqlite"      => "text",
                _                                           => "text"
            };
    }
}
