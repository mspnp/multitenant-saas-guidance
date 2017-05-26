// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Data.DTOs;
using Tailspin.Surveys.Security.Policy;
using Tailspin.Surveys.Web.Logging;
using Tailspin.Surveys.Web.Models;
using Tailspin.Surveys.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Linq;
using Tailspin.Surveys.Web.Security;

namespace Tailspin.Surveys.Web.Controllers
{
    /// <summary>
    /// This MVC controller provides actions for the management of <see cref="Survey"/>s.
    /// Most of the actions in this controller class require the user to be signed in.
    /// </summary>
    [Authorize]
    public class SurveyController : Controller
    {
        private readonly ISurveyService _surveyService;
        private readonly ILogger _logger;
        private readonly IAuthorizationService _authorizationService;

        public SurveyController(ISurveyService surveyService,
                                ILogger<SurveyController> logger,
                                IAuthorizationService authorizationService)
        {
            _surveyService = surveyService;
            _logger = logger;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// This action shows a list of <see cref="Survey"/>s related to the user. This includes <see cref="Survey"/>s that the user owns, 
        /// <see cref="Survey"/>s that the user contributes to, and <see cref="Survey"/>s the user has published.
        /// 
        /// This action also calls the <see cref="SurveyService"/> to process pending contributor requests.
        /// </summary>
        /// <returns>A view that shows the user's <see cref="Survey"/>s</returns>
        public async Task<IActionResult> Index()
        {
            try
            {
                // If there are any pending contributor requests that 
                await _surveyService.ProcessPendingContributorRequestsAsync();

                var userId = User.GetSurveyUserIdValue();
                var user = User.GetObjectIdentifierValue();
                var issuerValue = User.GetIssuerValue();
                var actionName = ControllerContext.ActionDescriptor.DisplayName;
                _logger.GetSurveysForUserOperationStarted(actionName, user, issuerValue);

                // The SurveyService.GetSurveysForUserAsync returns a UserSurveysDTO that has properties for Published, Own, and Contribute
                var result = await _surveyService.GetSurveysForUserAsync(userId);
                if (result.Succeeded)
                {
                    // If the user is in the creator role, the view shows a "Create Survey" button.
                    ViewBag.IsUserCreator =
                        await _authorizationService.AuthorizeAsync(User, PolicyNames.RequireSurveyCreator);
                    _logger.GetSurveysForUserOperationSucceeded(actionName, user, issuerValue);
                    return View(result.Item);
                }

                _logger.GetSurveysForUserOperationFailed(actionName, user, issuerValue, result.StatusCode);

                if (result.StatusCode == (int) HttpStatusCode.Unauthorized)
                {
                    //this should happen if the bearer token validation fails. We wont sign the user out for 403 - since user may have access to some resources and not others
                    return ReAuthenticateUser();
                }

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }

            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action shows a list of <see cref="Survey"/>s owned by users in the same <see cref="Tenant"/> as the current user.
        /// </summary>
        /// <returns>A view that shows <see cref="Survey"/>s in the same <see cref="Tenant"/> as the current user</returns>
        public async Task<IActionResult> ListPerTenant()
        {
            try
            {
                var tenantId = User.GetSurveyTenantIdValue();
                var result = await _surveyService.GetSurveysForTenantAsync(tenantId);

                if (result.Succeeded)
                {
                    // If the user is an administrator, additional functionality is exposed. 
                    ViewBag.IsUserAdmin = await _authorizationService.AuthorizeAsync(User, PolicyNames.RequireSurveyAdmin);
                    return View(result.Item);
                }

                if (result.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    return ReAuthenticateUser();
                }

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Get experience for creating a <see cref="Survey"/>.
        /// This action is restricted to users in the survey creator role. 
        /// Creator role inclusion is implemented using the RequireSurveyCreator policy
        /// which is defined in <see cref="SurveyCreatorRequirement"/>.
        /// </summary>
        /// <returns>A view with form fields ued to create a <see cref="Survey"/></returns>
        [Authorize(Policy = PolicyNames.RequireSurveyCreator)]
        public IActionResult Create()
        {
            var survey = new SurveyDTO();
            return View(survey);
        }

        /// <summary>
        /// This action provides the Http Post experience for creating a <see cref="Survey"/>.
        /// This action is restricted to users in the survey creator role.
        /// </summary>
        /// <param name="survey">The <see cref="SurveyDTO"/> instance that contains the fields necessary to create a <see cref="Survey"/></param>
        /// <returns>A view that either shows validation errors or a redirection to the Survey Edit experience</returns>
        [HttpPost]
        [Authorize(Policy = PolicyNames.RequireSurveyCreator)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SurveyDTO survey)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var result = await _surveyService.CreateSurveyAsync(survey);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Edit", new { id = result.Item.Id });
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, $"Unable to create survey. (HTTP {result.StatusCode})");
                        switch (result.StatusCode)
                        {
                            case (int)HttpStatusCode.Unauthorized:
                                return ReAuthenticateUser();
                            case (int)HttpStatusCode.Forbidden:
                                ViewBag.Forbidden = true;
                                return View(survey);
                            default:
                                return View(survey);
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Bad Request");
                    return View(survey);
                }
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Unable to create survey.");
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action shows the details of a specific <see cref="Survey"/>.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>A view showing the contents of a <see cref="Survey"/>, or an error message if the <see cref="Survey"/> is not found</returns>
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyAsync(id);
                if (result.Succeeded)
                {
                    return View(result.Item);
                }

                var content = result.Response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Get experience for editing a <see cref="Survey"/>.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>A view showing the <see cref="Survey"/>'s title and questions</returns>
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyAsync(id);
                if (result.Succeeded)
                {
                    if (result.Item.Published)
                    {
                        ViewBag.Message = "The survey is already published! You need to unpublish it in order to edit.";
                        return View("~/Views/Shared/Error.cshtml");
                    }

                    return View(result.Item);
                }

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Get experience for editing the title of a <see cref="Survey"/>
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>A view with form fields for the <see cref="Survey"/> being edited</returns>
        public async Task<IActionResult> EditTitle(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyAsync(id);
                if (result.Succeeded)
                {
                    var model = result.Item;
                    model.ExistingTitle = model.Title;
                    return View(model);
                }

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Post experience for editing a <see cref="Survey"/>.
        /// </summary>
        /// <param name="model">The <see cref="SurveyDTO"/> instance that contains the <see cref="Survey"/>'s updated fields</param>
        /// <returns>A view that either shows validation errors or a redirection to the Survey Edit experience</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        // We want to bind Id and Title and exclude Published, we don't want to publish when editing
        public async Task<IActionResult> EditTitle([Bind("Id", "Title", "ExistingTitle")] SurveyDTO model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var result = await _surveyService.UpdateSurveyAsync(model);
                    if (result.Succeeded)
                    {
                        return RedirectToAction("Edit", new { id = model.Id });
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, $"Unable to save changes. (HTTP {result.StatusCode})");
                        switch (result.StatusCode)
                        {
                            case (int)HttpStatusCode.Unauthorized:
                                return ReAuthenticateUser();
                            case (int)HttpStatusCode.Forbidden:
                                ViewBag.Forbidden = true;
                                return View(model);
                            default:
                                return View(model);
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Bad Request");
                    return View("EditTitle", model);
                }
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Unable to save changes.");
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Get experience for deleting a <see cref="Survey"/>.
        /// </summary>
        /// <param name="id">The id for the <see cref="Survey"/></param>
        /// <returns>A view that shows a delete confirmation prompt</returns>
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyAsync(id);
                if (result.Succeeded)
                {
                    return View(result.Item);
                }

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Post experience for deleting a <see cref="Survey"/>.
        /// </summary>
        /// <param name="model">The <see cref="SurveyDTO"/> instance that contains the id of the <see cref="Survey"/> to be deleted</param>
        /// <returns>A view that either shows validation errors or a redirection to the Survey Edit experience</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([Bind("Id")]SurveyDTO model)
        {
            try
            {
                var surveyResult = await _surveyService.GetSurveyAsync(model.Id);
                if (surveyResult.Succeeded)
                {
                    var result = await _surveyService.DeleteSurveyAsync(model.Id);
                    if (result.Succeeded)
                    {
                        ViewBag.Message = "The following survey has been deleted.";
                        return View("DeleteResult", result.Item);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, $"Unable to delete survey. (HTTP {result.StatusCode})");
                        switch (result.StatusCode)
                        {
                            case (int)HttpStatusCode.Unauthorized:
                                return ReAuthenticateUser();
                            case (int)HttpStatusCode.Forbidden:
                                ViewBag.Forbidden = true;
                                break;
                        }
                        return View(surveyResult.Item);
                    }
                }
                if (surveyResult.StatusCode == (int)HttpStatusCode.NotFound)
                {
                    ModelState.AddModelError(string.Empty, $"The survey can not be found, It may have already been deleted");
                    ViewBag.Message = $"The survey can not be found, It may have already been deleted";
                    return View("~/Views/Shared/Error.cshtml");
                }
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action shows a list of contributors associated with a specific <see cref="Survey"/>.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>A view showing contributors associated with a <see cref="Survey"/></returns>
        public async Task<IActionResult> Contributors(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyContributorsAsync(id);
                if (result.Succeeded)
                {
                    return View(result.Item);
                }

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Get experience for creating a <see cref="ContributorRequest"/>.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>A view with form fields used to create the <see cref="ContributorRequest"/></returns>
        public async Task<IActionResult> RequestContributor(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyAsync(id);
                if (result.Succeeded)
                {
                    ViewBag.SurveyId = id;
                    return View();
                }

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Post experience for creating a <see cref="ContributorRequest"/>.
        /// </summary>
        /// <param name="contributorRequestViewModel">The <see cref="SurveyContributorRequestViewModel"/> instance with fields used to create a new <see cref="ContributorRequest"/></param>
        /// <returns>A redirection to the Show Contributors experience if persistance succeeds, or a view showing validation errors if not.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestContributor(SurveyContributorRequestViewModel contributorRequestViewModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return RedirectToAction(nameof(Contributors), new { id = contributorRequestViewModel.SurveyId });
                }

                var existingContributors = await _surveyService.GetSurveyContributorsAsync(contributorRequestViewModel.SurveyId);
                if (existingContributors.Succeeded)
                {
                    if (existingContributors.Item.Contributors.Any(item =>
                        String.Equals(item.Email, contributorRequestViewModel.EmailAddress, StringComparison.OrdinalIgnoreCase)))
                    {
                        ViewBag.SurveyId = contributorRequestViewModel.SurveyId;
                        ViewBag.Message = contributorRequestViewModel.EmailAddress + " is already a contributor";
                        return View();
                    }

                    if (existingContributors.Item.Requests.Any(item =>
                        String.Equals(item.EmailAddress, contributorRequestViewModel.EmailAddress, StringComparison.OrdinalIgnoreCase)))
                    {
                        ViewBag.SurveyId = contributorRequestViewModel.SurveyId;
                        ViewBag.Message = contributorRequestViewModel.EmailAddress + " has already been requested before";
                        return View();
                    }
                }

                await _surveyService.AddContributorRequestAsync(new ContributorRequest
                {
                    SurveyId = contributorRequestViewModel.SurveyId,
                    EmailAddress = contributorRequestViewModel.EmailAddress
                });

                ViewBag.Message = $"Contribution Requested for {contributorRequestViewModel.EmailAddress}";
                ViewBag.SurveyId = contributorRequestViewModel.SurveyId;
                var result = await _surveyService.GetSurveyContributorsAsync(contributorRequestViewModel.SurveyId);
                if (result.Succeeded)
                {
                    return View("Contributors", result.Item);
                }
                else
                {
                    ViewBag.Message = "Unexpected Error";
                    return View("~/Views/Shared/Error.cshtml");
                }
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Get experience for publishing a <see cref="Survey"/>.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>A page that asks the user to confirm that he/she wants to publish this <see cref="Survey"/></returns>
        public async Task<IActionResult> Publish(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyAsync(id);
                if (result.Succeeded)
                {
                    if (result.Item.Published)
                    {
                        ModelState.AddModelError(string.Empty, $"The survey is already published");
                        return View("PublishResult", result.Item);
                    }
                    else
                    {
                        return View(result.Item);
                    }
                }

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Post experience for publishing a <see cref="Survey"/>.
        /// </summary>
        /// <param name="model">The <see cref="SurveyDTO"/> instance that has the id field used to publish a <see cref="Survey"/></param>
        /// <returns>A confirmation page showing that the <see cref="Survey"/> was published, or errors</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish([Bind("Id")]SurveyDTO model)
        {
            try
            {
                var surveyResult = await _surveyService.GetSurveyAsync(model.Id);
                if (surveyResult.Succeeded)
                {
                    if (surveyResult.Item.Published)
                    {
                        ModelState.AddModelError(string.Empty, $"The survey is already published");
                        return View("PublishResult", surveyResult.Item);
                    }
                    else
                    {
                        var publishResult = await _surveyService.PublishSurveyAsync(model.Id);
                        if (publishResult.Succeeded)
                        {
                            ViewBag.Message = "The following survey has been published.";
                            return View("PublishResult", publishResult.Item);
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, $"Unable to publish survey. (HTTP {publishResult.StatusCode})");
                            switch (publishResult.StatusCode)
                            {
                                case (int)HttpStatusCode.Unauthorized:
                                    return ReAuthenticateUser();
                                case (int)HttpStatusCode.Forbidden:
                                    ViewBag.Forbidden = true;
                                    break;
                            }
                            return View(surveyResult.Item);
                        }
                    }
                }
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// This action provides the Http Get experience for unpublishing a <see cref="Survey"/>.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>A page that asks the user to confirm that he/she wants to publish this <see cref="Survey"/></returns>
        public async Task<IActionResult> UnPublish(int id)
        {
            try
            {
                var result = await _surveyService.GetSurveyAsync(id);
                if (result.Succeeded)
                {
                    if (!result.Item.Published)
                    {
                        ModelState.AddModelError(string.Empty, $"The survey is already unpublished");
                        return View("UnPublishResult", result.Item);
                    }
                    else
                    {
                        return View(result.Item);
                    }
                }

                var errorResult = CheckStatusCode(result);
                if (errorResult != null) return errorResult;

                ViewBag.Message = "Unexpected Error";
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        private IActionResult CheckStatusCode(ApiResult result)
        {
            if (result.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                return ReAuthenticateUser();
            }

            if (result.StatusCode == (int)HttpStatusCode.Forbidden)
            {
                // Redirects user to Forbidden page
                return new ChallengeResult(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            if (result.StatusCode == (int)HttpStatusCode.NotFound)
            {
                ModelState.AddModelError(string.Empty, "The survey can not be found");
                ViewBag.Message = "The survey can not be found";
                return View("~/Views/Shared/Error.cshtml");
            }

            return null;
        }

        /// <summary>
        /// This action provides the Http Post experience for unpublishing a <see cref="Survey"/>.
        /// </summary>
        /// <param name="model">The <see cref="SurveyDTO"/> instance that has the id field used to unpublish a <see cref="Survey"/></param>
        /// <returns>A confirmation page showing that the <see cref="Survey"/> was unpublished, or errors</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnPublish([Bind("Id")]SurveyDTO model)
        {
            try
            {
                var surveyResult = await _surveyService.GetSurveyAsync(model.Id);
                if (surveyResult.Succeeded)
                {
                    if (!surveyResult.Item.Published)
                    {
                        ModelState.AddModelError(string.Empty, $"The survey is already unpublished");
                        return View("UnPublishResult", surveyResult.Item);
                    }
                    else
                    {
                        var unpublishResult = await _surveyService.UnPublishSurveyAsync(model.Id);
                        if (unpublishResult.Succeeded)
                        {
                            ViewBag.Message = "The following survey has been unpublished.";
                            return View("UnPublishResult", unpublishResult.Item);
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, $"Cannot unpublish survey. (HTTP {unpublishResult.StatusCode})");
                            switch (unpublishResult.StatusCode)
                            {
                                case (int)HttpStatusCode.Unauthorized:
                                    return ReAuthenticateUser();
                                case (int)HttpStatusCode.Forbidden:
                                    ViewBag.Forbidden = true;
                                    break;
                            }
                            return View(surveyResult.Item);
                        }
                    }
                }
            }
            catch (AuthenticationException)
            {
                return ReAuthenticateUser();
            }
            catch
            {
                ViewBag.Message = "Unexpected Error";
            }
            return View("~/Views/Shared/Error.cshtml");
        }

        private IActionResult ReAuthenticateUser()
        {
            return new ChallengeResult(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    RedirectUri = Url.Action("SignInCallback", "Account")
                });
        }
    }
}
