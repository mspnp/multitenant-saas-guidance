// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tailspin.Surveys.Common;
using Tailspin.Surveys.Common.Configuration;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Security;
using Tailspin.Surveys.Web.Logging;
using System.Security;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Globalization;
using Microsoft.IdentityModel.Tokens;

namespace Tailspin.Surveys.Web.Security
{
    /// <summary>
    /// 
    /// </summary>
    public class SurveyAuthenticationEvents : OpenIdConnectEvents
    {
        private readonly AzureAdOptions _adOptions;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.Web.Security.SurveyAuthenticationEvents"/>.
        /// </summary>
        /// <param name="adOptions">Application settings related to Azure Active Directory.</param>
        /// <param name="loggerFactory"><see cref="Microsoft.Extensions.Logging.ILoggerFactory"/> used to create type-specific <see cref="Microsoft.Extensions.Logging.ILogger"/> instances.</param>
        public SurveyAuthenticationEvents(AzureAdOptions adOptions, ILoggerFactory loggerFactory)
        {
            _adOptions = adOptions;
            _logger = loggerFactory.CreateLogger<SurveyAuthenticationEvents>();
        }

        /// <summary>
        /// Called prior to the OIDC middleware redirecting to the authentication endpoint.  In the event we are signing up a tenant, we need to
        /// put the "admin_consent" value for the prompt query string parameter.  AAD uses this to show the admin consent flow.
        /// </summary>
        /// <param name="context">The <see cref="Microsoft.AspNetCore.Authentication.OpenIdConnect.RedirectContext"/> for this event.</param>
        /// <returns>A completed <see cref="System.Threading.Tasks.Task"/></returns>
        public override Task RedirectToIdentityProvider(RedirectContext context)
        {
            if (context.IsSigningUp())
            {
                context.ProtocolMessage.Prompt = "admin_consent";
            }

            _logger.RedirectToIdentityProvider();
            return Task.FromResult(0);
        }

        /// <summary>
        /// Transforms the claims from AAD to well-known claims.
        /// </summary>
        /// <param name="principal">The current <see cref="System.Security.Claims.ClaimsPrincipal"/></param>
        private static void NormalizeClaims(ClaimsPrincipal principal)
        {
            Guard.ArgumentNotNull(principal, nameof(principal));

            var identity = principal.Identities.First();
            if (!identity.IsAuthenticated)
            {
                throw new InvalidOperationException("The supplied principal is not authenticated.");
            }

            var email = principal.FindFirst(ClaimTypes.Upn)?.Value;
            if (!string.IsNullOrWhiteSpace(email))
            {
                identity.AddClaim(new Claim(ClaimTypes.Email, email));
            }

            var name = principal.GetDisplayNameValue();
            if (!string.IsNullOrWhiteSpace(name))
            {
                // It looks like AAD does something strange here, but it's actually the JwtSecurityTokenHandler making assumptions
                // about the claims from AAD.  It takes the unique_name claim from AAD and maps it to a ClaimTypes.Name claim, which
                // is the default type for a name claim for our identity.  If we don't remove the old one, there will be two name claims,
                // so let's get rid of the first one.
                var previousNameClaim = principal.FindFirst(ClaimTypes.Name);
                if (previousNameClaim != null)
                {
                    identity.RemoveClaim(previousNameClaim);
                }
                identity.AddClaim(new Claim(identity.NameClaimType, name));
            }
        }

        private async Task<Tenant> SignUpTenantAsync(BaseControlContext context, TenantManager tenantManager)
        {
            Guard.ArgumentNotNull(context, nameof(context));
            Guard.ArgumentNotNull(tenantManager, nameof(tenantManager));

            var principal = context.Ticket.Principal;
            var issuerValue = principal.GetIssuerValue();
            var tenant = new Tenant
            {
                IssuerValue = issuerValue,
                Created = DateTimeOffset.UtcNow
            };

            try
            {
                await tenantManager.CreateAsync(tenant)
                    .ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                _logger.SignUpTenantFailed(principal.GetObjectIdentifierValue(), issuerValue, ex);
                throw;
            }

            return tenant;
        }

