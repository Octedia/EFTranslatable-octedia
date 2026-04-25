#nullable enable
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
    /// Translatable is a reference type that lets an Entity Framework property hold
    /// per-locale translations as a JSON dictionary in the database.
    /// </summary>
    /// <remarks>
    /// Changed from a value type to a reference type in v2.0 so that database NULL,
    /// nullable-reference annotations, and "no value" can all be modelled cleanly.
    /// </remarks>
    [JsonConverter(typeof(TranslatableJsonConverter))]
    public sealed class Translatable
    {
        /// <summary>
        /// A fallback locale to use if the requested locale has no translation.
        /// Defaults to "en".
        /// </summary>
        public static string FallbackLocale { get; set; } = "en";

        /// <summary>
        /// An empty Translatable instance with no translations.
        /// </summary>
        public static Translatable Empty => new Translatable();

        /// <summary>
        /// The current locales with their translations.
        /// </summary>
        public Dictionary<string, string> Translations { get; }

        /// <summary>
        /// The current locale the property is using.
        /// </summary>
        public string? CurrentLocale { get; set; }

        /// <summary>
        /// Creates an empty Translatable.
        /// </summary>
        public Translatable()
        {
            Translations = new Dictionary<string, string>();
        }

        /// <summary>
        /// Builds a Translatable from a JSON string.
        /// Null, empty, or malformed JSON yields an empty dictionary.
        /// </summary>
        public Translatable(string? json)
        {
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
            catch (Exception)
            {
                Translations = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Builds a Translatable from an existing dictionary of locale → translation.
        /// </summary>
        public Translatable(Dictionary<string, string>? translations)
        {
            Translations = translations ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Serializes the translations to JSON. Always returns valid JSON; never null.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(Translations, new JsonSerializerOptions()
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true
            });
        }

        /// <summary>
        /// Sets or updates the translation for the given locale.
        /// If <paramref name="locale"/> is null, falls back to <see cref="CurrentLocale"/>
        /// then to the current thread's UI culture.
        /// </summary>
        public Translatable Set(string value, string? locale = null)
        {
            var key = locale
                      ?? CurrentLocale
                      ?? Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

            Translations[key] = value;
            return this;
        }

        /// <summary>
        /// Returns the translation for the given locale with intelligent fallback.
        /// Resolution order: <paramref name="locale"/> → <see cref="CurrentLocale"/> →
        /// thread UI culture → <see cref="FallbackLocale"/> → first available translation.
        /// Never returns null.
        /// </summary>
        public string Get(string? locale = null)
        {
            var key = locale
                      ?? CurrentLocale
                      ?? Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

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
        /// Implicit conversion to string returns the current locale's translation.
        /// A null Translatable converts to an empty string.
        /// </summary>
        public static implicit operator string(Translatable? v) => v?.Get() ?? string.Empty;

        /// <summary>
        /// Sets the CurrentLocale and returns this instance for chaining.
        /// </summary>
        public Translatable WithLocale(string? locale)
        {
            CurrentLocale = locale;
            return this;
        }

        // Marker method registered with EF as a database function. EF rewrites calls to this
        // into JSON_VALUE / json_extract at SQL translation time; the method body never runs.
        private static string LocaleExtract(string property, string locale) =>
            throw new NotSupportedException();
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
