
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveRealmCaching.Interfaces;
using ReactiveRealmCaching.Services;
using Realms;
using Xamarin.Essentials;

namespace ReactiveRealmCaching
{
    public class CachingService : ICachingService
    {
        public async Task<bool> CheckIfEndpointExistsAsync(Uri? url)
        {
            var connected = Connectivity.NetworkAccess == NetworkAccess.Internet || Connectivity.NetworkAccess == NetworkAccess.ConstrainedInternet;
            return await Task.FromResult(connected);
        }
        
        private readonly IRealmService _realmService;
        private readonly IMapper _mapper;
        private IHostBuilder _hostBuilder;
        private IServiceProvider _serviceProvider;

        public CachingService(MapperConfiguration mapperConfiguration)
        {
            var host = new HostBuilder()
                .ConfigureHostConfiguration(c =>
                {
                    c.AddCommandLine(new [] { $"ContentRoot={FileSystem.AppDataDirectory}" });
                    // c.AddJsonStream(stream);
                })
                // .ConfigureServices(configureServices)
                .ConfigureServices((c, x) =>
                {
                    x.AddSingleton<IRealmService, RealmService>();
                    x.AddSingleton(provider => mapperConfiguration.CreateMapper());
                });
            
                _hostBuilder = host;
                _serviceProvider = host.Build().Services;

               _realmService = _serviceProvider.GetService<IRealmService>();
               _mapper = _serviceProvider.GetService<IMapper>();
        }

        public void UpdateDatabaseId(string id)
        {
            _realmService.UpdateDatabaseId(id);
        }

        private bool CheckIfEndpointExists()
        {
            return CheckIfEndpointExistsAsync(null).Result;
        }

        /// <summary>
        /// Sets up an observable pattern to use with the replay service 
        /// </summary>
        /// <param name="storeAndAddToReplayTask">this task should store data in realm and add data to the replay service</param>
        /// <param name="replayQueueCompleteTask">this task is used to perform actions when the replay service queue completes</param>
        /// <typeparam name="TAppModel"></typeparam>
        /// <returns>an observable stream that gives back the defined type</returns>
        // public IObservable<TAppModel> StoreAndReplay<TAppModel>(Func<TAppModel> storeAndAddToReplayTask, Func<IObserver<TAppModel>, QueueRanToCompletionEventArgs, object> replayQueueCompleteTask)
        // {
        //     return Observable.Create((IObserver<TAppModel> observer) =>
        //     {
        //         Observable.FromEventPattern<EventHandler<QueueRanToCompletionEventArgs>, QueueRanToCompletionEventArgs>(
        //             h => _replayService.UpdateComplete += h,
        //             h => _replayService.UpdateComplete -= h).Take(1).Subscribe(i =>
        //         {
        //             Console.WriteLine($"Queue Id : {i.EventArgs.QueueId} - Did Complete : {i.EventArgs.DidComplete}");
        //             replayQueueCompleteTask(observer, i.EventArgs); 
        //             observer.OnCompleted(); 
        //         });
        //         observer.OnNext(storeAndAddToReplayTask());
        //         return Disposable.Create(() => Console.WriteLine("Observer has unsubscribed"));   
        //     }).Take(2);
        // }

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
        public IObservable<TAppModel> GetAndFetchLatestItem<TAppModel, TDto>(Func<Task<TDto>> fetchFromApiTask,
            Func<TAppModel, TAppModel> storeToDomLibTask,
            Func<Task<TAppModel>> fetchFromDomLibTask)
        {
            var fetch = Observable.Defer(() => Observable.Return(default(TDto)))
                .SelectMany(_ =>
                {
                    return Observable.If(CheckIfEndpointExists, Observable.FromAsync(fetchFromApiTask)
                        .Select(dto => _mapper.Map<TDto, TAppModel>(dto))
                        .SelectMany(appModel =>
                        {
                            storeToDomLibTask(appModel);
                            return Observable.FromAsync(async () => await fetchFromDomLibTask());
                        }));
                })
                .SubscribeOn(TaskPoolScheduler.Default);

            var result = Observable.FromAsync(async () => await fetchFromDomLibTask());
            return result.Concat(fetch)
                .Multicast(new ReplaySubject<TAppModel>())
                .RefCount()
                .Catch<TAppModel, Exception>(e =>
                {
                    Trace.WriteLine($"Exception in GetAndFetchLatestItem processing type {typeof(TAppModel)}: {e.Message}");
                    return Observable.Throw<TAppModel>(e);
                });
        }

