using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Tailspin.Surveys.Common;
using System.Security.Claims;

namespace Tailspin.Surveys.TokenStorage
{
    public class SessionTokenCacheService : TokenCacheService
    {
        private IHttpContextAccessor _contextAccessor;

        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.TokenStorage.SessionTokenCacheService"/>
        /// </summary>
        /// <param name="contextAccessor">An instance of <see cref="Microsoft.AspNet.Http.IHttpContextAccessor"/> used to get access to the current HTTP context.</param>
        /// <param name="loggerFactory"><see cref="Microsoft.Extensions.Logging.ILoggerFactory"/> used to create type-specific <see cref="Microsoft.Extensions.Logging.ILogger"/> instances.</param>
        public SessionTokenCacheService(IHttpContextAccessor contextAccessor, ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            Guard.ArgumentNotNull(contextAccessor, nameof(contextAccessor));
            Guard.ArgumentNotNull(loggerFactory, nameof(loggerFactory));
            _contextAccessor = contextAccessor;
        }

        /// <summary>
        /// Returns an instance of <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache"/>.
        /// </summary>
        /// <param name="userObjectId">Azure Active Directory user's ObjectIdentifier.</param>
        /// <param name="clientId">Azure Active Directory ApplicationId.</param>
        /// <returns>An instance of <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache"/>.</returns>
        public override Task<TokenCache> GetCacheAsync(ClaimsPrincipal principal)
        {
            if (!principal.IsSignedInToApplication())
            {
                throw new InvalidOperationException("User needs to be authenticated first");
            }
            if (_cache == null)
            {
                _cache = new SessionTokenCache(_contextAccessor, _loggerFactory);
            }
            return Task.FromResult(_cache);
        }
    }
}
