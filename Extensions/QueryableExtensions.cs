#nullable enable
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
    /// Adds Translatable / localization-aware filter methods to <see cref="IQueryable{T}"/>.
    /// </summary>
    public static class QueryableExtensions
    {
        private static MethodInfo? _localeExtract;
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
        /// Filters the source where the selected Translatable property's value for the given locale equals
        /// <paramref name="equalsTo"/>. Translates to <c>JSON_VALUE</c> on SQL Server, <c>json_extract</c> elsewhere.
        /// </summary>
        public static IQueryable<TSource> WhereLocalizedEquals<TSource>(
            this IQueryable<TSource> source,
            Expression<Func<TSource, Translatable>> property,
            string equalsTo,
            string? locale = null)
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
                Expression.Call(LocaleExtract, Expression.Convert(property.Body, typeof(string)),
                    Expression.Constant($"$.{l}")),
                Expression.Constant(equalsTo));

            return source.Where(Expression.Lambda<Func<TSource, bool>>(expr, false, property.Parameters));
        }


        private static MethodInfo? _localeContains;

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
        /// Filters the source where the selected Translatable property's value for the given locale contains
        /// <paramref name="contains"/>. When <paramref name="locale"/> is null, the implicit string conversion
        /// of the Translatable is searched directly.
        /// </summary>
        public static IQueryable<TSource> WhereLocalizedContains<TSource>(
            this IQueryable<TSource> source,
            Expression<Func<TSource, Translatable>> property,
            string contains,
            string? locale = null)
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
                var expr = Expression.Call(convertedBody, LocaleContains, containsExpr);

                return source.Where(Expression.Lambda<Func<TSource, bool>>(expr, false, property.Parameters));
            }

            var expr2 = Expression.Call(
                Expression.Call(LocaleExtract, convertedBody, Expression.Constant($"$.{locale}")),
                LocaleContains, containsExpr);

            return source.Where(Expression.Lambda<Func<TSource, bool>>(expr2, false, property.Parameters));
        }

        /// <summary>
        /// Materializes the query and applies translations to all entities asynchronously.
        /// Use with <c>FromSqlRaw</c>/<c>FromSqlInterpolated</c> to translate results client-side.
        /// </summary>
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
        /// Materializes the query and applies translations to all entities (synchronous).
        /// </summary>
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
