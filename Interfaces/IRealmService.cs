using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ReactiveRealmCaching.Interfaces;
using Realms;

namespace ReactiveRealmCaching.Interfaces
{

	internal interface IRealmService
	{
		void UpdateDatabaseId(string id);
		
		/// <summary>
		/// Initializes user realm database
		/// </summary>
		/// <returns></returns>
		Realm CreateUserRealm();

		/// <summary>
		/// Reads Entity data using the optional predicate and returns the converted mapped item.
		/// </summary>
		/// <typeparam name="TMapped"></typeparam>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="expression"></param>
		/// <param name="filterClause"></param>
		/// <returns></returns>
		TMapped ReadFromRealmSingle<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression,
			string? filterClause = null) where TEntity : RealmObject;

		/// <summary>
		/// Reads Entity data using the optional predicate and returns the converted mapped item list.
		/// </summary>
		/// <typeparam name="TMapped"></typeparam>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="expression"></param>
		/// <param name="filterClause"></param>
		/// <returns></returns>
		IList<TMapped> ReadFromRealmMany<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression,
			string? filterClause = null)
			where TEntity : RealmObject;

		/// <summary>
		/// Reads Entity data using the optional Realm Filter Clause and returns the converted mapped item list.
		/// </summary>
		/// <typeparam name="TMapped"></typeparam>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filterClause"></param>
		/// <returns></returns>
		IList<TMapped> ReadFromRealmManyFilter<TMapped, TEntity>(string filterClause) where TEntity : RealmObject;

		/// <summary>
		/// Writes the mapped item to the database.
		/// </summary>
		/// <typeparam name="TMapped"></typeparam>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="mapped"></param>
		/// <returns></returns>
		TMapped WriteToRealmSingle<TMapped, TEntity>(TMapped mapped) where TEntity : RealmObject;

		/// <summary>
		/// Writes the mapped item list to the database.
		/// </summary>
		/// <typeparam name="TMapped"></typeparam>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="mappedList"></param>
		/// <returns></returns>
		IList<TMapped> WriteToRealmMany<TMapped, TEntity>(IList<TMapped> mappedList) where TEntity : RealmObject;

		/// <summary>
		/// Get the total count for a specified Realm type
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="expression"></param>
		/// <returns></returns>
		int GetRealmCount<TEntity>(Expression<Func<TEntity, bool>>? expression = null) where TEntity : RealmObject;

		/// <summary>
		/// Updates the mapped item as marked for deletion to the database.
		/// </summary>
		/// <typeparam name="TMapped"></typeparam>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="expression"></param>
		/// <returns></returns>
		IList<TMapped> MarkAsDeletedRealm<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression)
			where TEntity : RealmObject, ICanBeDeleted;

		/// <summary>
		/// Removes the Entity item list from the database based on the predicate.
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="expression"></param>
		void RemoveFromRealmMany<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : RealmObject;

		/// <summary>
		/// Removes the Entity item from the database based on the predicate.
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="expression"></param>
		void RemoveFromRealmSingle<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : RealmObject;

		/// <summary>
		/// Removes the Entity item list from the database that are marked for deletion.
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		void RemoveDeletedFromRealmMany<TEntity>() where TEntity : RealmObject, ICanBeDeleted;

		/// <summary>
		/// Returns the same object as the one referenced when the Realms.ThreadSafeReference.Object`1 
		/// was first created, but resolved for the current Realm for this thread. 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reference"></param>
		/// <returns></returns>
		T ResolveReference<T>(ThreadSafeReference.Object<T>? reference) where T : RealmObjectBase;

		/// <summary>
		/// Returns the same collection as the one referenced when the Realms.ThreadSafeReference.List`1
		/// was first created, but resolved for the current Realm for this thread.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="reference"></param>
		/// <returns></returns>
		IList<T> ResolveReference<T>(ThreadSafeReference.List<T>? reference) where T : RealmObjectBase;

		/// <summary>
		/// Edits a group of entities and returns the edited list
		/// </summary>
		/// <typeparam name="TMapped"></typeparam>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="expression"></param>
		/// <param name="edit"></param>
		/// <returns></returns>
		IList<TMapped> EditListRealmMany<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression,
			Func<TEntity, object> edit)
			where TEntity : RealmObject;

		event EventHandler<string> RealmChanged;
	}
}