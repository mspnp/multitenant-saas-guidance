// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Abstractions;
using Microsoft.AspNet.Routing;
using Microsoft.Data.Entity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Data.DTOs;
using Tailspin.Surveys.Security;
using Tailspin.Surveys.Web.Controllers;
using Tailspin.Surveys.Web.Models;
using Tailspin.Surveys.Web.Services;

namespace MultiTentantSurveyAppTests
{
    public class SurveyControllerTests
    {
        private Mock<ISurveyService> _surveyService;
        private Mock<ILogger<SurveyController>> _logger;
        private Mock<IAuthorizationService> _authorizationService;
        private SurveyController _target;

        public SurveyControllerTests()
        {
            _surveyService = new Mock<ISurveyService>();
            _logger = new Mock<ILogger<SurveyController>>();
            _authorizationService = new Mock<IAuthorizationService>();

            var services = new ServiceCollection();
            services.AddEntityFramework()
                .AddInMemoryDatabase()
                .AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase());

            _target = new SurveyController(_surveyService.Object, _logger.Object, _authorizationService.Object);
        }

        [Fact]
        public async Task Index_GetsUserSurveys()
        {
            var apiResultUserSurveys = new Mock<ApiResult<UserSurveysDTO>>();
            apiResultUserSurveys.SetupGet(s => s.Succeeded).Returns(true);
            apiResultUserSurveys.SetupGet(s => s.Item).Returns(new UserSurveysDTO());
            _surveyService.Setup(s => s.GetSurveysForUserAsync(54321))
                .ReturnsAsync(apiResultUserSurveys.Object);

            _target.ActionContext = CreateActionContextWithUserPrincipal("54321", "unregistereduser@contoso.com");
            var result = await _target.Index();
            var view = (ViewResult)result;
            Assert.Same(view.ViewData.Model, apiResultUserSurveys.Object.Item);
        }

        [Fact]
        public async Task Contributors_ShowsContributorsForSurvey()
        {
            var contributors = new ContributorsDTO();
            var apiResult = new Mock<ApiResult<ContributorsDTO>>();
            apiResult.SetupGet(s => s.Item).Returns(contributors);
            apiResult.SetupGet(s => s.Succeeded).Returns(true);

            _surveyService.Setup(s => s.GetSurveyContributorsAsync(It.IsAny<int>()))
                .ReturnsAsync(apiResult.Object);

            var result = await _target.Contributors(12345);
            var viewResult = Assert.IsType<ViewResult>(result);

            Assert.Equal(contributors, viewResult.ViewData.Model);
        }

        [Fact]
        public async Task RequestContributor_SavesRequest()
        {
            var contributorRequestViewModel = new SurveyContributorRequestViewModel { SurveyId = 123, EmailAddress = "unregistereduser@contoso.com" };

            var apiResult = new Mock<ApiResult>();
            var invitations = new List<ContributorRequest>();
            _surveyService.Setup(c => c.AddContributorRequestAsync(It.IsAny<ContributorRequest>()))
                .ReturnsAsync(apiResult.Object)
                .Callback<ContributorRequest>(c => invitations.Add(c));

            // RequestContributor looks for existing contributors
            var contributorsDto = new ContributorsDTO
            {
                Contributors = new List<UserDTO>(),
                Requests = new List<ContributorRequest>()
            };

            var apiResult2 = new Mock<ApiResult<ContributorsDTO>>();
            apiResult2.Setup(x => x.Succeeded).Returns(true);
            apiResult2.Setup(x => x.Item).Returns(contributorsDto);

            _surveyService.Setup(c => c.GetSurveyContributorsAsync(It.IsAny<int>()))
                .ReturnsAsync(apiResult2.Object);

            var result = await _target.RequestContributor(contributorRequestViewModel);

            Assert.Equal(123, invitations[0].SurveyId);
            Assert.Equal("unregistereduser@contoso.com", invitations[0].EmailAddress);
        }

        [Fact]
        public async Task Index_CallsProcessPendingContributorRequests()
        {
            bool surveyContributorProcessed = false;

            var apiResultContributors = new Mock<ApiResult<ContributorsDTO>>();
            apiResultContributors.SetupGet(s => s.Succeeded).Returns(true);
            _surveyService.Setup(s => s.ProcessPendingContributorRequestsAsync())
                .Callback(() => surveyContributorProcessed = true);

            _target.ActionContext = CreateActionContextWithUserPrincipal("54321", "unregistereduser@contoso.com");
            var result = await _target.Index();

            Assert.True(surveyContributorProcessed);
        }

        #region Helpers

        private ActionContext CreateActionContextWithUserPrincipal(string userId, string emailAddress)
        {
            var httpContext = new Mock<HttpContext>();
            var routeData = new Mock<RouteData>();
            var actionDescriptor = new Mock<ActionDescriptor>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new Claim(SurveyClaimTypes.SurveyUserIdClaimType, userId),
                new Claim(ClaimTypes.Email, emailAddress),
                new Claim(AzureADClaimTypes.ObjectId, "objectId"),
                new Claim(AzureADClaimTypes.TenantId, "TenantId"),
                new Claim(OpenIdConnectClaimTypes.IssuerValue, "issuer")

            }));
            httpContext.SetupGet(c => c.User).Returns(principal);
            return new ActionContext(httpContext.Object, routeData.Object, actionDescriptor.Object);
        }

        #endregion
    }
}
