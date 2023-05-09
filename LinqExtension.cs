using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;

namespace ReactiveRealmCaching
{

	public static class LinqExtension
	{
		public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source,
			Func<TSource, TKey> keySelector)
		{
			HashSet<TKey> seenKeys = new HashSet<TKey>();
			foreach (TSource element in source)
			{
				if (seenKeys.Add(keySelector(element)))
				{
					yield return element;
				}
			}
		}

		public static void CopyTo<T>(this IEnumerable<T>? source, ICollection<T>? dest)
		{
			if (source == null || dest == null)
			{
				return;
			}

			dest.Clear();
			foreach (var item in source)
			{
				dest.Add(item);
			}
		}

		public static bool IsEmpty<TSource>(this IEnumerable<TSource> source) => !source.Any();

		[Pure]
		public static bool IsNullOrEmpty<TSource>([NotNullWhen(false)] this IEnumerable<TSource>? source) =>
			source == null || !source.Any();
	}
}