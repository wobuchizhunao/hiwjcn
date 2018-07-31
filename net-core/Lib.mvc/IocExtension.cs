﻿using Autofac;
using Microsoft.AspNetCore.Http;
using System;

namespace Lib.mvc
{
    public static class IocExtension
    {
        [Obsolete("使用" + nameof(CurrentServiceProvider))]
        public const string HTTPCONTEXT_AUTOFAC_SCOPE_KEY = "ioc.autofac.scope.key";

        public static IServiceProvider CurrentServiceProvider(this HttpContext context) =>
            context.RequestServices ?? throw new Exception("无法获取当前ioc scope");

        [Obsolete("使用" + nameof(CurrentServiceProvider))]
        public static void SetAutofacScope(this HttpContext context, ILifetimeScope scope, string context_key = null) =>
            context.Items[context_key ?? HTTPCONTEXT_AUTOFAC_SCOPE_KEY] = scope ?? throw new ArgumentNullException(nameof(scope));

        /// <summary>
        /// 获取httpcontext.item中的scope对象，需要配置httpmodule
        /// </summary>
        [Obsolete("使用" + nameof(CurrentServiceProvider))]
        public static ILifetimeScope GetAutofacScope(this HttpContext context, string context_key = null)
        {
            var obj = context.Items[context_key ?? HTTPCONTEXT_AUTOFAC_SCOPE_KEY];
            if (obj is ILifetimeScope scope)
            {
                return scope;
            }
            throw new Exception("请求中没有缓存autofac scope");
        }
    }
}