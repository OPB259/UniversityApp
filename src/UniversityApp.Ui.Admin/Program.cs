using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;   // <= DODANE

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages
builder.Services.AddRazorPages();

// Session + HttpContext
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

// Cookie auth
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Denied";
    });

builder.Services.AddAuthorization();

// HttpClient do Web API
builder.Services.AddHttpClient("WebApi", http =>
{
    var baseUrl = builder.Configuration["WebApi:BaseUrl"] ?? "http://localhost:5169";
    http.BaseAddress = new Uri(baseUrl);
});

// Serwis do pobierania tokenu
builder.Services.AddScoped<ApiTokenService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();


// ====== Serwis tokenu ======

public sealed class ApiTokenService
{
    private readonly IHttpClientFactory _http;
    private readonly IHttpContextAccessor _ctx;

    public ApiTokenService(IHttpClientFactory http, IHttpContextAccessor ctx)
    {
        _http = http;
        _ctx = ctx;
    }

    public async Task<(bool ok, string? token, string? error)> GetTokenAsync(
        string username, string password, CancellationToken ct = default)
    {
        var client = _http.CreateClient("WebApi");

        var payload = new { username, password };
        using var resp = await client.PostAsJsonAsync("api/security/generatetoken", payload, ct);

        if (!resp.IsSuccessStatusCode)
            return (false, null, $"API auth error: {(int)resp.StatusCode} {resp.ReasonPhrase}");

        // API zwraca JSON: { "token": "..." }
        var json = await resp.Content.ReadFromJsonAsync<TokenDto>(cancellationToken: ct);
        var token = json?.Token;

        if (string.IsNullOrWhiteSpace(token))
            return (false, null, "Brak tokenu w odpowiedzi API.");

        _ctx.HttpContext!.Session.SetString("AccessToken", token);
        return (true, token, null);
    }

    public static string? ReadTokenFromSession(HttpContext httpContext)
        => httpContext.Session.GetString("AccessToken");

    private sealed record TokenDto
    {
        // Mamy TYLKO jedn¹ w³aœciwoœæ – zmapowan¹ na "token" z API
        [JsonPropertyName("token")]
        public string? Token { get; init; }
    }
}
