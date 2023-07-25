﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using eCommerce.Core.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using eCommerce.Core.Shared;
using eCommerce.Core.Services.Configuration;
using eCommerce.Infrastructure.Infrastructure;
using eCommerce.Infrastructure.Persistence.DataProviders;
using eCommerce.Infrastructure.Persistence;
using FluentMigrator.Runner;
using System.Reflection;
using eCommerce.Core.Services.Caching;
using eCommerce.Infrastructure.Services.Caching;
using eCommerce.Infrastructure.Concretes;
using eCommerce.Core.Services.Messages;
using eCommerce.Infrastructure.Services.Messages;
using eCommerce.Core.Services.Customers;
using eCommerce.Core.Services.Localization;
using eCommerce.Core.Services.ScheduleTasks;
using eCommerce.Core.Services.Security;
using eCommerce.Infrastructure.Services.Configuration;
using eCommerce.Infrastructure.Services.Customers;
using eCommerce.Infrastructure.Services.Secuirty;
using eCommerce.Infrastructure.Services.Localization;
using eCommerce.Infrastructure.Services.ScheduleTasks;

namespace eCommerce.Infrastructure.Infrastructure;

public static class ServiceCollectionExtensions
{
    #region Public Methods

    public static IServiceCollection AddInfrastructureProject(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddJwtBearer(configuration)
            .AddServices(configuration)
            .RegisterAllSettings()
            .AddFluentMigrator(configuration);

        return services;
    }

    #endregion

    #region Methods

    private static IServiceCollection AddFluentMigrator(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddMySql5()
                .WithGlobalConnectionString(configuration.GetConnectionString("ConnectionString"))
                .ScanIn(Assembly.GetAssembly(typeof(ICustomDataProvider))).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddSingleton(configuration)
            .AddScoped(typeof(IRepository<>), typeof(Repository<>))
            .AddScoped<ICustomDataProvider, MySqlCustomDataProvider>()
            .AddHttpContextAccessor()
            .AddTransient(typeof(IConcurrentCollection<>), typeof(ConcurrentTrie<>))

            // Caching
            .AddSingleton<ILocker, MemoryCacheLocker>()
            .AddSingleton<ICacheKeyManager, CacheKeyManager>()
            .AddMemoryCache()
            .AddSingleton<IStaticCacheManager, MemoryCacheManager>()

            // Web helper
            .AddScoped<IWebHelper, WebHelper>()

            // Work context
            .AddScoped<IWorkContext, WorkContext>()

            // Email
            .AddScoped<ITokenizer, Tokenizer>()
            .AddScoped<IEmailSender, EmailSender>()

            // Services
            .AddScoped<ISettingService, SettingService>()
            .AddScoped<ICustomerService, CustomerService>()
            .AddScoped<IEncryptionService, EncryptionService>()
            .AddScoped<ILanguageService, LanguageService>()
            .AddScoped<ILocalizationService, LocalizationService>()
            .AddScoped<ILocalizedEntityService, LocalizedEntityService>()
            .AddScoped<IScheduleTaskService, ScheduleTaskService>()
            .AddScoped<IQueuedEmailService, QueuedEmailService>()
            .AddScoped<IWorkflowMessageService, WorkflowMessageService>()
            .AddScoped<IEmailAccountService, EmailAccountService>()
            .AddScoped<IEmailTemplateService, EmailTemplateService>()
            .AddScoped<IMessageTokenProvider, MessageTokenProvider>()
            .AddScoped<IJwtService, JwtService>();

        return services;
    }

    private static IServiceCollection RegisterAllSettings(this IServiceCollection services)
    {
        var settings = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(ISettings).IsAssignableFrom(p) && !p.IsInterface);

        foreach (var setting in settings)
        {
            services.AddScoped(setting, serviceProvider =>
            {
                return serviceProvider.GetRequiredService<ISettingService>().LoadSettingAsync(setting).Result;
            });
        }

        return services;
    }

    private static IServiceCollection AddJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new Exception("JWT Key Not Found!");
        }

        services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(o =>
        {
            var Key = Encoding.UTF8.GetBytes(jwtKey);
            o.SaveToken = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Key)
            };
        });

        return services;
    }

    #endregion
}
