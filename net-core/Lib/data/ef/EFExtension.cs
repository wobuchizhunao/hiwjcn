﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Lib.data.ef
{
    public static class EFExtension
    {
        public static List<T> GetListEnsureMaxCount<T>(this IRepository<T> repo,
            Expression<Func<T, bool>> where, int count, string error_msg)
            where T : IDBTable
        {
            var list = repo.GetList(where, count);
            if (list.Count >= count)
            {
                throw new Exception(error_msg);
            }
            return list;
        }

        public static async Task<List<T>> GetListEnsureMaxCountAsync<T>(this IRepository<T> repo,
            Expression<Func<T, bool>> where, int count, string error_msg)
            where T : IDBTable
        {
            var list = await repo.GetListAsync(where, count);
            if (list.Count >= count)
            {
                throw new Exception(error_msg);
            }
            return list;
        }

        /// <summary>
        /// 创建表
        /// </summary>
        public static void TryCreateTable<T>(this T context) where T : DbContext
            => context.Database.EnsureCreated();

        /// <summary>
        /// 注册fluent mapping
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="ass"></param>
        public static void RegisterTableFluentMapping(this ModelBuilder builder, params Assembly[] ass)
        {
            foreach (var a in ass)
            {
                var tps = a.GetTypes().Where(x =>
                x.IsClass
                && !x.IsAbstract
                && x.BaseType != null
                && x.BaseType.IsGenericType
                && x.BaseType.GetGenericTypeDefinition() == typeof(EFMappingBase<>));
                foreach (var t in tps)
                {
                    dynamic configurationInstance = Activator.CreateInstance(t);
                    //mapping
                    builder.ApplyConfiguration(configurationInstance);//.Add(configurationInstance);
                }
            }
        }
        
        /// <summary>
        /// 获取不跟踪的IQueryable用于查询，效率更高
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="set"></param>
        /// <returns></returns>
        public static IQueryable<T> AsNoTrackingQueryable<T>(this IQueryable<T> set) where T : class
        {
            return set.AsQueryableTrackingOrNot(false);
        }

        /// <summary>
        /// 获取追踪或者不追踪的查询对象
        /// </summary>
        public static IQueryable<T> AsQueryableTrackingOrNot<T>(this IQueryable<T> set, bool tracking) where T : class
        {
            if (tracking)
            {
                return set.AsQueryable();
            }
            else
            {
                return set.AsNoTracking().AsQueryable();
            }
        }

        /// <summary>
        /// 把实体加载到EF上下文，不重复加载
        /// Nop中的方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static T AttachEntityToContext<T>(this DbContext db, T entity) where T : DBTable
        {
            //little hack here until Entity Framework really supports stored procedures
            //otherwise, navigation properties of loaded entities are not loaded until an entity is attached to the context
            var set = db.Set<T>();
            var alreadyAttached = set.Local.FirstOrDefault(x => x.IID == entity.IID);
            if (alreadyAttached == null)
            {
                //attach new entity
                set.Attach(entity);
                return entity;
            }

            //entity is already loaded
            return alreadyAttached;
        }

    }
}
