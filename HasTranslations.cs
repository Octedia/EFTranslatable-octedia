#nullable enable
using System.Collections.Generic;

namespace EFTranslatable
{
    /// <summary>
    /// Give the ability to a derived entity to translate itself.
    /// </summary>
    /// <typeparam name="T">EntityFramework model</typeparam>
    public abstract class HasTranslations<T> where T : class, new()
    {
        /// <summary>
        /// Creates a new instance of the entity with every <see cref="Translatable"/> property
        /// pointing at a fresh copy whose <see cref="Translatable.CurrentLocale"/> is set to
        /// <paramref name="locale"/>. Non-Translatable properties are copied by reference.
        /// </summary>
        /// <remarks>
        /// Translatable is a reference type as of v2.0, so a copy is made per call to avoid
        /// leaking the locale mutation back into the source entity. A null source Translatable
        /// becomes <see cref="Translatable.Empty"/> with the requested locale applied.
        /// </remarks>
        public T Translate(string locale)
        {
            var result = new T();
            var currentType = GetType();

            foreach (var property in currentType.GetProperties())
            {
                if (!property.CanWrite)
                {
                    continue;
                }

                if (property.PropertyType == typeof(Translatable))
                {
                    var propertyValue = property.GetValue(this);

                    if (propertyValue is Translatable source)
                    {
                        var copy = new Translatable(new Dictionary<string, string>(source.Translations));
                        copy.WithLocale(locale);
                        property.SetValue(result, copy);
                    }
                    else
                    {
                        var empty = new Translatable();
                        empty.WithLocale(locale);
                        property.SetValue(result, empty);
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
