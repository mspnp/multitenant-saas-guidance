// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Tailspin.Surveys.TokenStorage.Redis
{
    public interface ITokenCacheService
    {
        /// <summary>
        /// Returns an instance of <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache"/>.
        /// </summary>
        /// <param name="userObjectId">Azure Active Directory user's ObjectIdentifier.</param>
        /// <param name="clientId">Azure Active Directory ApplicationId.</param>
        /// <returns>An instance of <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.TokenCache"/>.</returns>
        Task<TokenCache> GetCacheAsync(string userObjectId, string clientId);

        /// <summary>
        /// Clears the token cache.
        /// </summary>
        /// <param name="userObjectId">Azure Active Directory user's ObjectIdentifier.</param>
        /// <param name="clientId">Azure Active Directory ApplicationId.</param>
        Task ClearCacheAsync(string userObjectId, string clientId);
    }
}
