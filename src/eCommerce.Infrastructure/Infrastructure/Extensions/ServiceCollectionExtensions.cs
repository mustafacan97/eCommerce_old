﻿using eCommerce.Infrastructure.Persistence.Primitives;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Reflection;
using YerdenYuksek.Application.Services.Public.Customers;
using YerdenYuksek.Application.Services.Public.Localization;
using YerdenYuksek.Application.Services.Public.Messages;
using YerdenYuksek.Application.Services.Public.Security;
using YerdenYuksek.Core.Caching;
using YerdenYuksek.Core.Infrastructure;
using YerdenYuksek.Web.Framework.Common;
using YerdenYuksek.Web.Framework.Infrastructure;
using YerdenYuksek.Web.Framework.Persistence;
using YerdenYuksek.Web.Framework.Persistence.Services.Public;
using TaskScheduler = eCommerce.Infrastructure.Persistence.Services.ScheduleTasks.TaskScheduler;
using ScheduleTaskRunner = eCommerce.Infrastructure.Persistence.Services.ScheduleTasks.ScheduleTaskRunner;
using ScheduleTaskService = eCommerce.Infrastructure.Persistence.Services.ScheduleTasks.ScheduleTaskService;
using eCommerce.Core.Interfaces;
using eCommerce.Core.Helpers;
using eCommerce.Core.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using eCommerce.Application.Services.Public.Security;
using eCommerce.Infrastructure.Persistence.Services.Public;
using eCommerce.Infrastructure.Persistence.Services.ScheduleTasks;
using eCommerce.Application.Services.ScheduleTasks;
using eCommerce.Core.Configuration;
using eCommerce.Application.Services.Configuration;

namespace eCommerce.Framework.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    #region Public Methods

    public static IServiceCollection RegisterServiceCollections(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .ConfigureApiBehaviorOptions()
            .ConfigureApplicationSettings(environment)
            .AddJwtBearer(configuration)
            .AddServices()
            .RegisterAllSettings()
            .AddEntityFramework(configuration)
            .AddFluentValidation();

        return services;
    }

    #endregion

    #region Methods

    private static IServiceCollection ConfigureApiBehaviorOptions(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

        return services;
    }

    /// <summary>
    /// Get all config files in solution and save them to appsettings.json file
    /// </summary>
    private static IServiceCollection ConfigureApplicationSettings(this IServiceCollection services, IHostEnvironment environment)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;

        CommonHelper.DefaultFileProvider = new YerdenYuksekFileProvider(environment);

        var typeFinder = new TypeFinder(CommonHelper.DefaultFileProvider);
        Singleton<ITypeFinder>.Instance = typeFinder;
        services.AddSingleton<ITypeFinder>(typeFinder);

        var configurations = typeFinder
                .FindClassesOfType<IConfig>()
                .Select(configType => (IConfig)Activator.CreateInstance(configType))
                .ToList();

        var appSettings = AppSettingsHelper.SaveAppSettings(configurations, CommonHelper.DefaultFileProvider, true);
        services.AddSingleton(appSettings);

        return services;
    }

    private static IServiceCollection AddEntityFramework(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("ConnectionString") ??
            throw new InvalidOperationException("ConnectionString not found.");

        ServerVersion serverVersion = ServerVersion.AutoDetect(connectionString);

        services.AddDbContext<ApplicationDbContext>(opt =>
        {
            opt.UseMySql(connectionString, serverVersion);
        });

        return services;
    }

    public static IServiceCollection AddUnitOfWork<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        services.AddScoped<IUnitOfWork, UnitOfWork<TContext>>();
        services.AddScoped<IUnitOfWork<TContext>, UnitOfWork<TContext>>();

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        //register engine
        services.AddSingleton<IEngine, Engine>();

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddUnitOfWork<ApplicationDbContext>();
        services.AddSingleton<ILocker, MemoryCacheLocker>();
        services.AddSingleton<IStaticCacheManager, MemoryCacheManager>();

        //file provider
        services.AddScoped<IYerdenYuksekFileProvider, YerdenYuksekFileProvider>();

        //add accessor to HttpContext
        services.AddHttpContextAccessor();

        //web helper
        services.AddScoped<IWebHelper, WebHelper>();

        //work context
        services.AddScoped<IWorkContext, WorkContext>();

        //static cache manager
        services.AddTransient(typeof(IConcurrentCollection<>), typeof(ConcurrentTrie<>));
        services.AddSingleton<ICacheKeyManager, CacheKeyManager>();
        services.AddMemoryCache();
        services.AddSingleton<IStaticCacheManager, MemoryCacheManager>();

        //email
        services.AddScoped<ITokenizer, Tokenizer>();
        services.AddScoped<IEmailSender, EmailSender>();

        //services
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IEncryptionService, EncryptionService>();
        services.AddScoped<ILanguageService, LanguageService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<ILocalizedEntityService, LocalizedEntityService>();
        services.AddScoped<IScheduleTaskService, ScheduleTaskService>();
        services.AddScoped<IQueuedEmailService, QueuedEmailService>();
        services.AddScoped<IWorkflowMessageService, WorkflowMessageService>();
        services.AddScoped<IMessageTemplateService, MessageTemplateService>();
        services.AddScoped<IMessageTokenProvider, MessageTokenProvider>();
        services.AddScoped<IJwtService, JwtService>();

        //register all settings
        services.RegisterAllSettings();

        //schedule tasks
        services.AddSingleton<ITaskScheduler, TaskScheduler>();
        services.AddTransient<IScheduleTaskRunner, ScheduleTaskRunner>();

        return services;
    }

    private static IServiceCollection RegisterAllSettings(this IServiceCollection services)
    {
        var typeFinder = Singleton<ITypeFinder>.Instance;
        var settings = typeFinder.FindClassesOfType(typeof(ISettings), false).ToList();

        foreach (var setting in settings)
        {
            services.AddScoped(setting, serviceProvider =>
            {
                return serviceProvider.GetRequiredService<ISettingService>().LoadSettingAsync(setting).Result;
            });
        }
        
        return services;
    }

    private static IServiceCollection AddFluentValidation(this IServiceCollection services)
    {
        services
            .AddFluentValidationAutoValidation()
            .AddValidatorsFromAssembly(Assembly.GetEntryAssembly())
            .AddFluentValidationClientsideAdapters();

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
