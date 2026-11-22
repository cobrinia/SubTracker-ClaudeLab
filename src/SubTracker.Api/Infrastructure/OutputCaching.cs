using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using SubTracker.Api.Services;

namespace SubTracker.Api.Infrastructure;

/// <summary>Теги кешу відповідей: після зміни даних сервіси видаляють записи кешу за тегом.</summary>
public static class CacheTags
{
    public const string Categories = "categories";
    public const string Subscriptions = "subscriptions";
}

/// <summary>
/// Політика OutputCache для захищених кінцевих точок. Стандартна політика НЕ кешує запити із заголовком
/// Authorization, тому тут кешування дозволено явно, але ключ кешу залежить від користувача (claim «sub»):
/// один користувач ніколи не отримає закешовану відповідь іншого. Кешування вмикається лише в оптимізованому
/// режимі (Performance:Optimized), щоб чесно порівняти навантажувальні тести «до» і «після».
/// </summary>
public sealed class PerUserOutputCachePolicy(string tag, TimeSpan duration) : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        var http = context.HttpContext;
        var optimized = http.RequestServices.GetRequiredService<IOptions<PerformanceOptions>>().Value.Optimized;
        var cacheable = optimized
                        && HttpMethods.IsGet(http.Request.Method)
                        && http.User.Identity?.IsAuthenticated == true;

        context.EnableOutputCaching = cacheable;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = duration;
        context.CacheVaryByRules.QueryKeys = "*";
        context.CacheVaryByRules.VaryByValues["user"] = http.User.FindFirst("sub")?.Value ?? "anonymous";
        context.Tags.Add(tag);
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        // Кешуємо лише успішні відповіді.
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK)
            context.AllowCacheStorage = false;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Іменовані політики кешу, що використовуються в атрибуті [OutputCache(PolicyName = …)].</summary>
public static class CachePolicies
{
    public const string Categories = "categories";
    public const string Subscriptions = "subscriptions";

    public static void Configure(OutputCacheOptions options)
    {
        options.AddPolicy(Categories, new PerUserOutputCachePolicy(CacheTags.Categories, TimeSpan.FromSeconds(60)));
        options.AddPolicy(Subscriptions, new PerUserOutputCachePolicy(CacheTags.Subscriptions, TimeSpan.FromSeconds(15)));
    }
}
