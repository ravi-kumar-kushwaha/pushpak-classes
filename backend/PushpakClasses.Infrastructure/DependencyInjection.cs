using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;

namespace PushpakClasses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? configuration["DB_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Data Source=pushpakclasses.db";

        var isSqlite = connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            || connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuration["DatabaseProvider"], "Sqlite", StringComparison.OrdinalIgnoreCase);

        if (isSqlite)
        {
            services.AddDbContextPool<PushpakClassesDbContext>(options =>
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly(typeof(PushpakClassesDbContext).Assembly.FullName);
                }), poolSize: 256);
        }
        else
        {
            services.AddDbContextPool<PushpakClassesDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(PushpakClassesDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }), poolSize: 1024);
        }

        services.AddScoped<IPushpakClassesDbContext>(sp => sp.GetRequiredService<PushpakClassesDbContext>());
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<DbSeeder>();

        // Configurations
        services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
        services.Configure<HangfireSettings>(configuration.GetSection(HangfireSettings.SectionName));

        // JWT Token Service
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Email Service (In Development or when Provider != "Smtp", always use LoggingEmailService so signup never fails)
        var emailProvider = configuration.GetSection($"{EmailSettings.SectionName}:Provider").Value;
        var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? configuration["ASPNETCORE_ENVIRONMENT"];
        var isDevelopment = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase);

        if (!isDevelopment && string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddTransient<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddTransient<IEmailService, LoggingEmailService>();
        }

        // Auth & User Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminUserService, AdminUserService>();

        // Quiz Management Services
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IQuestionImportService, QuestionImportService>();
        services.AddScoped<IQuestionService, QuestionService>();

        // Quiz Attempt & Scoring Services
        services.AddSingleton<IRedisService, RedisService>();
        services.AddScoped<IQuizAttemptService, QuizAttemptService>();
        services.AddScoped<IScoringService, ScoringService>();
        services.AddScoped<IAutoSubmitJob, AutoSubmitJob>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPdfCertificateService, PdfCertificateService>();

        // Hangfire Setup
        var hangfireSettings = configuration.GetSection(HangfireSettings.SectionName).Get<HangfireSettings>();
        if (hangfireSettings?.UseInMemory == true)
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMemoryStorage());
        }
        else
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString));
        }

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(Environment.ProcessorCount * 2, 4);
        });

        return services;
    }
}
