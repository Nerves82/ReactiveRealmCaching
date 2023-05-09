
    
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using ReactiveRealmCaching.Interfaces;
using Realms;

namespace ReactiveRealmCaching.Services
{
    public class RealmService : IRealmService
    {
        public event EventHandler<string>? RealmChanged;
        private readonly IMapper _mapper;

        public RealmService(IMapper mapper)
        {
            _mapper = mapper;
        }

        public static string DatabasePath
        {
            get
            {
                var myPath = RealmConfigurationBase.GetPathToRealm();
                myPath = myPath.Substring(0, myPath.LastIndexOf('/'));
                return myPath;
            }
        }

        protected virtual string UserId => "eorughlasieurhbgsiuhbgqawlieurh";

        protected virtual RealmConfiguration RealmConfiguration
        {
            get
            {
                if (string.IsNullOrWhiteSpace(UserId))
                    throw new InvalidOperationException(nameof(UserId));

                return new RealmConfiguration($"{UserId}.realm");
            }
        }

        /// <summary>
        /// Initializes user realm database
        /// </summary>
        /// <returns></returns>
        public Realms.Realm CreateUserRealm()
        {
            var config = RealmConfiguration;

#if LOCAL
               config.ShouldDeleteIfMigrationNeeded = true;
#elif QA
               config.ShouldDeleteIfMigrationNeeded = true;
#elif DEBUG
            config.ShouldDeleteIfMigrationNeeded = true;
            Console.WriteLine($"Database Path {config.DatabasePath}");
#elif Stage
                config.ShouldDeleteIfMigrationNeeded = false;
#elif RELEASE
                config.ShouldDeleteIfMigrationNeeded = false;
#endif

            var realm = Realms.Realm.GetInstance(config);
            realm.RealmChanged += (sender, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(UserId))
                {
                    RealmChanged?.Invoke(this, UserId);
                }
            };
            return realm;
        }

