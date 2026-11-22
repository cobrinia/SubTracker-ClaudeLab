using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SubTracker.DB;

namespace SubTracker.Api.Tests;

/// <summary>
/// Піднімає API в пам'яті з окремою тестовою БД. Справжня перевірка JWT від Keycloak замінена тестовою
/// схемою автентифікації: «токен» задається заголовками X-Test-*. Політики, обробники ресурсів,
/// валідація, ProblemDetails і БД працюють так само, як у реальному застосунку.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("SUBTRACKER_TEST_DB")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=SubTrackerDb_Tests;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:SubTrackerDb", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Performance:Optimized", "true");

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(o =>
                {
                    o.DefaultScheme = TestAuthHandler.SchemeName;
                    o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>Перед запуском — чиста база: міграції й початкові дані застосує сам API під час старту.</summary>
    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<SubTrackerDbContext>().UseSqlServer(ConnectionString).Options;
        await using var db = new SubTrackerDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>Клієнт від імені користувача (null — анонімний запит без «токена»).</summary>
    public HttpClient ClientFor(TestUser? user)
    {
        var client = CreateClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", user.Sub);
            client.DefaultRequestHeaders.Add("X-Test-Email", user.Email);
            client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(',', user.Roles));
        }
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}

/// <summary>Тестові користувачі — ті самі e-mail, що в початкових даних, тож прив'язка до Users працює як із Keycloak.</summary>
public sealed record TestUser(string Sub, string Email, string[] Roles)
{
    public static readonly TestUser Oleksandr = new("test-oleksandr", "o.kovalenko@example.com", ["user"]);
    public static readonly TestUser Maria = new("test-maria", "m.kovalenko@example.com", ["user"]);
    public static readonly TestUser Admin = new("test-admin", "admin@subtracker.local", ["admin", "user"]);
}

/// <summary>Схема автентифікації для тестів: формує ClaimsPrincipal з тими самими claims, що й токен Keycloak.</summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var sub))
            return Task.FromResult(AuthenticateResult.NoResult()); // → 401

        var claims = new List<Claim>
        {
            new("sub", sub!),
            new("email", Request.Headers["X-Test-Email"].ToString()),
            new("preferred_username", sub!)
        };
        claims.AddRange(Request.Headers["X-Test-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => new Claim("roles", r)));

        var identity = new ClaimsIdentity(claims, SchemeName, "preferred_username", "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