        public async Task InsertAsync<TAppModel, TEntity>(TAppModel item)
            where TEntity : RealmObject
        {
            try
            {
                _ = _realmService.WriteToRealmSingle<TAppModel, TEntity>(item);
                await Task.Yield();
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Exception in Insert processing type {typeof(TEntity)}: {e.Message}");
                throw;
            }
        }

        public async Task BulkInsertAsync<TAppModel, TEntity>(IEnumerable<TAppModel> items)
            where TEntity : RealmObject
        {
            try
            {
                _ = _realmService.WriteToRealmMany<TAppModel, TEntity>(items.ToList());
                await Task.Yield();
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Exception in BulkInsert processing type {typeof(IEnumerable<TEntity>)}: {e.Message}");
                throw;
            }
        }

        public IObservable<TResult> SaveAndPush<TAppModel, TEntity, TDto, TFetchResult, TResult>(Func<TDto, Task<TFetchResult>> pushTask,
            Func<TFetchResult, TAppModel> storeToRealm, TAppModel item)
            where TEntity : RealmObject, ICanSync
            where TFetchResult : ISyncResult<TDto>
            where TResult : ISyncResult<TAppModel>
        {
            var entity = _mapper.Map<TAppModel, TEntity>(item);
            entity.ShouldSync = true;

            using var realm = _realmService.CreateUserRealm();
            realm.Write(() => realm.Add(entity, true));
            realm.Refresh();

            var firstReference = ThreadSafeReference.Create(entity);
            var dtoItem = _mapper.Map<TEntity, TDto>(entity);

            return Observable.Defer(() => Observable.Return(default(TFetchResult)))
                .SelectMany(_ =>
                {
                    return Observable.FromAsync(() => pushTask(dtoItem))
                        .Timeout(TimeSpan.FromSeconds(30))
                        .Retry(3)
                        .Do(o =>
                        {
                            // foreach item, if there was no error then mark the sync flag as false
                            if (o.DidError)
                            {
                                return;
                            }
                            MarkEntitySynced(firstReference);
                        })
                        .Catch<TFetchResult, Exception>(e =>
                        {
                            Trace.WriteLine($"Exception in SaveAndPush Inner processing type {typeof(TFetchResult)}: {e.Message}");
                            return Observable.Throw<TFetchResult>(e);
                        });
                })
                .Select(x =>
                {
                    var result = Activator.CreateInstance<TResult>();
                    if (x.DidError == false)
                    {
                        var model = storeToRealm(x);
                        result.Object = model;
                    }
                    else
                    {
                        result.Object = item;
                    }

                    result.DidError = x.DidError;
                    result.Exception = x.Exception;

                    return result;
                })
                .Catch<TResult, Exception>(e =>
                {
                    Console.WriteLine($"Exception in SaveAndPush Outer processing type {typeof(TResult)}: {e.Message}");
                    return Observable.Throw<TResult>(e);
                });
        }

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
        public IObservable<IList<TAppModel>> GetAndFetchLatestList<TAppModel, TDto>(
            Func<Task<IList<TDto>>> fetchFromApiTask,
            Func<IList<TAppModel>, IList<TAppModel>> storeToDomLibTask,
            Func<Task<IList<TAppModel>>> fetchFromDomLibTask)
        {
            try
            {
                var fetch = Observable.Defer(() => Observable.Return(default(TDto)))
                    .SelectMany(_ =>
                    {
                        return Observable.If(CheckIfEndpointExists, Observable.FromAsync(fetchFromApiTask)
                            .Select(x => _mapper.Map<IList<TDto>, IList<TAppModel>>(x))
                            .SelectMany(x =>
                            {
                                storeToDomLibTask(x);
                                return Observable.FromAsync(async () => await fetchFromDomLibTask());
                            }));
                    })
                    .SubscribeOn(TaskPoolScheduler.Default);

                var result = Observable.FromAsync(async () => await fetchFromDomLibTask());

                return result.Concat(fetch)
                    .Multicast(new ReplaySubject<IList<TAppModel>>())
                    .RefCount()
                    .Catch<IList<TAppModel>, Exception>(e =>
                    {
                        Trace.WriteLine($"Exception in GetAndFetchLatestList Inner processing type {typeof(IList<TAppModel>)}: {e.Message}");
                        return Observable.Throw<IList<TAppModel>>(e);
                    });
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Exception in GetAndFetchLatestList Outer processing type {typeof(IList<TAppModel>)}: {e.Message}");
                return Observable.Throw<IList<TAppModel>>(e);
            }
        }

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
        public IObservable<IList<TAppModel>> GetAndFetchLatestList<TAppModel, TDto>(
            Func<Task<IList<TDto>>> fetchFromApiTask,
            Action<IList<TDto>> storeToDomLibTask,
            Func<Task<IList<TAppModel>>> fetchFromDomLibTask)
        {
            var fetch = Observable.Defer(() => Observable.Return(default(IList<TDto>)))
                .SelectMany(_ =>
                {
                    return Observable.If(CheckIfEndpointExists, Observable.FromAsync(fetchFromApiTask)
                        .SelectMany(dtoItems =>
                        {
                            storeToDomLibTask(dtoItems);
                            return Observable.FromAsync(async () => await fetchFromDomLibTask());
                        }));
                })
                .SubscribeOn(TaskPoolScheduler.Default);

            var result = Observable.FromAsync(async () => await fetchFromDomLibTask());

            return result.Concat(fetch)
                .Multicast(new ReplaySubject<IList<TAppModel>>())
                .RefCount()
                .Catch<IList<TAppModel>, Exception>(e =>
                {
                    Trace.WriteLine($"Exception in GetAndFetchLatestList processing type {typeof(IList<TAppModel>)}: {e.Message}");
                    return Observable.Throw<IList<TAppModel>>(e);
                });
        }

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
        public IObservable<IList<TAppModel>> GetAndFetchLatestList<TAppModel, TEntity, TDto>(Func<Task<IList<TDto>>> fetchTask,
            Expression<Func<TEntity, bool>>? cacheValidationPredicate = null,
            bool includeLocalDeletes = false,
            Func<IList<TAppModel>, IList<TAppModel>>? preCachingOperation = null,
            string? filterClause = null)
            where TEntity : RealmObject, IHaveAnObjectId, ICanBeDeleted
        {
            var fetch = Observable.Defer(() => Observable.Return(default(IList<TDto>)))
                .SelectMany(_ =>
                {
                    return Observable.If(CheckIfEndpointExists, Observable.FromAsync(fetchTask)
                        .SelectMany(dtoItems =>
                        {
                            try
                            {
                                var appModelList = _mapper.Map<IList<TDto>, IList<TAppModel>>(dtoItems);

                                if (preCachingOperation != null)
                                {
                                    var newList = preCachingOperation(appModelList);
                                    appModelList = new List<TAppModel>(newList);
                                }

                                _realmService.WriteToRealmMany<TAppModel, TEntity>(appModelList);

                                var items = _realmService.ReadFromRealmMany<TAppModel, TEntity>(
                                    cacheValidationPredicate, filterClause);
                                return Observable.Return(items);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                            }

                            return null;
                        }));
                });

            var items = _realmService.ReadFromRealmMany<TAppModel, TEntity>(cacheValidationPredicate, filterClause);
            var result = Observable.Return(items);

            return result.Concat(fetch)
                .Multicast(new ReplaySubject<IList<TAppModel>>())
                .RefCount()
                .Catch<IList<TAppModel>, Exception>(e =>
                {
                    Trace.WriteLine($"Exception in GetAndFetchLatestList processing type {typeof(IList<TAppModel>)}: {e.Message}");
                    return Observable.Throw<IList<TAppModel>>(e);
                });
        }
        
