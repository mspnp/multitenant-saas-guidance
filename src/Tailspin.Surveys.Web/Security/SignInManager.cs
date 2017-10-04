// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tailspin.Surveys.Web.Logging;

namespace Tailspin.Surveys.Web.Security
{
    public class SignInManager
    {
        private readonly HttpContext _httpContext;

        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="Tailspin.Surveys.Web.Security.SignInManager"/>;
        /// </summary>
        /// <param name="contextAccessor">An instance of <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/> used to get access to the current HTTP context.</param>
        /// <param name="surveysTokenService">An instance of <see cref="Tailspin.Surveys.Web.Security.ISurveysTokenService"/></param>
        /// <param name="logger">An <see cref="Microsoft.Extensions.Logging.ILogger"/> implementation used for diagnostic information.</param>
        public SignInManager(IHttpContextAccessor contextAccessor,
            ILogger<SignInManager> logger)
        {
            _httpContext = contextAccessor.HttpContext;
            _logger = logger;
        }

        /// <summary>
        /// Signs the currently signed in principal out of all authentication schemes.
        /// </summary>
        /// <param name="redirectUrl">A Url to which the user should be redirected when sign out of AAD completes.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task{Microsoft.AspNetCore.Mvc.IActionResult}"/> implementation.</returns>
        public async Task<IActionResult> SignOutAsync(string redirectUrl = null)
        {
            var userObjectIdentifier = _httpContext.User.GetObjectIdentifierValue();
            var issuer = _httpContext.User.GetTenantIdValue();

            try
            {
                _logger.SignoutStarted(userObjectIdentifier, issuer);

                await _httpContext.Authentication.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                await _httpContext.Authentication.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = redirectUrl });

                _logger.SignoutCompleted(userObjectIdentifier, issuer);
            }
            catch (Exception exp)
            {
                _logger.SignoutFailed(userObjectIdentifier, issuer, exp);
                return new StatusCodeResult(500);
            }

            return new EmptyResult();
        }
    }
}
