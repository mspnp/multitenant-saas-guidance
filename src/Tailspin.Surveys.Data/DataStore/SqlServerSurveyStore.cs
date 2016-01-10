// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Entity;
using Tailspin.Surveys.Common;
using Tailspin.Surveys.Data.DataModels;

namespace Tailspin.Surveys.Data.DataStore
{
    public class SqlServerSurveyStore : ISurveyStore
    {
        private ApplicationDbContext _dbContext;

        public SqlServerSurveyStore(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ICollection<Survey>> GetSurveysByOwnerAsync(int userId, int pageIndex = 0, int pageSize = Constants.DefaultPageSize)
        {
            var cappedPageSize = Math.Min(Constants.MaxPageSize, pageSize);
            return await _dbContext.Surveys.Where(s => s.OwnerId == userId && !s.Published)
                                     .OrderBy(s => s.Id)
                                     .Skip(pageIndex * cappedPageSize)
                                     .Take(cappedPageSize)
                                     .ToArrayAsync()
                                     .ConfigureAwait(false);
        }

        public async Task<ICollection<Survey>> GetPublishedSurveysByOwnerAsync(int userId, int pageIndex = 0, int pageSize = Constants.DefaultPageSize)
        {
            var cappedPageSize = Math.Min(Constants.MaxPageSize, pageSize);
            return await _dbContext.Surveys.Where(s => s.OwnerId == userId && s.Published)
                                           .OrderBy(s => s.Id)
                                           .Skip(pageIndex * cappedPageSize)
                                           .Take(cappedPageSize)
                                           .ToArrayAsync()
                                           .ConfigureAwait(false);
        }

        public async Task<ICollection<Survey>> GetSurveysByContributorAsync(int userId, int pageIndex = 0, int pageSize = Constants.DefaultPageSize)
        {
            var cappedPageSize = Math.Min(Constants.MaxPageSize, pageSize);
            return await _dbContext.SurveyContributors.Include(sc => sc.Survey)
                                                      .Where(sc => sc.UserId == userId && !sc.Survey.Published)
                                                      .Select(sc => sc.Survey)
                                                      .OrderBy(s => s.Id)
                                                      .Skip(pageIndex * cappedPageSize)
                                                      .Take(cappedPageSize)
                                                      .ToArrayAsync()
                                                      .ConfigureAwait(false);
        }

        public async Task<ICollection<Survey>> GetPublishedSurveysByTenantAsync(int tenantId, int pageIndex = 0, int pageSize = Constants.DefaultPageSize)
        {
            var cappedPageSize = Math.Min(Constants.MaxPageSize, pageSize);
            return await _dbContext.Surveys.Where(s => s.TenantId == tenantId && s.Published)
                                           .OrderBy(s => s.Id)
                                           .Skip(pageIndex * cappedPageSize)
                                           .Take(cappedPageSize)
                                           .ToArrayAsync()
                                           .ConfigureAwait(false);
        }

        public async Task<ICollection<Survey>> GetUnPublishedSurveysByTenantAsync(int tenantId, int pageIndex = 0, int pageSize = Constants.DefaultPageSize)
        {
            var cappedPageSize = Math.Min(Constants.MaxPageSize, pageSize);
            return await _dbContext.Surveys.Where(s => s.TenantId == tenantId && !s.Published)
                                           .OrderBy(s => s.Id)
                                           .Skip(pageIndex * cappedPageSize)
                                           .Take(cappedPageSize)
                                           .ToArrayAsync()
                                           .ConfigureAwait(false);
        }

        public async Task<ICollection<Survey>> GetPublishedSurveysAsync(int pageIndex = 0, int pageSize = Constants.DefaultPageSize)
        {
            var cappedPageSize = Math.Min(Constants.MaxPageSize, pageSize);
            return await _dbContext.Surveys.Where(s => s.Published)
                                           .OrderBy(s => s.Id)
                                           .Skip(pageIndex * cappedPageSize)
                                           .Take(cappedPageSize)
                                           .ToArrayAsync()
                                           .ConfigureAwait(false);
        }

        public async Task<Survey> GetSurveyAsync(int id)
        {
            return await _dbContext.Surveys
                .Include(survey => survey.Contributors)
                .ThenInclude(contrib => contrib.User)
                .Include(survey => survey.Questions)
                .Include(survey => survey.Requests)
                .SingleOrDefaultAsync(s => s.Id == id)
                .ConfigureAwait(false);
        }

        public async Task<Survey> UpdateSurveyAsync(Survey survey)
        {
            _dbContext.Surveys.Attach(survey);
            _dbContext.Entry(survey).State = EntityState.Modified;
            await _dbContext
                .SaveChangesAsync()
                .ConfigureAwait(false);

            return survey;
        }

        public async Task<Survey> AddSurveyAsync(Survey survey)
        {
            _dbContext.Surveys.Add(survey);
            await _dbContext
                .SaveChangesAsync()
                .ConfigureAwait(false);
            return survey;
        }

        public async Task<Survey> DeleteSurveyAsync(Survey survey)
        {
            _dbContext.Surveys.Remove(survey);
            await _dbContext
                .SaveChangesAsync()
                .ConfigureAwait(false);
            return survey;
        }

        public async Task<Survey> PublishSurveyAsync(int id)
        {
            Survey survey = await _dbContext.Surveys
                .SingleOrDefaultAsync(s => s.Id == id)
                .ConfigureAwait(false);
            survey.Published = true;
            _dbContext.Surveys.Attach(survey);
            _dbContext.Entry(survey).State = EntityState.Modified;
            await _dbContext
                .SaveChangesAsync()
                .ConfigureAwait(false);
            return survey;
        }

        public async Task<Survey> UnPublishSurveyAsync(int id)
        {
            Survey survey = await _dbContext.Surveys
                .SingleOrDefaultAsync(s => s.Id == id)
                .ConfigureAwait(false);
            survey.Published = false;
            _dbContext.Surveys.Attach(survey);
            _dbContext.Entry(survey).State = EntityState.Modified;
            await _dbContext
                .SaveChangesAsync()
                .ConfigureAwait(false);
            return survey;
        }
    }
}
