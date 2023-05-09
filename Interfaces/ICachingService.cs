using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ReactiveRealmCaching.Interfaces;
using Realms;

namespace ReactiveRealmCaching.Interfaces
{

	public interface ICachingService
	{
		Task<bool> CheckIfEndpointExistsAsync(Uri url);

		/// <summary>
		/// Gets local data asynchronously from the fetchFromDomLibTask 
		/// and simultaneously requests remote data asynchronously from the fetchFromApiTask. If there is 
		/// updated data from the remote request, the remote data will get updated on the local database.
		/// </summary>
		/// <typeparam name="TAppModel">Domain type for the application</typeparam>
		/// <typeparam name="TDto">Domain type for the remote data request</typeparam>
		/// <param name="fetchFromApiTask"></param>
		/// <param name="storeToDomLibTask"></param>
		/// <param name="fetchFromDomLibTask"></param>
		/// <returns></returns>
		IObservable<TAppModel> GetAndFetchLatestItem<TAppModel, TDto>(Func<Task<TDto>> fetchFromApiTask,
			Func<TAppModel, TAppModel> storeToDomLibTask,
			Func<Task<TAppModel>> fetchFromDomLibTask);

		IObservable<TAppModel> GetAndFetchLatestItem<TAppModel, TEntity, TDto>(Func<Task<TDto>> fetchTask,
			Expression<Func<TEntity, bool>> cacheValidationPredicate = null,
			bool includeLocalDeletes = false,
			Func<TAppModel, TAppModel> preCachingOperation = null,
			string filterClause = null) where TEntity : RealmObject, IHaveAnObjectId, ICanBeDeleted;

		Task InsertAsync<TAppModel, TEntity>(TAppModel item)
			where TEntity : RealmObject;

		Task BulkInsertAsync<TAppModel, TEntity>(IEnumerable<TAppModel> items)
			where TEntity : RealmObject;

		IObservable<TResult> SaveAndPush<TAppModel, TEntity, TDto, TFetchResult, TResult>(
			Func<TDto, Task<TFetchResult>> pushTask,
			Func<TFetchResult, TAppModel> storeToRealm, TAppModel item)
			where TEntity : RealmObject, ICanSync
			where TFetchResult : ISyncResult<TDto>
			where TResult : ISyncResult<TAppModel>;

		/// <summary>
		/// Gets local data asynchronously from the fetchFromDomLibTask 
		/// and simultaneously requests remote data asynchronously from the fetchFromApiTask. If there is 
		/// updated data from the remote request, the remote data will get updated on the local database.
		/// </summary>
		/// <typeparam name="TAppModel">Domain collection type for the application</typeparam>
		/// <typeparam name="TDto">Domain collection type for the remote data request</typeparam>
		/// <param name="fetchFromApiTask"></param>
		/// <param name="storeToDomLibTask"></param>
		/// <param name="fetchFromDomLibTask"></param>
		/// <returns></returns>
		IObservable<IList<TAppModel>> GetAndFetchLatestList<TAppModel, TDto>(
			Func<Task<IList<TDto>>> fetchFromApiTask,
			Func<IList<TAppModel>, IList<TAppModel>> storeToDomLibTask,
			Func<Task<IList<TAppModel>>> fetchFromDomLibTask);

		/// <summary>
		/// Gets data from the local database via fetchFromDomLibTask, and simultaneously requests source data 
		/// asynchronously from fetchFromApiTask. If there is updated data from the source request, storeToDomLibTask
		/// will be called to allow the caller to persist that data back to the local database, and fetchFromDomLibTask
		/// will be called again to allow the caller to return the updated local data as the final method result.
		/// </summary>
		/// <typeparam name="TAppModel">Domain collection type for the application</typeparam>
		/// <typeparam name="TDto">Domain type for the source data request</typeparam>
		/// <param name="fetchFromApiTask"></param>
		/// <param name="storeToDomLibTask"></param>
		/// <param name="fetchFromDomLibTask"></param>
		/// <returns></returns>
		IObservable<IList<TAppModel>> GetAndFetchLatestList<TAppModel, TDto>(
			Func<Task<IList<TDto>>> fetchFromApiTask,
			Action<IList<TDto>> storeToDomLibTask,
			Func<Task<IList<TAppModel>>> fetchFromDomLibTask);

		/// <summary>
		/// Gets data from the local database and simultaneously requests source data asynchronously from 
		/// the fetchTask. If there is updated data from the source request, the source data will get updated 
		/// on the local database.
		/// </summary>
		/// <typeparam name="TAppModel">Domain collection type for the application</typeparam>
		/// <typeparam name="TEntity">Domain collection type for the local storage</typeparam>
		/// <typeparam name="TDto">Domain type for the source data request</typeparam>
		/// <param name="fetchTask"></param>
		/// <param name="cacheValidationPredicate"></param>
		/// <param name="includeLocalDeletes"></param>
		/// <param name="preCachingOperation"></param>
		/// <param name="filterClause"></param>
		/// <returns></returns>
		IObservable<IList<TAppModel>> GetAndFetchLatestList<TAppModel, TEntity, TDto>(Func<Task<IList<TDto>>> fetchTask,
			Expression<Func<TEntity, bool>> cacheValidationPredicate = null,
			bool includeLocalDeletes = false,
			Func<IList<TAppModel>, IList<TAppModel>> preCachingOperation = null,
			string filterClause = null)
			where TEntity : RealmObject, IHaveAnObjectId, ICanBeDeleted;

		Task FetchLatestAsync<TDto, TEntity>(Func<Task<IList<TDto>>> fetchTask,
			Expression<Func<TEntity, bool>> cacheValidationPredicate = null)
			where TEntity : RealmObject, IHaveAnObjectId, ICanBeDeleted;

		T ResolveEntity<T>(ThreadSafeReference.Object<T> entity) where T : RealmObject;
		IList<T> ResolveEntityList<T>(ThreadSafeReference.List<T> entities) where T : RealmObject;
	}
}