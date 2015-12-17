// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.OptionsModel;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Tailspin.Surveys.Common;
using Tailspin.Surveys.Common.Configuration;
using Tailspin.Surveys.TokenStorage;
using Tailspin.Surveys.Web.Configuration;
using Tailspin.Surveys.Web.Logging;

namespace Tailspin.Surveys.Web.Security
{
    /// <summary>
    /// This service helps with the acquisition of access tokens from Azure Active Directory.
    /// </summary>
    public class AzureADTokenService : IAccessTokenService
    {
        private readonly AzureAdOptions _adOptions;
        private readonly ITokenCacheService _tokenCacheService;
        private readonly ILogger _logger;
        private readonly ICredentialService _credentialService;

        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.Web.Security.AzureADTokenService"/>.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="tokenCacheService"></param>
        /// <param name="credentialService"></param>
        /// <param name="logger"></param>
        public AzureADTokenService(
            IOptions<ConfigurationOptions> options,
            ITokenCacheService tokenCacheService,
            ICredentialService credentialService,
            ILogger<AzureADTokenService> logger)
        {
            _adOptions = options?.Value?.AzureAd;
            _tokenCacheService = tokenCacheService;
            _credentialService = credentialService;
            _logger = logger;
        }

        /// <summary>
        /// This method retrieves the access token for the WebAPI resource that has previously
        /// been retrieved and cached. This method will fail if an access token for the WebAPI 
        /// resource has not been retrieved and cached. You can use the RequestAccessTokenAsync
        /// method to retrieve and cache access tokens.
        /// </summary>
        /// <param name="user">The <see cref="ClaimsPrincipal"/> for the user to whom the access token belongs.</param>
        /// <returns>A string access token wrapped in a <see cref="Task"/></returns>
        public async Task<string> GetTokenForWebApiAsync(ClaimsPrincipal user)
        {
            return await GetAccessTokenForResourceAsync(_adOptions.WebApiResourceId, user)
                .ConfigureAwait(false);
        }

        private async Task<string> GetAccessTokenForResourceAsync(string resource, ClaimsPrincipal user)
        {
            var userId = user.GetObjectIdentifierValue();
            var issuerValue = user.GetIssuerValue();
            var userName = user.Identity?.Name;

            try
            {
                _logger.BearerTokenAcquisitionStarted(resource, userName, issuerValue);
                var authContext = await CreateAuthenticationContext(user)
                    .ConfigureAwait(false);
                var result = await authContext.AcquireTokenSilentAsync(
                    resource,
                    await _credentialService.GetCredentialsAsync().ConfigureAwait(false),
                    new UserIdentifier(userId, UserIdentifierType.UniqueId))
                    .ConfigureAwait(false);

                _logger.BearerTokenAcquisitionSucceeded(resource, userName, issuerValue);

                return result.AccessToken;
            }
            catch (AdalException ex)
            {
                _logger.BearerTokenAcquisitionFailed(resource, userName, issuerValue, ex);
                throw new AuthenticationException($"AcquireTokenSilentAsync failed for user: {userId}", ex);
            }
        }

        private async Task<AuthenticationContext> CreateAuthenticationContext(ClaimsPrincipal claimsPrincipal)
        {
            Guard.ArgumentNotNull(claimsPrincipal, nameof(claimsPrincipal));

            return new AuthenticationContext(
               Constants.AuthEndpointPrefix + claimsPrincipal.GetTenantIdValue(),
                await _tokenCacheService.GetCacheAsync(claimsPrincipal)
                .ConfigureAwait(false));
        }

        /// <summary>
        /// This method acquires an access token using an authorization code and ADAL. The access token is then cached
        /// in a <see cref="TokenCache"/> to be used later (by calls to GetTokenForWebApiAsync).
        /// </summary>
        /// <param name="claimsPrincipal">A <see cref="ClaimsPrincipal"/> for the signed in user</param>
        /// <param name="authorizationCode">a string authorization code obtained when the user signed in</param>
        /// <param name="redirectUri">The Uri of the application requesting the access token</param>
        /// <param name="resource">The resouce identifier of the target resource</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task{Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationResult}"/>.</returns>
        public async Task<AuthenticationResult> RequestAccessTokenAsync(
            ClaimsPrincipal claimsPrincipal,
            string authorizationCode,
            string redirectUri,
            string resource)
        {
            Guard.ArgumentNotNull(claimsPrincipal, nameof(claimsPrincipal));
            Guard.ArgumentNotNullOrWhiteSpace(authorizationCode, nameof(authorizationCode));
            Guard.ArgumentNotNullOrWhiteSpace(redirectUri, nameof(redirectUri));
            Guard.ArgumentNotNullOrWhiteSpace(resource, nameof(resource));

            try
            {
                var userId = claimsPrincipal.GetObjectIdentifierValue();
                var issuerValue = claimsPrincipal.GetIssuerValue();
                _logger.AuthenticationCodeRedemptionStarted(userId, issuerValue, resource);
                var authenticationContext = await CreateAuthenticationContext(claimsPrincipal)
                    .ConfigureAwait(false);
                var authenticationResult = await authenticationContext.AcquireTokenByAuthorizationCodeAsync(
                    authorizationCode,
                    new Uri(redirectUri),
                    await _credentialService.GetCredentialsAsync().ConfigureAwait(false),
                    resource)
                    .ConfigureAwait(false);

                _logger.AuthenticationCodeRedemptionCompleted(userId, issuerValue, resource);
                return authenticationResult;
            }
            catch (Exception ex)
            {
                _logger.AuthenticationCodeRedemptionFailed(ex);
                throw;
            }
        }

        /// <summary>
        /// This method clears the user's <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache"/>.
        /// </summary>
        /// <param name="claimsPrincipal">The <see cref="System.Security.Claims.ClaimsPrincipal"/> for the user</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/></returns>
        public async Task ClearCacheAsync(ClaimsPrincipal claimsPrincipal)
        {
            Guard.ArgumentNotNull(claimsPrincipal, nameof(claimsPrincipal));

            await _tokenCacheService.ClearCacheAsync(claimsPrincipal)
                .ConfigureAwait(false);
        }
    }
}
