using System.Text.Json.Serialization;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SubTracker.Api.Errors;
using SubTracker.Api.Infrastructure;
using SubTracker.Api.Mapping;
using SubTracker.Api.Security;
using SubTracker.Api.Services;
using SubTracker.Api.Validation;
using SubTracker.DB;

var builder = WebApplication.CreateBuilder(args);

// Рядок підключення зберігається в User Secrets (той самий UserSecretsId, що й у консольного застосунку ЛР № 2).
// У Development секрети підключаються автоматично; для профілю production-local — тут. Змінні середовища
// і командний рядок додаються повторно, щоб і далі мати вищий пріоритет (окрема БД для навантажувальних тестів).
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);
}

var connectionString = builder.Configuration.GetConnectionString("SubTrackerDb")
    ?? throw new InvalidOperationException("Не задано ConnectionStrings:SubTrackerDb (dotnet user-secrets).");
var oidc = builder.Configuration.GetSection(OidcOptions.SectionName).Get<OidcOptions>() ?? new OidcOptions();

// ---------- Дані ----------
// DbContext — Scoped (за замовчуванням AddDbContext): один екземпляр і одна одиниця роботи на HTTP-запит.
builder.Services.AddDbContext<SubTrackerDbContext>(o => o.UseSubTracker(connectionString));

// ---------- MVC, JSON, валідація ----------
builder.Services
    .AddControllers(o => o.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions));
// Ті самі налаштування для генератора OpenAPI (він читає HTTP JsonOptions): enum-и рядками, числа без "string".
builder.Services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));
// Валідатори FluentValidation не мають стану, але TimeProvider — singleton, тож Scoped за замовчуванням безпечний.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ---------- Версіонування: api/v1/... ----------
builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.ApiVersionReader = new UrlSegmentApiVersionReader(); // версія лише в URI: api/v1/...
        o.ReportApiVersions = true; // заголовок api-supported-versions у відповідях
    })
    .AddMvc()
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";      // група «v1» → документ OpenAPI «v1»
        o.SubstituteApiVersionInUrl = true; // у документації /api/v1/… замість /api/v{version}/…
    });

// ---------- Автентифікація: JWT-токени від Keycloak (OIDC) ----------
builder.Services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.SectionName));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = oidc.Authority;              // звідси береться /.well-known/openid-configuration і ключі (JWKS)
        o.Audience = oidc.Audience;                // токен має бути виданий саме для subtracker-api (claim aud)
        o.RequireHttpsMetadata = oidc.RequireHttpsMetadata; // false лише для локального Keycloak по http
        o.MapInboundClaims = false;                // залишити імена claims як у токені: sub, roles, email
        o.TokenValidationParameters.NameClaimType = "preferred_username";
        o.TokenValidationParameters.RoleClaimType = oidc.RoleClaimType;
        o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
    });

// ---------- Авторизація: політики + обробники ресурсів ----------
builder.Services.AddAuthorizationBuilder().AddSubTrackerPolicies();
// Обробники не мають стану і не залежать від scoped-сервісів → Singleton.
builder.Services.AddSingleton<IAuthorizationHandler, SubscriptionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, OwnedResourceAuthorizationHandler>();

// ---------- Сервісний шар ----------
builder.Services.AddHttpContextAccessor();                       // Singleton (сам читає HttpContext через AsyncLocal)
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); // кеш користувача в межах запиту + DbContext
builder.Services.AddScoped<ICategoryService, CategoryService>();       // залежать від DbContext (Scoped) →
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>(); // не можуть жити довше за нього
builder.Services.AddScoped<IPaymentMethodService, PaymentMethodService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ApiMapper>();                      // згенерований код без стану
builder.Services.AddSingleton(TimeProvider.System);              // системний годинник; у тестах підміняється
builder.Services.Configure<PerformanceOptions>(builder.Configuration.GetSection(PerformanceOptions.SectionName));

// ---------- Кешування відповідей (лише в режимі Performance:Optimized) ----------
builder.Services.AddOutputCache(CachePolicies.Configure);

// ---------- Помилки: ProblemDetails (RFC 9457) ----------
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Instance ??= $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- OpenAPI ----------
builder.Services.AddSubTrackerOpenApi(oidc);

var app = builder.Build();

// Окремий режим: наповнити базу для навантажувального тестування і завершити роботу.
var seedIndex = Array.IndexOf(args, "--seed-load-data");
if (seedIndex >= 0)
{
    var count = seedIndex + 1 < args.Length && int.TryParse(args[seedIndex + 1], out var n) ? n : 5000;
    await using var scope = app.Services.CreateAsyncScope();
    await LoadDataSeeder.RunAsync(scope.ServiceProvider.GetRequiredService<SubTrackerDbContext>(), count);
    return;
}

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    // Застосувати нові міграції (AddUserExternalId) і початкові дані під час запуску в Development.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SubTrackerDbContext>().Database.MigrateAsync();
}

// ---------- Конвеєр обробки запиту ----------
app.UseExceptionHandler();   // необроблені винятки → GlobalExceptionHandler → ProblemDetails
app.UseStatusCodePages();    // тіло ProblemDetails для 401/403/404 без тіла
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();        // після авторизації: користувач уже відомий (кеш розділено за sub)

app.MapControllers();

// Документація відкрита без токена (fallback-політика вимагає автентифікації для всього іншого).
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // /openapi/v1.json
    app.MapScalarApiReference(o => o
            .WithTitle("SubTracker API")
            .AddPreferredSecuritySchemes(OpenApiSetup.SchemeName)
            .AddAuthorizationCodeFlow(OpenApiSetup.SchemeName, flow =>
            {
                flow.ClientId = oidc.ClientId;   // публічний клієнт без секрету
                flow.Pkce = Pkce.Sha256;         // PKCE S256
                flow.SelectedScopes = ["openid", "profile", "email"];
            }))
        .AllowAnonymous();                       // /scalar/v1
}

app.Run();

static void ConfigureJson(System.Text.Json.JsonSerializerOptions json)
{
    json.Converters.Add(new JsonStringEnumConverter());
    json.NumberHandling = JsonNumberHandling.Strict; // "amount": 10.5, а не "10.5"
}

/// <summary>Потрібен для WebApplicationFactory&lt;Program&gt; в інтеграційних тестах.</summary>
public partial class Program;

