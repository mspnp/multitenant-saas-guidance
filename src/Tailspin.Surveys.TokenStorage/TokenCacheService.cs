// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Tailspin.Surveys.Common;
using System.Security.Claims;

namespace Tailspin.Surveys.TokenStorage
{
    /// <summary>
    /// Returns and manages the instance of token cache to be used when making use of ADAL. 
    public abstract class TokenCacheService : ITokenCacheService
    {
        protected readonly ILoggerFactory _loggerFactory;
        protected readonly ILogger _logger;
        protected TokenCache _cache = null;

        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.TokenStorage.TokenCacheService"/>
        /// </summary>
        /// <param name="loggerFactory"><see cref="Microsoft.Extensions.Logging.ILoggerFactory"/> used to create type-specific <see cref="Microsoft.Extensions.Logging.ILogger"/> instances.</param>
        protected TokenCacheService(ILoggerFactory loggerFactory)
        {
            Guard.ArgumentNotNull(loggerFactory, nameof(loggerFactory));

            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Returns an instance of <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache"/>.
        /// </summary>
        /// <param name="userObjectId">Azure Active Directory user's ObjectIdentifier.</param>
        /// <param name="clientId">Azure Active Directory ApplicationId.</param>
        /// <returns>An instance of <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache"/>.</returns>
        public abstract Task<TokenCache> GetCacheAsync(ClaimsPrincipal principal);

        /// <summary>
        /// Clears the token cache.
        /// </summary>
        /// <param name="userObjectId">Azure Active Directory user's ObjectIdentifier.</param>
        /// <param name="clientId">Azure Active Directory Client Id.</param>
        public virtual async Task ClearCacheAsync(ClaimsPrincipal principal)
        {
            var cache = await GetCacheAsync(principal);
            cache.Clear();
        }
    }
}

