using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Tailspin.Surveys.Common;

namespace Tailspin.Surveys.Web.Security
{
    public static class AuthenticationContextExtensions
    {
        /// <summary>
        /// Acquires security token from the authority using an authorization code previously received.
        /// This method does not lookup token cache, but stores the result in it, so it can be looked up using other methods such as <see cref="M:Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext.AcquireTokenSilentAsync(System.String,System.String,Microsoft.IdentityModel.Clients.ActiveDirectory.UserIdentifier)" />.
        /// </summary>
        /// <param name="authenticationContext">The <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext"/> instance to use for token acquisition.</param>
        /// <param name="authorizationCode">The authorization code received from service authorization endpoint.</param>
        /// <param name="redirectUri">The redirect address used for obtaining authorization code.</param>
        /// <param name="credentials">A <see cref="Tailspin.Surveys.Web.Security.AdalCredential"/> instance containing the credentials to use for token acquisition.</param>
        /// <param name="resource">Identifier of the target resource that is the recipient of the requested token. It can be null if provided earlier to acquire authorizationCode.</param>
        /// <returns>It contains Access Token, Refresh Token and the Access Token's expiration time.</returns>
        public static Task<AuthenticationResult> AcquireTokenByAuthorizationCodeAsync(
            this AuthenticationContext authenticationContext,
            string authorizationCode,
            Uri redirectUri,
            AdalCredential credentials,
            string resource)
        {
            Guard.ArgumentNotNull(authenticationContext, nameof(authenticationContext));
            Guard.ArgumentNotNull(credentials, nameof(credentials));

            switch (credentials.CredentialType)
            {
                case AdalCredentialType.ClientAssertionCertificate:
                    return authenticationContext.AcquireTokenByAuthorizationCodeAsync(authorizationCode, redirectUri,
                        credentials.ClientAssertionCertificate, resource);

                case AdalCredentialType.ClientCredential:
                    return authenticationContext.AcquireTokenByAuthorizationCodeAsync(authorizationCode, redirectUri,
                        credentials.ClientCredential, resource);
                default:
                    // This is not surfaced well from ADAL, but this works in the version referenced in this application.
                    throw new AdalException("invalid_credential_type");
            }
        }

        /// <summary>
        /// Acquires security token without asking for user credential.
        /// </summary>
        /// <param name="authenticationContext">The <see cref="Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext"/> instance to use for token acquisition.</param>
        /// <param name="resource">Identifier of the target resource that is the recipient of the requested token.</param>
        /// <param name="credentials">A <see cref="Tailspin.Surveys.Security.AdalCredential"/> instance containing the credentials to use for token acquisition.</param>
        /// <param name="userId">Identifier of the user token is requested for. This parameter can be <see cref="T:Microsoft.IdentityModel.Clients.ActiveDirectory.UserIdentifier" />.Any.</param>
        /// <returns>It contains Access Token, Refresh Token and the Access Token's expiration time. If acquiring token without user credential is not possible, the method throws AdalException.</returns>
        public static Task<AuthenticationResult> AcquireTokenSilentAsync(
            this AuthenticationContext authenticationContext,
            string resource,
            AdalCredential credentials,
            UserIdentifier userId)
        {
            Guard.ArgumentNotNull(authenticationContext, nameof(authenticationContext));
            Guard.ArgumentNotNull(credentials, nameof(credentials));

            switch (credentials.CredentialType)
            {
                case AdalCredentialType.ClientAssertionCertificate:
                    return authenticationContext.AcquireTokenSilentAsync(resource, credentials.ClientAssertionCertificate, userId);

                case AdalCredentialType.ClientCredential:
                    return authenticationContext.AcquireTokenSilentAsync(resource, credentials.ClientCredential, userId);
                default:
                    // This is not surfaced well from ADAL, but I know this works in the current version
                    throw new AdalException("invalid_credential_type");
            }
        }
    }
}
