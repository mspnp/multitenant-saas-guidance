using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Tailspin.Surveys.Data.DataModels;
using Microsoft.Data.Entity;
using Tailspin.Surveys.Data.DataStore;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Tailspin.Surveys.Tests
{
    // This test uses the InMemoryDatabase to simulate the SQL database.
    // Be aware that an in-memory DB will never exactly match the behavior of the real DB.

    // When using InMemoryDatabase for unit testing, create a new database for each test,
    // and don't re-use the same DbContext to populate the database and run the queries.

    public class SurveyStoreTests
    {
        private readonly ServiceCollection _serviceCollection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public SurveyStoreTests()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseInMemoryDatabase();
            _options = optionsBuilder.Options;

            _serviceCollection = new ServiceCollection();
            _serviceCollection
                .AddEntityFramework()
                .AddInMemoryDatabase();
            _serviceCollection.AddTransient<ApplicationDbContext>(provider => new ApplicationDbContext(provider, _options));
            _serviceCollection.AddTransient<SqlServerSurveyStore>();
        }

        private ApplicationDbContext GetContext(IServiceProvider provider)
        {
            return new ApplicationDbContext(provider, _options);
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_CorrectSurvey()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                context.Add(new Survey { Id = 1 });
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetSurveyAsync(1);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_Survey_Contributors()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                var survey = new Survey
                {
                    Id = 1,
                    Contributors = new List<SurveyContributor>
                    {
                        new SurveyContributor { SurveyId = 1, UserId = 2 }
                    }
                };
                context.Add(survey);
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetSurveyAsync(1);

            Assert.NotNull(result.Contributors);
            Assert.NotEmpty(result.Contributors);
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_Survey_Questions()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                var survey = new Survey
                {
                    Id = 1,
                    Questions = new List<Question>
                    {
                        new Question { SurveyId = 1  }
                    }
                };
                context.Add(survey);
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetSurveyAsync(1);

            Assert.NotNull(result.Questions);
            Assert.NotEmpty(result.Questions);
        }

        [Fact]
        public async Task GetSurveyAsync_Returns_Survey_Requests()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                var survey = new Survey
                {
                    Id = 1,
                    Requests = new List<ContributorRequest>
                    {
                        new ContributorRequest()
                    }
                };
                context.Add(survey);
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetSurveyAsync(1);

            Assert.NotNull(result.Requests);
            Assert.NotEmpty(result.Requests);
        }

        [Fact]
        public async Task GetSurveysByOwnerAsync_Returns_CorrectSurveys()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                context.AddRange(
                    new Survey { Id = 1, OwnerId = 1 },
                    new Survey { Id = 2, OwnerId = 1 },
                    new Survey { Id = 3, OwnerId = 2 }
                    );
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetSurveysByOwnerAsync(1);

            Assert.NotEmpty(result);
            // Returned collection should only contain surveys with the matching owner ID.
            Assert.True(result.All(s => s.OwnerId == 1));
        }

        [Fact]
        public async Task GetPublishedSurveysByOwnerAsync_Returns_PublishedSurveys()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                context.AddRange(
                    new Survey { Id = 1, OwnerId = 1 },
                    new Survey { Id = 2, OwnerId = 1, Published = true },
                    new Survey { Id = 3, OwnerId = 1, Published = true },
                    new Survey { Id = 4, OwnerId = 2, Published = true }  
                    );
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetPublishedSurveysByOwnerAsync(1);

            Assert.Equal(2, result.Count);
            Assert.True(result.All(s => s.OwnerId == 1));  // must match owner ID
            Assert.True(result.All(s => s.Published == true)); // only published surveys
        }

        [Fact]
        public async Task GetSurveysByContributorAsync_Returns_CorrectSurveys()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                context.AddRange(
                    new SurveyContributor { SurveyId = 1, UserId = 10 },
                    new SurveyContributor { SurveyId = 2, UserId = 10 },
                    new SurveyContributor { SurveyId = 3, UserId = 20 }
                    );
                context.AddRange(
                    new Survey { Id = 1, OwnerId = 1 },
                    new Survey { Id = 2, OwnerId = 2 },
                    new Survey { Id = 3, OwnerId = 3 },
                    new Survey { Id = 4, OwnerId = 4 }
                    );

                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetSurveysByContributorAsync(10);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, s => s.Id == 1);
            Assert.Contains(result, s => s.Id == 2);
        }

        [Fact]
        public async Task GetPublishedSurveysByTenantAsync_Returns_CorrectSurveys()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                context.AddRange(
                    new Survey { Id = 1, TenantId = 1 },
                    new Survey { Id = 2, TenantId = 1, Published = true },
                    new Survey { Id = 3, TenantId = 2 },
                    new Survey { Id = 4, TenantId = 2, Published = true }
                    );
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetPublishedSurveysByTenantAsync(1);

            Assert.Equal(1, result.Count);
            Assert.Equal(2, result.First().Id);
        }

        [Fact]
        public async Task GetUnPublishedSurveysByTenantAsync_Returns_CorrectSurveys()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                context.AddRange(
                    new Survey { Id = 1, TenantId = 1 },
                    new Survey { Id = 2, TenantId = 1, Published = true },
                    new Survey { Id = 3, TenantId = 2 },
                    new Survey { Id = 4, TenantId = 2, Published = true }
                    );
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetUnPublishedSurveysByTenantAsync(1);

            Assert.Equal(1, result.Count);
            Assert.Equal(1, result.First().Id);
        }

        [Fact]
        public async Task GetPublishedSurveysAsync_Returns_CorrectSurveys()
        {
            IServiceProvider provider = _serviceCollection.BuildServiceProvider();
            using (var context = provider.GetService<ApplicationDbContext>())
            {
                context.AddRange(
                    new Survey { Id = 1, TenantId = 1 },
                    new Survey { Id = 2, TenantId = 1, Published = true },
                    new Survey { Id = 3, TenantId = 2 },
                    new Survey { Id = 4, TenantId = 2, Published = true }
                    );
                context.SaveChanges();
            }

            var store = provider.GetService<SqlServerSurveyStore>();
            var result = await store.GetPublishedSurveysAsync();

            Assert.Equal(2, result.Count);
            Assert.True(result.All(x => x.Published));
        }
    }
}
