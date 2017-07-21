// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Data.DataStore;
using Tailspin.Surveys.Data.DTOs;
using Tailspin.Surveys.Security.Policy;

namespace Tailspin.Surveys.WebAPI.Controllers
{
    /// <summary>
    /// This class provides a REST based API for the management of surveys.
    /// This class uses Bearer token authentication and authorization.
    /// </summary>
    [Authorize(ActiveAuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SurveyController : Controller
    {
        private readonly ISurveyStore _surveyStore;
        private readonly IContributorRequestStore _contributorRequestStore;
        private readonly IAuthorizationService _authorizationService;

        public SurveyController(ISurveyStore surveyStore, IContributorRequestStore contributorRequestStore, IAuthorizationService authorizationService)
        {
            _surveyStore = surveyStore;
            _contributorRequestStore = contributorRequestStore;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// This method returns a <see cref="Survey"/> if one is found with a matching id property. 
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="SurveyDTO"/> if found, otherwise a <see cref="HttpNotFoundResult"/></returns>
        [HttpGet("surveys/{id:int}", Name = "GetSurvey")]
        public async Task<IActionResult> Get(int id)
        {
            var survey = await _surveyStore.GetSurveyAsync(id);
            if (survey == null)
            {
                return NotFound();
            }

            // The AuthorizationService uses the policies in the Tailspin.Surveys.Security project
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.Read))
            {
                return StatusCode(403);
            }
            return Ok(DataMapping._surveyToDto(survey));
        }

        /// <summary>
        /// This method returns <see cref="Survey"/>s associated to the user: Published, Owned, or Contributed.
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="UserSurveysDTO"/> populated with <see cref="Survey"/>s that the user owns or contributes to, or published</returns>
        [HttpGet("users/{userId}/surveys")]
        public async Task<IActionResult> GetSurveysForUser(int userId)
        {
            if (User.GetSurveyUserIdValue() != userId)
            {
                return StatusCode(403);
            }

            var surveys = new UserSurveysDTO();
            surveys.Published = (await _surveyStore.GetPublishedSurveysByOwnerAsync(userId)).Select(DataMapping._surveyToSummaryDto).ToArray();
            surveys.Own = (await _surveyStore.GetSurveysByOwnerAsync(userId)).Select(DataMapping._surveyToSummaryDto).ToArray();
            surveys.Contribute = (await _surveyStore.GetSurveysByContributorAsync(userId)).Select(DataMapping._surveyToSummaryDto).ToArray();

            return Ok(surveys);
        }

        /// <summary>
        /// This method returns all <see cref="Survey"/>s owned by users of a specific tenant.
        /// </summary>
        /// <param name="tenantId">The id of the <see cref="Tenant"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="TenantSurveysDTO"/> populated with Published and Unpublished surveys associated with a <see cref="Tenant"/></returns>
        [HttpGet("tenants/{tenantId}/surveys")]
        public async Task<IActionResult> GetSurveysForTenant(int tenantId)
        {
            if (User.GetSurveyTenantIdValue() != tenantId)
            {
                return StatusCode(403);
            }

            var surveys = new TenantSurveysDTO();
            surveys.Published = (await _surveyStore.GetPublishedSurveysByTenantAsync(tenantId)).Select(DataMapping._surveyToSummaryDto).ToArray();
            surveys.UnPublished = (await _surveyStore.GetUnPublishedSurveysByTenantAsync(tenantId)).Select(DataMapping._surveyToSummaryDto).ToArray();
            return Ok(surveys);
        }

        /// <summary>
        /// This method returns all published <see cref="Survey"/>s. This method is anonymously accessible.
        /// </summary>
        /// <returns>An <see cref="ObjectResult"/> that contains an enumerable collection of published <see cref="Survey"/>s</returns>
        [HttpGet("surveys/published")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublishedSurveys()
        {
            var surveys = (await _surveyStore.GetPublishedSurveysAsync()).Select(DataMapping._surveyToDto);
            return Ok(surveys);
        }

        /// <summary>
        /// This method returns the contributors to a <see cref="Survey"/>.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="ContributorsDTO"/> populated with an enumerable collection of users who contribute to the specified <see cref="Survey"/></returns>
        [HttpGet("surveys/{id}/contributors")]
        public async Task<IActionResult> GetSurveyContributors(int id)
        {
            var survey = await _surveyStore.GetSurveyAsync(id);
            if (survey == null)
            {
                return NotFound();
            }

            // Validate that the current user has Read permissions to this survey.
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.Read))
            {
                return StatusCode(403);
            }

            return Ok(new ContributorsDTO()
            {
                SurveyId = id,
                Contributors = survey.Contributors.Select(x => new UserDTO { Email = x.User.Email }).ToArray(),
                Requests = survey.Requests.Where(r => r.SurveyId == id).ToArray()
            });
        }

        /// <summary>
        /// This method attempts to persist a new <see cref="Survey"/> using the values in the <see cref="SurveyDTO"/> instance.
        /// This action is decorated with an authorization policy that requires only users in the Survey Creator role can call it.
        /// </summary>
        /// <param name="item">An <see cref="SurveyDTO"/> that contains the values used to create and persist a new <see cref="Survey"/></param>
        /// <returns>A <see cref="CreatedAtRouteResult"/> of the newly created <see cref="Survey"/> if successfully persisted</returns>
        [HttpPost("surveys")]
        [Authorize(Policy = PolicyNames.RequireSurveyCreator)]
        public async Task<IActionResult> Create([FromBody] SurveyDTO item)
        {
            if (item == null)
            {
                return BadRequest();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var survey = DataMapping._dtoToSurvey(item);
            survey.OwnerId = User.GetSurveyUserIdValue();
            survey.TenantId = User.GetSurveyTenantIdValue();

            await _surveyStore.AddSurveyAsync(survey);

            item.Id = survey.Id;

            return CreatedAtRoute("GetSurvey", new { controller = "Surveys", id = survey.Id }, item);
        }

        /// <summary>
        /// This method attempts to update the <see cref="Survey"/> with the specified id value.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <param name="item">A <see cref="SurveyDTO"/> containing property values of the <see cref="Survey"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="SurveyDTO"/> containing property values of the updated <see cref="Survey"/></returns>
        [HttpPut("surveys/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SurveyDTO item)
        {
            if (item == null || item.Id != id)
            {
                return BadRequest();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var survey = await _surveyStore.GetSurveyAsync(id);
            if (survey == null)
            {
                return NotFound();
            }

            // Validate that the current user has Update permissions to this survey.
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.Update))
            {
                return StatusCode(403);
            }

            // Apply update
            survey.Title = item.Title;
            survey.Published = item.Published;

            await _surveyStore.UpdateSurveyAsync(survey);
            return Ok(DataMapping._surveyToDto(survey));
        }

        /// <summary>
        /// This method deletes the <see cref="Survey"/> with the specified id value.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains the deleted <see cref="Survey"/> if deletion is successful or a <see cref="HttpNotFoundResult"/> if the <see cref="Survey"/> is not found</returns>
        [HttpDelete("surveys/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var survey = await _surveyStore.GetSurveyAsync(id);
            if (survey == null)
            {
                return NotFound();
            }

            // Validate that the current user has Delete permissions to this survey.
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.Delete))
            {
                return StatusCode(403);
            }

            await _surveyStore.DeleteSurveyAsync(survey);
            return Ok(DataMapping._surveyToDto(survey));
        }

        /// <summary>
        /// This method persists a survey contributor request.
        /// </summary>
        /// <param name="item">A <see cref="ContributorRequest"/> instance</param>
        /// <returns>A <see cref="NoContentResult"/> if the <see cref="ContributorRequest"/> is successfully persisted 
        /// or a <see cref="BadRequestResult"/> if the corresponding <see cref="Survey"/> is not found 
        /// or the <see cref="ContributorRequest"/> is not valid</returns>
        [HttpPost("/surveys/{id}/contributorrequests")]
        public async Task<IActionResult> AddContributorRequest([FromBody] ContributorRequest item)
        {
            if (item == null)
            {
                return BadRequest();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var survey = await _surveyStore.GetSurveyAsync(item.SurveyId);
            if (survey == null)
            {
                return BadRequest();
            }

            // Validate that the current user has Update permissions to this survey.
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.Update))
            {
                return StatusCode(403);
            }

            await _contributorRequestStore.AddRequestAsync(item);

            return NoContent();
        }

        /// <summary>
        /// This method processes pending <see cref="ContributorRequest"/>s for the current user.
        /// </summary>
        /// <returns>A <see cref="NoContentResult"/></returns>
        [HttpPost("surveys/processpendingcontributorrequests")]
        public async Task<IActionResult> ProcessPendingContributorRequests()
        {
            var emailAddress = User.GetEmailValue();

            // Get pending ContributorRequests that match user's email address
            var contributorRequests = await _contributorRequestStore.GetRequestsForUserAsync(emailAddress);

            foreach (var contributorRequest in contributorRequests)
            {
                var survey = await _surveyStore.GetSurveyAsync(contributorRequest.SurveyId);

                int contributorId = User.GetSurveyUserIdValue();

                // Check for existing contributor assignment
                if (!survey.Contributors.Any(x => x.UserId == contributorId))
                {
                    survey.Contributors.Add(new SurveyContributor { SurveyId = contributorRequest.SurveyId, UserId = contributorId });
                    await _surveyStore.UpdateSurveyAsync(survey);
                }

                await _contributorRequestStore.RemoveRequestAsync(contributorRequest);
            }

            return NoContent();
        }

        /// <summary>
        /// This method publishes the <see cref="Survey"/> with the matching id.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="SurveyDTO"/> of a published <see cref="Survey"/>, or a <see cref="HttpNotFoundResult"/> if the <see cref="Survey"/> is not found</returns>
        [HttpPut("surveys/{id}/publish")]
        public async Task<IActionResult> Publish(int id)
        {
            var survey = await _surveyStore.GetSurveyAsync(id);
            if (survey == null)
            {
                return NotFound();
            }

            // Validate that the current user has Publish permissions to this survey.
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.Publish))
            {
                return StatusCode(403);
            }

            var published = await _surveyStore.PublishSurveyAsync(id);

            return Ok(DataMapping._surveyToDto(published));
        }

        /// <summary>
        /// This method unpublishes the <see cref="Survey"/> with the matching id.
        /// </summary>
        /// <param name="id">The id of the <see cref="Survey"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="SurveyDTO"/> of an unpublished <see cref="Survey"/>, or a <see cref="HttpNotFoundResult"/> if the <see cref="Survey"/> is not found</returns>
        [HttpPut("surveys/{id}/unpublish")]
        public async Task<IActionResult> UnPublish(int id)
        {
            var survey = await _surveyStore.GetSurveyAsync(id);
            if (survey == null)
            {
                return NotFound();
            }

            // Validate that the current user has UnPublish permissions to this survey.
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.UnPublish))
            {
                return StatusCode(403);
            }

            var unpublished = await _surveyStore.UnPublishSurveyAsync(id);

            return Ok(DataMapping._surveyToDto(unpublished));
        }

    }
}