        public IObservable<TAppModel> GetAndFetchLatestItem<TAppModel, TEntity, TDto>(Func<Task<TDto>> fetchTask, 
            Expression<Func<TEntity, bool>>? cacheValidationPredicate = null, 
            bool includeLocalDeletes = false, 
            Func<TAppModel, TAppModel>? preCachingOperation = null,
            string? filterClause = null) where TEntity : RealmObject, IHaveAnObjectId, ICanBeDeleted
        {
            var fetch = Observable.Defer(() => Observable.Return(default(TDto)))
                .SelectMany(_ =>
                {
                    return Observable.If(CheckIfEndpointExists, Observable.FromAsync(fetchTask)
                        .SelectMany(dtoItem =>
                        {
                            if (dtoItem == null) return Observable.Empty<TAppModel>();

                            var appModel = _mapper.Map<TDto, TAppModel>(dtoItem);

                            if (preCachingOperation != null)
                            {
                                var newItem = preCachingOperation(appModel);
                                appModel = newItem;
                            }

                            _realmService.WriteToRealmSingle<TAppModel, TEntity>(appModel);

                            var item = _realmService.ReadFromRealmSingle<TAppModel, TEntity>(cacheValidationPredicate, filterClause);
                            return Observable.Return(item);
                        }));
                });

            var items = _realmService.ReadFromRealmSingle<TAppModel, TEntity>(cacheValidationPredicate, filterClause);
            var result = Observable.Return(items);

            return result.Concat(fetch)
                .Multicast(new ReplaySubject<TAppModel>())
                .RefCount()
                .Catch<TAppModel, Exception>(e =>
                {
                    Trace.WriteLine($"Exception in GetAndFetchLatestList processing type {typeof(TAppModel)}: {e.Message}");
                    return Observable.Throw<TAppModel>(e);
                });
        }

