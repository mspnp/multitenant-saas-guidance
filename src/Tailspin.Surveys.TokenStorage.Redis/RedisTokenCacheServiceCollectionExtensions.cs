// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Tailspin.Surveys.TokenStorage.Redis;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TokenStorageServiceCollectionExtensions
    {
        public static void AddRedisTokenStorage(this IServiceCollection services)
        {
            // Add the default token service
            services.AddScoped<ITokenCacheService, RedisTokenCacheService>();
        }
    }
}
