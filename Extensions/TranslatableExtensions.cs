using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;


namespace EFTranslatable.Extensions
{
    /// <summary>
    /// Add the required Database functions to the current ModelBuilder
    /// </summary>
    public static class TranslatableExtensions
    {
        /// <summary>
        /// Extend the ModelBuilder to allow the Translatable functionalists
        /// </summary>
        /// <param name="modelBuilder">The current EntityFrameWork ModelBuilder being used</param>
        /// <param name="context">The current EntityFrameWork DbContext being used</param>
        /// <param name="columnType">The Database column type Json recommended, otherwise text</param>
        /// <param name="fallbackLocale">A fallback locale to use if the current Locale has no translations</param>
        /// <returns>ModelBuilder</returns>
        public static ModelBuilder WithTranslatable(this ModelBuilder modelBuilder, DbContext context, string columnType = "json", string fallbackLocale = null)
        {
            if (fallbackLocale != null)
            {
                Translatable.FallbackLocale = fallbackLocale;
            }

            // Validate that LocaleExtract method exists before registering as database function
            var localeExtractMethod = typeof(Translatable).GetMethod("LocaleExtract", BindingFlags.NonPublic | BindingFlags.Static);
            if (localeExtractMethod == null)
            {
                throw new InvalidOperationException(
                    "Failed to find LocaleExtract method on Translatable type. " +
                    "This is a critical internal error - the EFTranslatable library may be corrupted or incompatible.");
            }

            modelBuilder.HasDbFunction(localeExtractMethod)
                .HasTranslation((args) =>
                {
                    return new SqlFunctionExpression(
                      context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "JSON_Value" : "JSON_Extract",
                        args, nullable: true,
                        argumentsPropagateNullability: new[] { false, false }, typeof(string), null
                        );
                });


            // Create a value converter for storing Translatable as JSON in the database
            // The Translatable constructor handles null values safely
            // convertsNulls: true ensures the converter is called even when database returns NULL
            // Note: convertsNulls is marked as internal in EF Core 6 but is necessary for handling NULL database values
            var converter = new ValueConverter<Translatable, string>(
               v => v.ToJson(),
               v => new Translatable(v),
               convertsNulls: true
           );

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var mBuilder = modelBuilder.Entity(entityType.Name);

                var properties = entityType.ClrType.GetProperties().Where(p => p.PropertyType == typeof(Translatable));
                foreach (var property in properties)
                {
                    mBuilder.Property(property.Name).

                     HasConversion
                      (
                          converter
                          // ValueComparer with null safety for EF Core change tracking
                          // Handles cases where Translatable.Translations might be null (shouldn't happen after constructor fix, but defensive)
                          , new ValueComparer<Translatable>(
                              // Equality comparison: both null = equal, one null = not equal, otherwise compare dictionaries
                              (first, second) =>
                                  first.Translations == null && second.Translations == null ? true :
                                  first.Translations == null || second.Translations == null ? false :
                                  first.Translations.SequenceEqual(second.Translations),
                              // Hash code generation: null returns 0, filter out null entries, then aggregate dictionary hash codes
                              c => c.Translations == null ? 0 :
                                   c.Translations
                                       .Where(kvp => kvp.Key != null && kvp.Value != null)
                                       .Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                              // Snapshot creation: null creates empty Translatable, otherwise deep copy the dictionary
                              c => c.Translations == null
                                  ? new Translatable(new Dictionary<string, string>())
                                  : new Translatable(c.Translations.ToDictionary(_ => _.Key, _ => _.Value))
                          )
                      ).HasColumnType(columnType);
                }
            }

            return modelBuilder;
        }
    }
}