        public async Task FetchLatestAsync<TDto, TEntity>(Func<Task<IList<TDto>>> fetchTask, Expression<Func<TEntity, bool>>? cacheValidationPredicate = null) where TEntity : RealmObject, IHaveAnObjectId, ICanBeDeleted
        {
            var items = await fetchTask();

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // items can be null
            if (!items.IsNullOrEmpty())
            {
                _realmService.WriteToRealmMany<TDto, TEntity>(items);
                _realmService.MarkAsDeletedRealm<TDto, TEntity>(cacheValidationPredicate);
                _realmService.RemoveDeletedFromRealmMany<TEntity>();
                // _syncStatusService.StepFilesSynched(items);
            }
        }

        public T ResolveEntity<T>(ThreadSafeReference.Object<T>? entity) where T : RealmObject
        {
            return _realmService.ResolveReference(entity);
        }

        public IList<T> ResolveEntityList<T>(ThreadSafeReference.List<T>? entities) where T : RealmObject
        {
            return _realmService.ResolveReference(entities);
        }

        private void MarkEntitySynced<TEntity>(ThreadSafeReference.Object<TEntity> reference)
            where TEntity : RealmObject, ICanSync
        {
            using var realm = _realmService.CreateUserRealm();
            var entity = realm.ResolveReference(reference);

            if (entity != null)
            {
                realm.Write(() =>
                {
                    entity.ShouldSync = false;
                });
            }

            realm.Refresh();
        }
    }
}
