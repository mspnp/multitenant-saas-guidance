// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Tailspin.Surveys.Data.DataModels;
using Tailspin.Surveys.Data.DataStore;
using Tailspin.Surveys.Security.Policy;
using AppConfiguration = Tailspin.Surveys.WebAPI.Configuration;
using Constants = Tailspin.Surveys.Common.Constants;

namespace Tailspin.Surveys.WebAPI
{
    /// <summary>
    /// This class contains the starup logic for this WebAPI project.
    /// </summary>
    public class Startup
    {
        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            InitializeLogging(loggerFactory);
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json");

            if (env.IsDevelopment())
            {
                // This reads the configuration keys from the secret store.
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets<Startup>();
            }
            builder.AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by a runtime.
        // Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(PolicyNames.RequireSurveyCreator,
                    policy =>
                    {
                        policy.AddRequirements(new SurveyCreatorRequirement());
                        policy.RequireAuthenticatedUser(); // Adds DenyAnonymousAuthorizationRequirement 
                        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    });
                options.AddPolicy(PolicyNames.RequireSurveyAdmin,
                    policy =>
                    {
                        policy.AddRequirements(new SurveyAdminRequirement());
                        policy.RequireAuthenticatedUser(); // Adds DenyAnonymousAuthorizationRequirement 
                        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                    });
            });

            // Add Entity Framework services to the services container.
            services.AddEntityFrameworkSqlServer()
                .AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(Configuration.GetSection("Data")["SurveysConnectionString"]));

            services.AddScoped<TenantManager, TenantManager>();
            services.AddScoped<UserManager, UserManager>();

            services.AddMvc();

            services.AddScoped<ISurveyStore, SqlServerSurveyStore>();
            services.AddScoped<IQuestionStore, SqlServerQuestionStore>();
            services.AddScoped<IContributorRequestStore, SqlServerContributorRequestStore>();
            services.AddSingleton<IAuthorizationHandler>(factory =>
            {
                var loggerFactory = factory.GetService<ILoggerFactory>();
                return new SurveyAuthorizationHandler(loggerFactory.CreateLogger<SurveyAuthorizationHandler>());
            });

            //
            //http://stackoverflow.com/questions/37371264/asp-net-core-rc2-invalidoperationexception-unable-to-resolve-service-for-type/37373557
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            //
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ApplicationDbContext dbContext, ILoggerFactory loggerFactory)
        {
            var configOptions = new AppConfiguration.ConfigurationOptions();
            Configuration.Bind(configOptions);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseDatabaseErrorPage();
            }

            app.UseJwtBearerAuthentication(new JwtBearerOptions {
                Audience = configOptions.AzureAd.WebApiResourceId,
                Authority = Constants.AuthEndpointPrefix,
                TokenValidationParameters = new TokenValidationParameters {
                    ValidateIssuer = false
                },
                Events= new SurveysJwtBearerEvents(loggerFactory.CreateLogger<SurveysJwtBearerEvents>())
            });
            
            // Add MVC to the request pipeline.
            app.UseMvc();
        }
        private void InitializeLogging(ILoggerFactory loggerFactory)
        {
            loggerFactory.AddDebug(LogLevel.Information);
        }
    }
}