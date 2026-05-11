using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MonetaCore.Data;
using MonetaCore.Middleware;
using MonetaCore.Models;
using MonetaCore.Security;
using MonetaCore.Services;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables(prefix: "MONETACORE_");

QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MonetaCore API",
        Version = "v1",
        Description = "MonetaCore operational APIs for invoices, payments, integrations, compliance, and portal access."
    });

    options.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = ".AspNetCore.Cookies",
        Description = "Cookie authentication generated after signing in through the web UI."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "cookieAuth"
                }
            },
            Array.Empty<string>()
        }
    });

    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    ServerVersion serverVersion;
    string? configuredServerVersion = builder.Configuration["DatabaseServerVersion"];

    if (builder.Environment.IsDevelopment())
    {
        serverVersion = ServerVersion.AutoDetect(connectionString);
    }
    else if (!string.IsNullOrWhiteSpace(configuredServerVersion))
    {
        serverVersion = ServerVersion.Parse(configuredServerVersion);
    }
    else
    {
        serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
    }

    options.UseMySql(connectionString, serverVersion);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IInvoiceNumberService, InvoiceNumberService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IComplianceService, ComplianceService>();
builder.Services.AddScoped<IEventOutboxService, EventOutboxService>();
builder.Services.AddScoped<IOutboxMessageDispatcher, AccountingApiOutboxMessageDispatcher>();
builder.Services.AddSingleton<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddHttpClient<IPayMongoService, PayMongoService>();
builder.Services.Configure<PayMongoOptions>(builder.Configuration.GetSection("PayMongo"));
builder.Services.Configure<ComplianceOptions>(builder.Configuration.GetSection("Compliance"));
builder.Services.Configure<OutboxDispatcherOptions>(builder.Configuration.GetSection("OutboxDispatcher"));
builder.Services.AddHostedService<OutboxDispatcherBackgroundService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.SuperAdminOnly, policy =>
        policy.RequireRole(ApplicationRoles.SuperAdmin));

    options.AddPolicy(AuthorizationPolicies.SuperOrMainAdmin, policy =>
        policy.RequireRole(ApplicationRoles.SuperAdmin, ApplicationRoles.MainAdmin));

    options.AddPolicy(AuthorizationPolicies.FinanceOperations, policy =>
        policy.RequireRole(
            ApplicationRoles.SuperAdmin,
            ApplicationRoles.MainAdmin,
            ApplicationRoles.FinanceManager,
            ApplicationRoles.Accountant));

    options.AddPolicy(AuthorizationPolicies.ClientManagement, policy =>
        policy.RequireRole(
            ApplicationRoles.SuperAdmin,
            ApplicationRoles.MainAdmin,
            ApplicationRoles.BillingStaff,
            ApplicationRoles.Accountant,
            ApplicationRoles.FinanceManager));

    options.AddPolicy(AuthorizationPolicies.IntegrationsOperations, policy =>
        policy.RequireRole(
            ApplicationRoles.SuperAdmin,
            ApplicationRoles.MainAdmin,
            ApplicationRoles.Accountant,
            ApplicationRoles.FinanceManager));

    options.AddPolicy(AuthorizationPolicies.BillingOperations, policy =>
        policy.RequireRole(
            ApplicationRoles.SuperAdmin,
            ApplicationRoles.MainAdmin,
            ApplicationRoles.BillingStaff,
            ApplicationRoles.FinanceManager));

    options.AddPolicy(AuthorizationPolicies.FinanceManagerOnly, policy =>
        policy.RequireRole(ApplicationRoles.FinanceManager));

    options.AddPolicy(AuthorizationPolicies.InvoiceCancellation, policy =>
        policy.RequireRole(
            ApplicationRoles.SuperAdmin,
            ApplicationRoles.MainAdmin,
            ApplicationRoles.FinanceManager));

    options.AddPolicy(AuthorizationPolicies.AuditAccess, policy =>
        policy.RequireRole(
            ApplicationRoles.SuperAdmin,
            ApplicationRoles.MainAdmin,
            ApplicationRoles.Auditor));
});

// Trust the X-Forwarded-For / X-Forwarded-Proto headers sent by Render's proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    await SeedData.InitializeAsync(dbContext, passwordService);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

bool enableApiDocumentation = app.Environment.IsDevelopment() ||
    app.Configuration.GetValue<bool>("ApiDocumentation:Enabled");

if (enableApiDocumentation)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "MonetaCore API v1");
        options.RoutePrefix = "api/docs";
        options.DocumentTitle = "MonetaCore API Documentation";
    });
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/livez", () => Results.Ok(new
{
    status = "alive",
    checkedAtUtc = DateTime.UtcNow
}));

app.MapGet("/healthz", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    bool databaseConnected;
    try
    {
        databaseConnected = await dbContext.Database.CanConnectAsync(cancellationToken);
    }
    catch
    {
        databaseConnected = false;
    }

    if (!databaseConnected)
    {
        return Results.Problem(
            title: "Database unavailable",
            detail: "MonetaCore could not connect to the configured database.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "healthy",
        checkedAtUtc = DateTime.UtcNow
    });
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
