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
    // Infer catalog owners from room/device usage so masters don't see each other's stock.
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE vat
        SET OwnerUserId = (
            SELECT TOP 1 b.OwnerUserId
            FROM RoomAssets ra
            INNER JOIN Rooms r ON r.Id = ra.RoomId
            INNER JOIN Branches b ON b.Id = r.BranchId
            WHERE ra.VenueAssetTypeId = vat.Id
              AND b.OwnerUserId IS NOT NULL
            ORDER BY b.CreatedAt
        )
        FROM VenueAssetTypes vat
        WHERE vat.OwnerUserId IS NULL
        """);
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE ct
        SET OwnerUserId = (
            SELECT TOP 1 b.OwnerUserId
            FROM Devices d
            INNER JOIN DeviceControllers dc ON dc.DeviceId = d.Id
            INNER JOIN Branches b ON b.Id = d.BranchId
            WHERE dc.ControllerTypeId = ct.Id
              AND b.OwnerUserId IS NOT NULL
            ORDER BY b.CreatedAt
        )
        FROM ControllerTypes ct
        WHERE ct.OwnerUserId IS NULL
        """);
    // Fix catalogs used only on one master's branches (wrong OwnerUserId from earlier leaks).
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE vat
        SET OwnerUserId = owners.OwnerUserId
        FROM VenueAssetTypes vat
        INNER JOIN (
            SELECT ra.VenueAssetTypeId AS Id, MIN(b.OwnerUserId) AS OwnerUserId
            FROM RoomAssets ra
            INNER JOIN Rooms r ON r.Id = ra.RoomId
            INNER JOIN Branches b ON b.Id = r.BranchId
            WHERE b.OwnerUserId IS NOT NULL
            GROUP BY ra.VenueAssetTypeId
            HAVING COUNT(DISTINCT b.OwnerUserId) = 1
        ) owners ON owners.Id = vat.Id
        WHERE vat.OwnerUserId IS NULL OR vat.OwnerUserId <> owners.OwnerUserId
        """);
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE ct
        SET OwnerUserId = owners.OwnerUserId
        FROM ControllerTypes ct
        INNER JOIN (
            SELECT dc.ControllerTypeId AS Id, MIN(b.OwnerUserId) AS OwnerUserId
            FROM DeviceControllers dc
            INNER JOIN Devices d ON d.Id = dc.DeviceId
            INNER JOIN Branches b ON b.Id = d.BranchId
            WHERE b.OwnerUserId IS NOT NULL
            GROUP BY dc.ControllerTypeId
            HAVING COUNT(DISTINCT b.OwnerUserId) = 1
        ) owners ON owners.Id = ct.Id
        WHERE ct.OwnerUserId IS NULL OR ct.OwnerUserId <> owners.OwnerUserId
        """);
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE vat
        SET OwnerUserId = (
            SELECT TOP 1 u.Id
            FROM users u
            WHERE u.TenantId = vat.TenantId
              AND u.IsMaster = 1
              AND u.IsDeleted = 0
            ORDER BY u.CreatedAt
        )
        FROM VenueAssetTypes vat
        WHERE vat.OwnerUserId IS NULL
        """);
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE ct
        SET OwnerUserId = (
            SELECT TOP 1 u.Id
            FROM users u
            WHERE u.TenantId = ct.TenantId
              AND u.IsMaster = 1
              AND u.IsDeleted = 0
            ORDER BY u.CreatedAt
        )
        FROM ControllerTypes ct
        WHERE ct.OwnerUserId IS NULL
        """);
    // Inventory units: exclusive cafeteria usage → correct owner.
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE iu
        SET OwnerUserId = owners.OwnerUserId
        FROM inventory_units iu
        INNER JOIN (
            SELECT iu2.Id AS Id, MIN(b.OwnerUserId) AS OwnerUserId
            FROM inventory_units iu2
            INNER JOIN cafeteria_items ci
                ON ci.TenantId = iu2.TenantId
               AND ci.IsDeleted = 0
               AND (ci.BaseUnitName = iu2.Name OR ci.LargeUnitName = iu2.Name)
            INNER JOIN branches b ON b.Id = ci.BranchId
            WHERE b.OwnerUserId IS NOT NULL
            GROUP BY iu2.Id
            HAVING COUNT(DISTINCT b.OwnerUserId) = 1
        ) owners ON owners.Id = iu.Id
        WHERE iu.OwnerUserId IS NULL OR iu.OwnerUserId <> owners.OwnerUserId
        """);
    await db.Database.ExecuteSqlRawAsync("""
        UPDATE iu
        SET OwnerUserId = (
            SELECT TOP 1 u.Id
            FROM users u
            WHERE u.TenantId = iu.TenantId
              AND u.IsMaster = 1
              AND u.IsDeleted = 0
            ORDER BY u.CreatedAt
        )
        FROM inventory_units iu
        WHERE iu.OwnerUserId IS NULL
        """);
    // Drop MasterAdmin UserBranch links to branches they don't own (stops cross-master item leak).
    // Do not touch SuperAdmin assignments.
    await db.Database.ExecuteSqlRawAsync("""
        DELETE ub
        FROM UserBranches ub
        INNER JOIN users u ON u.Id = ub.UserId
        INNER JOIN branches b ON b.Id = ub.BranchId
        WHERE u.Role = 1
          AND u.IsDeleted = 0
          AND b.OwnerUserId IS NOT NULL
          AND b.OwnerUserId <> u.Id
        """);
    // Staff must not keep branches owned by a different master than their parent.
    await db.Database.ExecuteSqlRawAsync("""
        DELETE ub
        FROM UserBranches ub
        INNER JOIN users u ON u.Id = ub.UserId
        INNER JOIN branches b ON b.Id = ub.BranchId
        WHERE u.Role = 0
          AND u.IsDeleted = 0
          AND u.ParentUserId IS NOT NULL
          AND b.OwnerUserId IS NOT NULL
          AND b.OwnerUserId <> u.ParentUserId
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


