using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SubTracker.Api.Contracts;
using SubTracker.Domain.Enums;

namespace SubTracker.Api.Tests;

/// <summary>
/// Перевірка контракту API: коди стану (200/201/204/400/401/403/404/409/422), заголовок Location,
/// ідемпотентність PUT/DELETE, метадані пагінації, формат ProblemDetails.
/// </summary>
public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient Oleksandr => factory.ClientFor(TestUser.Oleksandr);
    private HttpClient Maria => factory.ClientFor(TestUser.Maria);
    private HttpClient Admin => factory.ClientFor(TestUser.Admin);

    [Fact]
    public async Task Request_without_token_returns_401_problem()
    {
        var response = await factory.ClientFor(null).GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_links_token_to_seeded_user_by_email()
    {
        var me = await Oleksandr.GetFromJsonAsync<MeDto>("/api/v1/me", Json);

        Assert.NotNull(me);
        Assert.Equal(1, me.Id);
        Assert.Equal("test-oleksandr", me.ExternalId);
    }

    [Fact]
    public async Task User_cannot_create_category_403_admin_can_201_then_duplicate_409()
    {
        var name = $"Ігри {Guid.NewGuid():N}"[..20];

        var forbidden = await Oleksandr.PostAsJsonAsync("/api/v1/categories", new CreateCategoryDto(name), Json);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var created = await Admin.PostAsJsonAsync("/api/v1/categories", new CreateCategoryDto(name), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        var duplicate = await Admin.PostAsJsonAsync("/api/v1/categories", new CreateCategoryDto(name), Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_subscription_returns_201_with_location_and_resource_is_readable()
    {
        var dto = await NewSubscriptionAsync();

        var response = await Oleksandr.PostAsJsonAsync("/api/v1/subscriptions", dto, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var details = await Oleksandr.GetFromJsonAsync<SubscriptionDetailsDto>(response.Headers.Location, Json);
        Assert.Equal(dto.Name, details!.Subscription.Name);
    }

    [Fact]
    public async Task Invalid_subscription_returns_400_validation_problem()
    {
        var dto = (await NewSubscriptionAsync()) with { Name = "X", Amount = -5, Currency = "uah" };

        var response = await Oleksandr.PostAsJsonAsync("/api/v1/subscriptions", dto, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Amount", out _));
        Assert.True(errors.TryGetProperty("Currency", out _));
    }

    [Fact]
    public async Task Unknown_category_violates_business_rule_422()
    {
        var dto = (await NewSubscriptionAsync()) with { CategoryId = 9999 };

        var response = await Oleksandr.PostAsJsonAsync("/api/v1/subscriptions", dto, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Missing_subscription_returns_404()
    {
        var response = await Oleksandr.GetAsync("/api/v1/subscriptions/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Other_user_gets_403_on_foreign_subscription()
    {
        var location = await CreateSubscriptionAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await Maria.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Maria.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Admin.GetAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Put_and_delete_are_idempotent()
    {
        var create = await NewSubscriptionAsync();
        var location = await CreateSubscriptionAsync(create);
        var update = new UpdateSubscriptionDto(create.Name, create.CategoryId, create.PaymentMethodId, 299m, "UAH",
            BillingPeriod.Monthly, create.NextChargeDate, null, null, 5);

        var first = await Oleksandr.PutAsJsonAsync(location, update, Json);
        var afterFirst = await Oleksandr.GetStringAsync(location);
        var second = await Oleksandr.PutAsJsonAsync(location, update, Json);
        var afterSecond = await Oleksandr.GetStringAsync(location);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(afterFirst, afterSecond); // повтор не змінив стан

        Assert.Equal(HttpStatusCode.NoContent, (await Oleksandr.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Oleksandr.DeleteAsync(location)).StatusCode);
    }

    [Fact]
    public async Task Charging_paused_subscription_is_conflict_409()
    {
        var location = await CreateSubscriptionAsync();
        var paused = await Oleksandr.PatchAsJsonAsync($"{location}/status",
            new ChangeSubscriptionStatusDto(SubscriptionStatus.Paused), Json);
        Assert.Equal(HttpStatusCode.NoContent, paused.StatusCode);

        var charge = await Oleksandr.PostAsync($"{location}/charges", null);

        Assert.Equal(HttpStatusCode.Conflict, charge.StatusCode);
    }

    [Fact]
    public async Task List_returns_pagination_metadata()
    {
        var page = await Oleksandr.GetFromJsonAsync<PagedResponse<SubscriptionDto>>(
            "/api/v1/subscriptions?page=1&pageSize=2&sortBy=Amount&sortDir=Desc", Json);

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.True(page.Items.Count <= 2);
        Assert.True(page.TotalCount >= page.Items.Count);
        Assert.Equal(page.TotalCount > 2, page.HasNext);
        Assert.True(page.Items.Count < 2 || page.Items[0].Amount >= page.Items[1].Amount);
    }

    [Fact]
    public async Task Page_size_above_limit_returns_400()
    {
        var response = await Oleksandr.GetAsync("/api/v1/subscriptions?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- допоміжні методи ----------

    private async Task<CreateSubscriptionDto> NewSubscriptionAsync()
    {
        var methods = await Oleksandr.GetFromJsonAsync<List<PaymentMethodDto>>("/api/v1/payment-methods", Json);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var method = methods!.First(m => m.ExpiryDate is null || m.ExpiryDate >= today);
        return new CreateSubscriptionDto($"Тест {Guid.NewGuid():N}"[..30], 2, method.Id, 149.99m, "UAH",
            BillingPeriod.Monthly, today.AddDays(7));
    }

    private async Task<string> CreateSubscriptionAsync(CreateSubscriptionDto? dto = null)
    {
        var response = await Oleksandr.PostAsJsonAsync("/api/v1/subscriptions", dto ?? await NewSubscriptionAsync(), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response.Headers.Location!.PathAndQuery;
    }
}
