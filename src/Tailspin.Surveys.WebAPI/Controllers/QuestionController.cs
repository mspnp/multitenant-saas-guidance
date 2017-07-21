// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
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
    /// This class provides a REST based API for the management of questions.
    /// This class uses Bearer token authentication and authorization.
    /// </summary>
    [Authorize(ActiveAuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class QuestionController : Controller
    {
        private readonly IQuestionStore _questionStore;
        private readonly ISurveyStore _surveyStore;
        private readonly IAuthorizationService _authorizationService;

        public QuestionController(IQuestionStore questionStore, ISurveyStore surveyStore, IAuthorizationService authorizationService)
        {
            _surveyStore = surveyStore;
            _authorizationService = authorizationService;
            _questionStore = questionStore;
        }

        /// <summary>
        /// This method returns the Question with a matching id property.
        /// </summary>
        /// <param name="id">The id of the Question</param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="QuestionDTO"/> if found, otherwise a <see cref="NotFoundResult"/></returns>
        [HttpGet("questions/{id:int}", Name = "GetQuestion")]
        public async Task<IActionResult> Get(int id)
        {
            var question = await _questionStore.GetQuestionAsync(id);
            if (question == null)
            {
                return NotFound();
            }

            if (!await _authorizationService.AuthorizeAsync(User, question.Survey, Operations.Update))
            {
                return StatusCode(403);
            }

            return Ok(DataMapping._questionToDto(question));
        }

        /// <summary>
        /// This method persists a new <see cref="Question"/> with the specified id value.
        /// </summary>
        /// <param name="id">The id of the <see cref="Question"/></param>
        /// <param name="questionDto">A <see cref="QuestionDTO"/> containing property values of the <see cref="Question"/></param>
        /// <returns>A <see cref="CreatedAtRouteResult"/> of the newly created <see cref="Question"/> if successfully persisted</returns>
        [HttpPost("surveys/{id:int}/questions")]
        public async Task<IActionResult> Create(int id, [FromBody] QuestionDTO questionDto)
        {
            if (questionDto == null)
            {
                return BadRequest();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var question = new Question
            {
                SurveyId = id,
                Text = questionDto.Text,
                Type = questionDto.Type,
                PossibleAnswers = questionDto.PossibleAnswers
            };

            var survey = await _surveyStore.GetSurveyAsync(question.SurveyId);
            if (survey == null)
            {
                return NotFound();
            }

            // The AuthorizationService uses the policies in the Tailspin.Surveys.Security project
            if (!await _authorizationService.AuthorizeAsync(User, survey, Operations.Update))
            {
                return StatusCode(403);
            }


            await _questionStore.AddQuestionAsync(question);
            return CreatedAtRoute("GetQuestion", new { controller = "Question", id = question.Id }, questionDto);
        }

        /// <summary>
        /// This method updates the <see cref="Question"/> with the specified id value.
        /// </summary>
        /// <param name="id">The id of the <see cref="Question"/></param>
        /// <param name="questionDto">A <see cref="QuestionDTO"/> containing property values of the <see cref="Question"/></param>
        /// <returns>An <see cref="ObjectResult"/> that contains a <see cref="QuestionDTO"/> containing property values of the updated <see cref="Question"/></returns>
        [HttpPut("questions/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] QuestionDTO questionDto)
        {
            if (questionDto == null || questionDto.Id != id)
            {
                return BadRequest();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var question = await _questionStore.GetQuestionAsync(id);
            if (question == null)
            {
                return NotFound();
            }

            if (!await _authorizationService.AuthorizeAsync(User, question.Survey, Operations.Update))
            {
                return StatusCode(403);
            }


            // Apply update
            question.Text = questionDto.Text;
            question.PossibleAnswers = questionDto.PossibleAnswers;
            question.Type = questionDto.Type;
            question.PossibleAnswers = questionDto.PossibleAnswers;
            question.SurveyId = questionDto.SurveyId;

            var result = await _questionStore.UpdateQuestionAsync(question);

            return Ok(DataMapping._questionToDto(result));
        }

        /// <summary>
        /// This method deletes the <see cref="Question"/> with the specified id value.
        /// </summary>
        /// <param name="id">The id of the <see cref="Question"/></param>
        /// <returns>A <see cref="NoContentResult"/> if deletion is successful or a <see cref="HttpNotFoundResult"/> if the <see cref="Question"/> is not found</returns>
        [HttpDelete("questions/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var question = await _questionStore.GetQuestionAsync(id);
            if (question == null)
            {
                return NotFound();
            }

            if (!await _authorizationService.AuthorizeAsync(User, question.Survey, Operations.Update))
            {
                return StatusCode(403);
            }

            await _questionStore.DeleteQuestionAsync(question);
            return NoContent();
        }
    }
}