        /// <summary>
        /// Reads Entity data using the optional expression and returns the converted mapped item.
        /// </summary>
        /// <typeparam name="TMapped"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="expression"></param>
        /// <param name="filterClause"></param>
        /// <returns></returns>
        public TMapped ReadFromRealmSingle<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression,
            string? filterClause = null)
            where TEntity : RealmObject
        {
            using var realm = CreateUserRealm();
            TEntity single;

            if (string.IsNullOrWhiteSpace(filterClause))
            {
                single = expression != null
                    ? realm.All<TEntity>().FirstOrDefault(expression)
                    : realm.All<TEntity>().FirstOrDefault();
            }
            else
            {
                single = expression != null
                    ? realm.All<TEntity>().Filter(filterClause).FirstOrDefault(expression)
                    : realm.All<TEntity>().Filter(filterClause).FirstOrDefault();
            }


            realm.Refresh();
            return _mapper.Map<TEntity, TMapped>(single);
        }

        /// <summary>
        /// Reads Entity data using the optional expression and returns the converted mapped item list.
        /// </summary>
        /// <typeparam name="TMapped"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="expression"></param>
        /// <param name="filterClause"></param>
        /// <returns></returns>
        public IList<TMapped> ReadFromRealmMany<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression,
            string? filterClause = null)
            where TEntity : RealmObject
        {
            using var realm = CreateUserRealm();
            List<TEntity> all;

            if (string.IsNullOrWhiteSpace(filterClause))
            {
                var testAll = realm.All<TEntity>();
                all = expression != null
                    ? realm.All<TEntity>().Where(expression).ToList()
                    : realm.All<TEntity>().ToList();
            }
            else
            {
               
                all = expression != null
                    ? realm.All<TEntity>().Where(expression).Filter(filterClause).ToList()
                    : realm.All<TEntity>().Filter(filterClause).ToList();
            }


            realm.Refresh();
            return _mapper.Map<List<TEntity>, List<TMapped>>(all);
        }

        /// <summary>
        /// Reads Entity data using the optional Realm Filter Clause and returns the converted mapped item list.
        /// </summary>
        /// <typeparam name="TMapped"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="filterClause"></param>
        /// <returns></returns>
        public IList<TMapped> ReadFromRealmManyFilter<TMapped, TEntity>(string filterClause)
            where TEntity : RealmObject
        {
            using var realm = CreateUserRealm();

            List<TEntity> all = !string.IsNullOrWhiteSpace(filterClause)
                ? realm.All<TEntity>().Filter(filterClause).ToList()
                : realm.All<TEntity>().ToList();

            realm.Refresh();
            return _mapper.Map<List<TEntity>, List<TMapped>>(all);
        }

        /// <summary>
        /// Writes the mapped item to the database.
        /// </summary>
        /// <typeparam name="TMapped"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="mapped"></param>
        /// <returns></returns>
        public TMapped WriteToRealmSingle<TMapped, TEntity>(TMapped mapped)
            where TEntity : RealmObject
        {
            var entity = _mapper.Map<TMapped, TEntity>(mapped);
            TEntity returnEntity = null;

            using var realm = CreateUserRealm();
            realm.Write(() => { returnEntity = realm.Add(entity, true); });
            realm.Refresh();

            var returnMapped = _mapper.Map<TEntity, TMapped>(returnEntity);
            return returnMapped;
        }

        /// <summary>
        /// Writes the mapped item list to the database.
        /// </summary>
        /// <typeparam name="TMapped"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="mappedList"></param>
        /// <returns></returns>
        public IList<TMapped> WriteToRealmMany<TMapped, TEntity>(IList<TMapped> mappedList)
            where TEntity : RealmObject
        {
            var entityList = _mapper.Map<IList<TMapped>, IList<TEntity>>(mappedList);
            var returnMappedList = new List<TMapped>();

            if (entityList != null && entityList.Any())
            {
                using var realm = CreateUserRealm();

                var returnEntityList = new List<TEntity>();
                realm.Write(() =>
                {
                    foreach (var o in entityList)
                    {
                        returnEntityList.Add(realm.Add(o, true));
                    }
                });
                realm.Refresh();

                returnMappedList = _mapper.Map<List<TEntity>, List<TMapped>>(returnEntityList);
            }

            return returnMappedList;
        }

        /// <summary>
        /// Get the total count for a specified Realm type
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public int GetRealmCount<TEntity>(Expression<Func<TEntity, bool>>? expression = null)
            where TEntity : RealmObject
        {
            using var realm = CreateUserRealm();
            int count = expression != null
                ? realm.All<TEntity>().Count(expression)
                : realm.All<TEntity>().Count();

            realm.Refresh();
            return count;
        }

        /// <summary>
        /// Updates the mapped item list as marked for deletion to the database.
        /// </summary>
        /// <typeparam name="TMapped"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public IList<TMapped> MarkAsDeletedRealm<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression)
            where TEntity : RealmObject, ICanBeDeleted
        {
            using var realm = CreateUserRealm();

            IQueryable<TEntity> deletingEntityList = expression != null
                ? realm.All<TEntity>().Where(expression)
                : realm.All<TEntity>();

            realm.Write(() =>
            {
                foreach (var entity in deletingEntityList)
                {
                    entity.IsMarkedForDeletion = true;
                }
            });

            realm.Refresh();
            var deletingMappedList = _mapper.Map<IList<TEntity>, IList<TMapped>>(deletingEntityList.ToList());
            return deletingMappedList;
        }

        /// <summary>
        /// Removes the Entity item list from the database based on the expression.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="expression"></param>
        public void RemoveFromRealmMany<TEntity>(Expression<Func<TEntity, bool>> expression)
            where TEntity : RealmObject
        {
            using var realm = CreateUserRealm();

            realm.Write(() => { realm.RemoveRange(realm.All<TEntity>().Where(expression)); });

            realm.Refresh();
        }

        /// <summary>
        /// Removes the Entity item from the database based on the expression.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="expression"></param>
        public void RemoveFromRealmSingle<TEntity>(Expression<Func<TEntity, bool>> expression)
            where TEntity : RealmObject
        {
            using var realm = CreateUserRealm();

            realm.Write(() =>
            {
                var entity = realm.All<TEntity>().FirstOrDefault(expression);
                realm.Remove(entity);
            });

            realm.Refresh();
        }

        /// <summary>
        /// Removes the Entity item list from the database that are marked for deletion.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        public void RemoveDeletedFromRealmMany<TEntity>()
            where TEntity : RealmObject, ICanBeDeleted
        {
            RemoveFromRealmMany<TEntity>(x => x.IsMarkedForDeletion);
        }

        /// <summary>
        /// Returns the same object as the one referenced when the Realms.ThreadSafeReference.Object`1 
        /// was first created, but resolved for the current Realm for this thread. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <returns></returns>
        public T ResolveReference<T>(ThreadSafeReference.Object<T>? reference)
            where T : RealmObjectBase
        {
            var instance = Realms.Realm.GetInstance(RealmConfiguration);
            return instance.ResolveReference(reference);
        }

        /// <summary>
        /// Returns the same collection as the one referenced when the Realms.ThreadSafeReference.List`1
        /// was first created, but resolved for the current Realm for this thread.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <returns></returns>
        public IList<T> ResolveReference<T>(ThreadSafeReference.List<T>? reference)
            where T : RealmObjectBase
        {
            var instance = Realms.Realm.GetInstance(RealmConfiguration);
            return instance.ResolveReference(reference);
        }

        /// <summary>
        /// Edits the mapped item list and writes to the database.
        /// </summary>
        /// <typeparam name="TMapped"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="expression"></param>
        /// <param name="edit"></param>
        /// <returns></returns>
        public IList<TMapped> EditListRealmMany<TMapped, TEntity>(Expression<Func<TEntity, bool>>? expression,
            Func<TEntity, object> edit)
            where TEntity : RealmObject
        {
            var returnMappedList = new List<TMapped>();
            using var realm = CreateUserRealm();

            var all = expression != null
                ? realm.All<TEntity>().Where(expression).ToList()
                : realm.All<TEntity>().ToList();

            if (all.Any())
            {
                realm.Write(() => { all.ForEach(x => edit(x)); });
                realm.Refresh();

                returnMappedList = _mapper.Map<List<TEntity>, List<TMapped>>(all);
            }

            return returnMappedList;
        }
    }
}