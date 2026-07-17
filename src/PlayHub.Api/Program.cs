using System.Text;

using Hangfire;
using Hangfire.SqlServer;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.AspNetCore.Authorization;

using Microsoft.EntityFrameworkCore;

using Microsoft.IdentityModel.Tokens;

using Microsoft.OpenApi.Models;

using PlayHub.Api.Authorization;

using PlayHub.Api.Hubs;

using PlayHub.Api.Middleware;

using PlayHub.Api.Services;

using PlayHub.Application.Common;

using PlayHub.Application.Sessions;

using PlayHub.Infrastructure;

using PlayHub.Infrastructure.Data;



var builder = WebApplication.CreateBuilder(args);



builder.Services.AddControllers();

builder.Services.AddSignalR();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<ISessionNotifier, SessionHubNotifier>();

builder.Services.AddInfrastructure(builder.Configuration);



builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)

    .AddJwtBearer(options =>

    {

        var jwt = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters

        {

            ValidateIssuer = true,

            ValidateAudience = true,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt["Issuer"],

            ValidAudience = jwt["Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),

            ClockSkew = TimeSpan.FromMinutes(2)

        };



        // Allow JWT via query string for SignalR WebSocket connections

        options.Events = new JwtBearerEvents

        {

            OnMessageReceived = context =>

            {

                var accessToken = context.Request.Query["access_token"];

                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))

                    context.Token = accessToken;

                return Task.CompletedTask;

            }

        };

    });



builder.Services.AddAuthorization(options => PermissionPolicies.Register(options));

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();



builder.Services.AddCors(options =>

{

    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];

    options.AddPolicy("Frontend", policy =>

        policy.SetIsOriginAllowed(origin =>
        {
            if (corsOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                return true;

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                return false;

            // Support wildcard entries like "https://*.vercel.app" (covers production + preview deploys).
            return corsOrigins
                .Where(o => o.StartsWith("https://*.", StringComparison.OrdinalIgnoreCase))
                .Select(o => o["https://*.".Length..])
                .Any(domain => uri.Scheme == Uri.UriSchemeHttps
                    && (uri.Host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                        || uri.Host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)));
        })

            .AllowAnyHeader()

            .AllowAnyMethod()

            .AllowCredentials());

});



builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>

{

    options.SwaggerDoc("v1", new OpenApiInfo

    {

        Title = "PlayHub API",

        Version = "v1",

        Description = "Multi-tenant SaaS API for PlayStation shop & cafeteria management"

    });



    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme

    {

        Name = "Authorization",

        Type = SecuritySchemeType.Http,

        Scheme = "bearer",

        BearerFormat = "JWT",

        In = ParameterLocation.Header,

        Description = "Enter JWT token"

    });



    options.AddSecurityRequirement(new OpenApiSecurityRequirement

    {

        {

            new OpenApiSecurityScheme

            {

                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }

            },

            Array.Empty<string>()

        }

    });

});



builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("HrConnection")));

builder.Services.AddHangfireServer();



var app = builder.Build();



if (app.Environment.IsDevelopment())

{

    app.UseSwagger();

    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "PlayHub API v1"));

}



app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("Frontend");

app.UseAuthentication();

app.UseMiddleware<TenantMiddleware>();

app.UseAuthorization();



app.MapControllers();

app.MapHub<BranchSessionHub>("/hubs/sessions");

app.MapHangfireDashboard("/hangfire");



using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlayHubDbContext>();
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE b
        SET OwnerUserId = (
            SELECT TOP 1 u.Id
            FROM users u
            WHERE u.TenantId = b.TenantId
              AND u.IsMaster = 1
              AND u.IsDeleted = 0
            ORDER BY u.CreatedAt
        )
        FROM branches b
        WHERE b.OwnerUserId IS NULL
        """);
    await DatabaseSeeder.SeedAsync(db, app.Configuration);
}

RecurringJob.AddOrUpdate<PlayHub.Infrastructure.Jobs.SubscriptionExpiryJob>(
    "subscription-expiry",
    job => job.RunAsync(),
    Cron.Daily(0, 5)); // 00:05 UTC daily

RecurringJob.AddOrUpdate<PlayHub.Infrastructure.Jobs.DeviceMaintenanceReminderJob>(
    "device-maintenance-reminder",
    job => job.RunAsync(),
    Cron.Weekly(DayOfWeek.Sunday, 8)); // Sunday 08:00 UTC

app.Run();


