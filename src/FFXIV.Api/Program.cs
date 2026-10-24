using System.IO.Compression;
using System.Threading.RateLimiting;
using FFXIV.Api.Filters;
using FFXIV.Api.Health;
using FFXIV.Api.Middleware;
using FFXIV.Api.RateLimiting;
using FFXIV.Application;
using FFXIV.Infrastructure;
using FFXIV.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi;
using Serilog;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog. The container's stdout is the log sink in production (Container Apps
// collects it); a file under the app directory would only fill the writable layer.
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

if (builder.Environment.IsDevelopment())
{
    loggerConfiguration.WriteTo.File(
        path: "Logs/ffxiv-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        formatProvider: CultureInfo.InvariantCulture);
}

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();

// Sentry error monitoring (conditional)
if (builder.Configuration.GetValue<bool>("Sentry:Enabled"))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = builder.Configuration["Sentry:Dsn"] ?? "";
        o.TracesSampleRate = builder.Configuration.GetValue<double>("Sentry:TracesSampleRate", 0.1);
        o.SendDefaultPii = false;
    });
}

// Add services to the container
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Request DTOs carry DataAnnotations; a failed one answers with the same { error } shape
        // the hand-written checks use, so clients deal with one error format.
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = context.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? "The request is invalid.";
            return new BadRequestObjectResult(new { error = message });
        };
    });
builder.Services.AddScoped<AdminApiKeyFilter>();

// Azure Container Apps terminates TLS and appends the client address to X-Forwarded-For; only
// the rightmost entry is the ingress's own, so exactly one hop is trusted and no proxy list is
// needed. Without this every client shares the ingress address, and per-client rate limits,
// request logs and the HTTPS scheme are all wrong.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);

// Rate limiting. Every policy is partitioned per client (the signed-in user, else the address
// the ingress forwarded), so one caller cannot exhaust a budget for the whole site.
var rateLimits = builder.Configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>()
    ?? new RateLimitSettings();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
        RateLimitPartitions.SlidingWindow(context, rateLimits.AuthPerFiveMinutes, TimeSpan.FromMinutes(5)));
    options.AddPolicy("register", context =>
        RateLimitPartitions.FixedWindow(context, rateLimits.RegisterPerHour, TimeSpan.FromHours(1)));
    options.AddPolicy("public-read", context =>
        RateLimitPartitions.SlidingWindow(context, rateLimits.PublicReadPerMinute, TimeSpan.FromMinutes(1)));
    options.AddPolicy("rotation-write", context =>
        RateLimitPartitions.FixedWindow(context, rateLimits.RotationWritePerHour, TimeSpan.FromHours(1)));
    options.AddPolicy("feedback", context =>
        RateLimitPartitions.FixedWindow(context, rateLimits.FeedbackPerHour, TimeSpan.FromHours(1)));

    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Rate limit exceeded. Try again later." }, ct);
    };
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "FFXIV Crafting Companion API",
        Version = "v1",
        Description = "API for FFXIV crafting recipes, items, and simulation"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    // Without the host document the reference serializes as {}, and Swagger UI never sends the token.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000"];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddCheck<XivApiHealthCheck>("xivapi")
    .AddCheck<RedisHealthCheck>("redis");

var app = builder.Build();

// Apply pending migrations on startup if configured
if (app.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline
app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiVersionHeaderMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FFXIV Crafting Companion API v1");
    });
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors("DefaultPolicy");

app.UseAuthentication();
app.UseAuthorization();

// After authentication, so the policies can partition by the signed-in user.
app.UseRateLimiter();

app.MapControllers();

// The full readiness report opens SQL (which resumes a paused free-tier database) and calls
// XIVAPI, and it names components and error messages. It answers 401 without running a single
// check unless the admin key is present; nothing anonymous can make the API wake the database.
app.MapGet("/health", async (
    HttpContext context,
    HealthCheckService healthChecks,
    IConfiguration configuration,
    CancellationToken ct) =>
{
    if (!AdminApiKeyFilter.Matches(context.Request, configuration))
        return Results.Unauthorized();

    var report = await healthChecks.CheckHealthAsync(ct);
    var body = await HealthCheckResponseWriter.BuildResponseAsync(context.RequestServices, report, ct);
    return Results.Json(body, HealthCheckResponseWriter.JsonOptions,
        statusCode: HealthCheckResponseWriter.StatusCodeFor(report));
})
.ExcludeFromDescription();

// Liveness must not depend on the database or XIVAPI: a paused free-tier DB would otherwise
// fail the probe and restart the container forever. This is the Container Apps probe.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

try
{
    Log.Information("Starting FFXIV Crafting Companion API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
