using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EFTranslatable.Extensions
{
    /// <summary>
    /// Add Translatable/Localizations Methods to IQueryable
    /// </summary>
    public static class QueryableExtensions
    {
        private static MethodInfo _localeExtract;
        private static readonly object _lockObject = new object();

        private static MethodInfo LocaleExtract
        {
            get
            {
                if (_localeExtract == null)
                {
                    lock (_lockObject)
                    {
                        if (_localeExtract == null)
                        {
                            _localeExtract = typeof(Translatable)
                                .GetMethod("LocaleExtract", BindingFlags.NonPublic | BindingFlags.Static);

                            if (_localeExtract == null)
                            {
                                throw new InvalidOperationException(
                                    "Failed to find LocaleExtract method on Translatable type. " +
                                    "Cannot execute localized queries. The EFTranslatable library may be corrupted.");
                            }
                        }
                    }
                }
                return _localeExtract;
            }
        }

        /// <summary>
        /// Filter the current EntitySet where the giving property-predicate Equals to the giving String
        /// </summary>
        /// <param name="source">DbSet</param>
        /// <param name="property">The predicate to select a Translatable property</param>
        /// <param name="equalsTo">The giving String to compare property to</param>
        /// <param name="locale">The locale where the predicate will run on, If null the current Thread locale will be used instead</param>
        /// <typeparam name="TSource">EntityFrameWork Model</typeparam>
        /// <returns>IQueryable</returns>
        public static IQueryable<TSource> WhereLocalizedEquals<TSource>(this IQueryable<TSource> source,
            Expression<Func<TSource, Translatable>> property,
            string equalsTo,
            string locale = null)
        {
            if (equalsTo == null)
            {
                throw new ArgumentNullException(nameof(equalsTo),
                    "The comparison value cannot be null. Use string.Empty to search for empty translations.");
            }

            var l = locale
                    ?? Translatable.FallbackLocale
                    ?? Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

            var expr = Expression.MakeBinary(ExpressionType.Equal,
                Expression.Call(LocaleExtract!, Expression.Convert(property.Body, typeof(string)),
                    Expression.Constant($"$.{l}")),
                Expression.Constant(equalsTo));


            return source.Where(Expression.Lambda<Func<TSource, bool>>(expr, false, property.Parameters));
        }


        private static MethodInfo _localeContains;

        private static MethodInfo LocaleContains
        {
            get
            {
                if (_localeContains == null)
                {
                    lock (_lockObject)
                    {
                        if (_localeContains == null)
                        {
                            _localeContains = typeof(string)
                                .GetMethod("Contains", new[] { typeof(string) });

                            if (_localeContains == null)
                            {
                                throw new InvalidOperationException(
                                    "Failed to find string.Contains(string) method. " +
                                    "Cannot execute localized contains queries. This may indicate a .NET framework version incompatibility.");
                            }
                        }
                    }
                }
                return _localeContains;
            }
        }
        /// <summary>
        /// Filter the current EntitySet where the giving property-predicate Contains to the giving String
        /// </summary>
        /// <param name="source">DbSet</param>
        /// <param name="property">The predicate to select a Translatable property</param>
        /// <param name="contains">The giving String to check if property contains it</param>
        /// <param name="locale">The locale where the predicate will run on, If null the current Thread locale will be used instead</param>
        /// <typeparam name="TSource">EntityFrameWork Model</typeparam>
        /// <returns>IQueryable</returns>
        public static IQueryable<TSource> WhereLocalizedContains<TSource>(this IQueryable<TSource> source,
            Expression<Func<TSource, Translatable>> property,
            string contains,
            string locale = null)
        {
            if (contains == null)
            {
                throw new ArgumentNullException(nameof(contains),
                    "The search string cannot be null. Use string.Empty to search for empty content.");
            }

            var containsExpr = Expression.Constant(contains);
            var convertedBody = Expression.Convert(property.Body, typeof(string));

            if (locale == null)
            {
                var expr = Expression.Call(convertedBody, LocaleContains!, containsExpr);

                return source.Where(Expression.Lambda<Func<TSource, bool>>(expr, false, property.Parameters));
            }

            var expr2 = Expression.Call(
                Expression.Call(LocaleExtract!, convertedBody, Expression.Constant($"$.{locale}")),
                LocaleContains!, containsExpr);

            return source.Where(Expression.Lambda<Func<TSource, bool>>(expr2, false, property.Parameters));
        }

        /// <summary>
        /// Materializes the query and applies translations to all entities in one call.
        /// Use this with FromSqlRaw/FromSqlInterpolated to translate results client-side.
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from HasTranslations&lt;T&gt;</typeparam>
        /// <param name="query">The IQueryable (typically from FromSqlRaw)</param>
        /// <param name="locale">Target locale code (e.g., "en", "ar", "fr")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of translated entities</returns>
        /// <example>
        /// <code>
        /// var doctors = await context.Doctors
        ///     .FromSqlRaw("EXEC sp_GetDoctors")
        ///     .ToListWithTranslationsAsync("en");
        /// // doctors[0].Title is now a string in English
        /// </code>
        /// </example>
        public static async Task<List<T>> ToListWithTranslationsAsync<T>(
            this IQueryable<T> query,
            string locale,
            CancellationToken cancellationToken = default
        ) where T : HasTranslations<T>, new()
        {
            var entities = await query.ToListAsync(cancellationToken);
            return entities.Select(e => e.Translate(locale)).ToList();
        }

        /// <summary>
        /// Materializes the query and applies translations to all entities (synchronous version).
        /// Use this with FromSqlRaw/FromSqlInterpolated to translate results client-side.
        /// </summary>
        /// <typeparam name="T">Entity type that inherits from HasTranslations&lt;T&gt;</typeparam>
        /// <param name="query">The IQueryable (typically from FromSqlRaw)</param>
        /// <param name="locale">Target locale code (e.g., "en", "ar", "fr")</param>
        /// <returns>List of translated entities</returns>
        /// <example>
        /// <code>
        /// var doctors = context.Doctors
        ///     .FromSqlRaw("EXEC sp_GetDoctors")
        ///     .ToListWithTranslations("en");
        /// </code>
        /// </example>
        public static List<T> ToListWithTranslations<T>(
            this IQueryable<T> query,
            string locale
        ) where T : HasTranslations<T>, new()
        {
            var entities = query.ToList();
            return entities.Select(e => e.Translate(locale)).ToList();
        }
    }
}