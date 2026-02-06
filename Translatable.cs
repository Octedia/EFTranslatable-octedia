using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading;

namespace EFTranslatable
{
    /// <summary>
    /// Translatable is a type which allow the Entity property Translatable 
    /// </summary>
    [JsonConverter(typeof(TranslatableJsonConverter))]
    public struct Translatable
    {
        /// <summary>
        /// A fallback locale to use if the current Locale has no translations
        /// </summary>
        public static string FallbackLocale { get; set; }

        /// <summary>
        /// The current Locales with it's Translations
        /// </summary>
        public readonly Dictionary<string, string> Translations;
        /// <summary>
        /// The current locale the Property is using
        /// </summary>
        public string CurrentLocale { get; set; }

        /// <summary>
        /// A constructor to build Translatable out of JSON string.
        /// Handles null, empty, or malformed JSON gracefully by initializing with an empty dictionary.
        /// </summary>
        /// <param name="json">The JSON string representing a dictionary of locale-translation pairs.
        /// Can be null, empty, or malformed - will default to an empty dictionary in these cases.</param>
        /// <remarks>
        /// This constructor provides null safety by ensuring the Translations dictionary is never null.
        /// If deserialization fails due to invalid JSON, an empty dictionary is used as a safe fallback.
        /// </remarks>
        [JsonConstructor]
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
            catch (Exception)  // Catch all exceptions, not just JsonException
            {
                // Gracefully handle any deserialization error (JsonException, ArgumentException, etc.)
                Translations = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// A constructor to build/copy Translatable out of an other Translatable
        /// </summary>
        /// <param name="translations">The giving Locales with it's Translations</param>
        [JsonConstructor]
        public Translatable(Dictionary<string, string> translations)
        {
            Translations = translations;
            CurrentLocale = null;
        }

        /// <summary>
        /// Convert the current Translations to JSON with Unicode support
        /// </summary>
        /// <returns>JSON string representation of the translations dictionary.
        /// Returns "{}" (empty JSON object) if Translations is null.</returns>
        /// <remarks>
        /// This method includes null safety checks and uses JavaScriptEncoder to properly handle all Unicode characters,
        /// making it safe for internationalized content.
        /// </remarks>
        public string ToJson()
        {
            if (Translations == null)
                return "{}";

            return JsonSerializer.Serialize(Translations, new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true
            });
        }

        /// <summary>
        /// Set/Update the given locale with the given translation
        /// </summary>
        /// <param name="value">The translation text to set</param>
        /// <param name="locale">
        ///         The locale code (e.g., "en", "ar", "fr"). If null, the CurrentLocale will be used.
        ///         If CurrentLocale is null, the current Thread's UI culture will be used instead.
        /// </param>
        /// <returns>The current Translatable instance for method chaining</returns>
        /// <remarks>
        /// This method includes null safety checks. If Translations is null, the method returns immediately
        /// without throwing an exception. The method updates existing translations or adds new ones as needed.
        /// </remarks>
        public Translatable Set(string value, string locale = null)
        {
            // Defensive check
            if (Translations == null)
                return this;

            var key = locale
                      ?? CurrentLocale
                      ?? Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

            if (Translations.ContainsKey(key))
                Translations[key] = value;
            else
                Translations.Add(key, value);

            return this;
        }

        /// <summary>
        /// Returns the translated value for the specified locale with intelligent fallback logic
        /// </summary>
        /// <param name="locale">
        ///     The locale code to retrieve (e.g., "en", "ar", "fr"). If null, the CurrentLocale will be used.
        ///     If CurrentLocale is null, the current Thread's UI culture will be used.
        ///     If no translation is found for the requested locale, the FallbackLocale will be used.
        ///     If FallbackLocale is not set or not found, the first available translation will be used.
        /// </param>
        /// <returns>
        ///     The translated string for the specified locale, or an empty string if no translation is available.
        ///     Never returns null.
        /// </returns>
        /// <remarks>
        /// This method includes comprehensive null safety checks. If Translations is null,
        /// an empty string is returned immediately. The fallback logic ensures that a valid
        /// string is always returned, preventing NullReferenceExceptions in consuming code.
        /// </remarks>
        public string Get(string locale = null)
        {
            // Defensive check for null Translations (shouldn't happen after constructor fix)
            if (Translations == null)
                return string.Empty;

            var key = locale ?? CurrentLocale ?? Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

            if (string.IsNullOrEmpty(key) || !Translations.ContainsKey(key))
            {
                key = FallbackLocale ?? Translations.Keys.FirstOrDefault() ?? string.Empty;
            }

            Translations.TryGetValue(key, out var value);

            return value ?? string.Empty;
        }

        /// <inheritdoc />
        public override string ToString() => Get();


        /// <summary>
        /// Convert the current Translatable to String
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static implicit operator string(Translatable v)
        {
            return v.Get();
        }

        /// <summary>
        /// Set the CurrentLocale and Chain 
        /// </summary>
        /// <param name="locale">The locale to be used</param>
        /// <returns>Translatable</returns>
        public Translatable WithLocale(string locale)
        {
            CurrentLocale = locale;
            return this;
        }

        private static string LocaleExtract(string property, string locale) => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public class TranslatableJsonConverter : JsonConverter<Translatable>
    {
        /// <inheritdoc />
        public override Translatable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new Translatable(reader.GetString());
        }
        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Translatable value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
