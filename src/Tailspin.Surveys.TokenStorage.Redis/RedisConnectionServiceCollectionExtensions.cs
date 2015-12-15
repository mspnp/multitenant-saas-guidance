// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Tailspin.Surveys.TokenStorage.Redis;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> extension methods used to add support for Redis.
    /// </summary>
    public static class RedisConnectionServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a configured singleton of <see cref="StackExchange.Redis.IConnectionMultiplexer"/> to the provided <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>.
        /// </summary>
        /// <param name="services"><see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> instance where the singleton should be added.</param>
        /// <param name="setupAction">Action method that passes a <see cref="Tailspin.Surveys.TokenStorage.Redis.RedisConnectionBuilder"/> that can be used to configure a Redis <see cref="StackExchange.Redis.IConnectionMultiplexer"/>.</param>
        /// <returns></returns>
        public static IServiceCollection AddRedisConnection(this IServiceCollection services, Action<RedisConnectionBuilder> setupAction)
        {
            var builder = new RedisConnectionBuilder();
            if (setupAction != null)
            {
                setupAction(builder);
            }

            services.AddSingleton<IConnectionMultiplexer>(factory => ConnectionMultiplexer.Connect(builder.Options));
            return services;
        }
    }
}
