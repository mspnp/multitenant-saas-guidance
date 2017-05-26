// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Data.DataStore;
using Tailspin.Surveys.Data.DTOs;
using Tailspin.Surveys.Security;
using Tailspin.Surveys.WebAPI.Controllers;
using Xunit;
using Microsoft.AspNetCore.Mvc.Controllers;
using FakeItEasy;

namespace MultiTentantSurveyAppTests
{
    public class WebAPISurveyControllerTests
    {
        private ISurveyStore _surveyStore;
        private IContributorRequestStore _contributorRequestStore;
        private IAuthorizationService _authorizationService;
        private SurveyController _target;

        public WebAPISurveyControllerTests()
        {
            _surveyStore = A.Fake<ISurveyStore>();
            _contributorRequestStore = A.Fake<IContributorRequestStore>();
            _authorizationService = A.Fake<IAuthorizationService>();
            _target = new SurveyController(_surveyStore, _contributorRequestStore, _authorizationService);
        }

        [Fact]
        public async Task GetSurveysForUser_ReturnsSurveys()
        {
            ICollection<Survey> surveys = new List<Survey>();
            A.CallTo(() => _surveyStore.GetPublishedSurveysByOwnerAsync(A<int>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(surveys));
            A.CallTo(() => _surveyStore.GetSurveysByOwnerAsync(A<int>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(surveys));
            A.CallTo(() => _surveyStore.GetSurveysByContributorAsync(A<int>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(surveys));

            _target.ControllerContext = CreateActionContextWithUserPrincipal("12345", "testTenantId");
            var result = await _target.GetSurveysForUser(12345);

            var objectResult = (ObjectResult) result;
            Assert.IsType<UserSurveysDTO>(objectResult.Value);
        }

        [Fact]
        public async Task GetSurveysForUser_FailsIfNotUser()
        {
            _target.ControllerContext = CreateActionContextWithUserPrincipal("00000", "testTenantId");
            var result = await _target.GetSurveysForUser(12345);

            var statusCodeResult = (StatusCodeResult)result;
            Assert.Equal(403, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task GetSurveysForTenant_ReturnsSurveys()
        {
            ICollection<Survey> surveys = new List<Survey>();
            A.CallTo(() => _surveyStore.GetPublishedSurveysByTenantAsync(A<int>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(surveys));
            A.CallTo(() => _surveyStore.GetUnPublishedSurveysByTenantAsync(A<int>.Ignored, A<int>.Ignored, A<int>.Ignored))
                .Returns(Task.FromResult(surveys));
            _target.ControllerContext = CreateActionContextWithUserPrincipal("12345", "12345");
            var result = await _target.GetSurveysForTenant(12345);

            var objectResult = (ObjectResult)result;
            Assert.IsType<TenantSurveysDTO>(objectResult.Value);
        }

        [Fact]
        public async Task GetSurveysForTenant_FailsIfNotInSameTenant()
        {
            _target.ControllerContext = CreateActionContextWithUserPrincipal("12345", "54321");
            var result = await _target.GetSurveysForTenant(12345);

            var statusCodeResult = (StatusCodeResult)result;
            Assert.Equal(403, statusCodeResult.StatusCode);
        }

        private ControllerContext CreateActionContextWithUserPrincipal(string userId, string tenantId)
        {
            var httpContext = A.Fake<HttpContext>();
            var routeData = A.Fake<RouteData>();
            var controllerActionDescriptor = A.Fake<ControllerActionDescriptor>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new Claim(SurveyClaimTypes.SurveyUserIdClaimType, userId),
                new Claim(SurveyClaimTypes.SurveyTenantIdClaimType, tenantId)

            }));
            A.CallTo(() => httpContext.User).Returns(principal);

            return new ControllerContext(
                new ActionContext(
                    httpContext,
                    routeData,
                    controllerActionDescriptor
                    ));
        }
    }
}
