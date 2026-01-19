using System.Collections.Generic;

namespace EFTranslatable
{
    /// <summary>
    /// Give the ability to driven Entity to translate it's self
    /// </summary>
    /// <typeparam name="T">EntityFrameWork Model</typeparam>
    public abstract class HasTranslations<T> where T : class, new()
    {
        /// <summary>
        /// Creates a new instance of the entity with all Translatable properties set to the specified locale.
        /// Non-translatable properties are copied as-is to the new instance.
        /// </summary>
        /// <param name="locale">The target locale code (e.g., "en", "ar", "fr") to translate all Translatable properties to</param>
        /// <returns>A new entity instance with all Translatable properties configured for the specified locale</returns>
        /// <remarks>
        /// This method uses reflection to iterate through all properties of the entity.
        /// For Translatable properties:
        /// - Null values are safely handled by creating empty Translatable instances
        /// - The locale is set using WithLocale() which affects the implicit string conversion
        /// - The original entity's properties remain unchanged
        ///
        /// For non-Translatable properties:
        /// - Values are copied by reference to the new instance
        ///
        /// This method is null-safe and will not throw NullReferenceException even if
        /// Translatable properties contain null values from the database.
        /// </remarks>
        public T Translate(string locale)
        {
            var result = new T();
            var currentType = GetType();

            foreach (var property in currentType.GetProperties())
            {
                if (property.PropertyType == typeof(Translatable))
                {
                    var propertyValue = property.GetValue(this);

                    // Handle null Translatable properties gracefully
                    if (propertyValue == null)
                    {
                        // Set default empty Translatable
                        property.SetValue(result, new Translatable(new Dictionary<string, string>()));
                        continue;
                    }

                    // Type-safe cast with pattern matching to handle EF proxies and type mismatches
                    if (propertyValue is Translatable value)
                    {
                        var tempLocale = value.CurrentLocale;
                        value.WithLocale(locale);
                        property.SetValue(result, value);
                        value.WithLocale(tempLocale);
                    }
                    else
                    {
                        // Property type says Translatable but value is different type
                        // This can happen with EF proxies or inheritance issues
                        // Fall back to empty Translatable rather than crashing
                        property.SetValue(result, new Translatable(new Dictionary<string, string>()));
                    }
                }
                else
                {
                    property.SetValue(result, property.GetValue(this));
                }
            }

            return result;
        }
    }
}