        private async Task CreateOrUpdateUserAsync(AuthenticationTicket authenticationTicket, UserManager userManager, Tenant tenant)
        {
            Guard.ArgumentNotNull(authenticationTicket, nameof(authenticationTicket));
            Guard.ArgumentNotNull(userManager, nameof(userManager));
            Guard.ArgumentNotNull(tenant, nameof(tenant));

            var principal = authenticationTicket.Principal;
            string objectIdentifier = principal.GetObjectIdentifierValue();
            string displayName = principal.GetDisplayNameValue();
            string email = principal.GetEmailValue();

            var user = await userManager.FindByObjectIdentifier(objectIdentifier)
                .ConfigureAwait(false);
            if (user == null)
            {
                // The user isn't in our database, so add them.
                user = new User
                {
                    Created = DateTimeOffset.UtcNow,
                    ObjectId = objectIdentifier,
                    TenantId = tenant.Id,
                    DisplayName = displayName,
                    Email = email
                };

                await userManager.CreateAsync(user)
                    .ConfigureAwait(false);
            }
            else
            {
                // Since we aren't the system of record, we need to attempt to keep our display values in sync with the user store.
                // We'll do a simple form of it here.
                bool shouldSaveUser = false;
                if (!user.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase))
                {
                    user.DisplayName = displayName;
                    shouldSaveUser = true;
                }

                // Do a case insensitive comparison for email matching
                if (!user.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
                {
                    user.Email = email;
                    shouldSaveUser = true;
                }

                if (shouldSaveUser)
                {
                    await userManager
                        .UpdateAsync(user)
                        .ConfigureAwait(false);
                }
            }

            // Add in the survey user id claim.
            principal.Identities.First().AddClaim(new Claim(SurveyClaimTypes.SurveyUserIdClaimType, user.Id.ToString()));
            // Add in the user's tenant id claim.
            principal.Identities.First().AddClaim(new Claim(SurveyClaimTypes.SurveyTenantIdClaimType, user.TenantId.ToString()));
        }

        /// <summary>
        /// Method that is called by the OIDC middleware after the authentication data has been validated.  This is where most of the sign up
        /// and sign in work is done.
        /// </summary>
        /// <param name="context">An OIDC-supplied <see cref="Microsoft.AspNetCore.Authentication.OpenIdConnect.AuthenticationValidatedContext"/> containing the current authentication information.</param>
        /// <returns>a completed <see cref="System.Threading.Tasks.Task"/></returns>
        public override async Task TokenValidated(TokenValidatedContext context)
        {
            var principal = context.Ticket.Principal;
            var userId = principal.GetObjectIdentifierValue();
            var tenantManager = context.HttpContext.RequestServices.GetService<TenantManager>();
            var userManager = context.HttpContext.RequestServices.GetService<UserManager>();
            var issuerValue = principal.GetIssuerValue();
            _logger.AuthenticationValidated(userId, issuerValue);

            // Normalize the claims first.
            NormalizeClaims(principal);
            var tenant = await tenantManager.FindByIssuerValueAsync(issuerValue)
                .ConfigureAwait(false);

            if (context.IsSigningUp())
            {
                // Originally, we were checking to see if the tenant was non-null, however, this would not allow
                // permission changes to the application in AAD since a re-consent may be required.  Now we just don't
                // try to recreate the tenant.
                if (tenant == null)
                {
                    tenant = await SignUpTenantAsync(context, tenantManager)
                        .ConfigureAwait(false);
                }

                // In this case, we need to go ahead and set up the user signing us up.
                await CreateOrUpdateUserAsync(context.Ticket, userManager, tenant)
                    .ConfigureAwait(false);
            }
            else
            {
                if (tenant == null)
                {
                    _logger.UnregisteredUserSignInAttempted(userId, issuerValue);
                    throw new SecurityTokenValidationException($"Tenant {issuerValue} is not registered");
                }

                await CreateOrUpdateUserAsync(context.Ticket, userManager, tenant)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Called by the OIDC middleware when authentication fails.
        /// </summary>
        /// <param name="context">An OIDC-middleware supplied <see cref="Microsoft.AspNetCore.Authentication.OpenIdConnect.AuthenticationFailedContext"/> containing information about the failed authentication.</param>
        /// <returns>A completed <see cref="System.Threading.Tasks.Task"/></returns>
        public override Task AuthenticationFailed(AuthenticationFailedContext context)
        {
            _logger.AuthenticationFailed(context.Exception);
            return Task.FromResult(0);
        }

        public override async Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
        {
            var principal = context.Ticket.Principal;

            //
            var request = context.HttpContext.Request;
            var currentUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path);
            var properties = context.Properties;

            //

            var surveysTokenService = context.HttpContext.RequestServices.GetService<ISurveysTokenService>();
            try
            {
                await surveysTokenService.RequestTokenAsync(
                    principal,
                    context.ProtocolMessage.Code,
                    currentUri,
                    _adOptions.WebApiResourceId)
                    .ConfigureAwait(false);
            }
            catch
            {
                // If an exception is thrown within this event, the user is never set on the OWIN middleware,
                // so there is no need to sign out.  However, the access token could have been put into the
                // cache so we need to clean it up.
                await surveysTokenService.ClearCacheAsync(principal)
                    .ConfigureAwait(false);
                throw;
            }

        }

        // These method are overridden to make it easier to debug the OIDC auth flow.

        public override Task TicketReceived(TicketReceivedContext context)
        {
            return base.TicketReceived(context);
        }

        public override Task TokenResponseReceived(TokenResponseReceivedContext context)
        {
            return base.TokenResponseReceived(context);
        }

        public override Task UserInformationReceived(UserInformationReceivedContext context)
        {
            return base.UserInformationReceived(context);
        }

        public override Task MessageReceived(MessageReceivedContext context)
        {
            return base.MessageReceived(context);
        }
    }
}
