using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages + autoryzacje folderów
builder.Services
    .AddRazorPages()
    .AddRazorPagesOptions(o =>
    {
        o.Conventions.AuthorizeFolder("/Students");
        o.Conventions.AuthorizeFolder("/Courses");
        o.Conventions.AuthorizeFolder("/Enrollments");
        o.Conventions.AllowAnonymousToFolder("/Account");
        o.Conventions.AllowAnonymousToPage("/Index");
        o.Conventions.AllowAnonymousToPage("/Privacy");
    });

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

// Cookie auth
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "ui.auth";
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Denied";
        o.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// ===== WebAPI HTTP client + handler dopinaj¹cy token z sesji =====
var baseUrl = builder.Configuration["WebApi:BaseUrl"] ?? "http://localhost:5169";
builder.Services.AddTransient<AuthMessageHandler>();
builder.Services.AddHttpClient<ApiClient>(http =>
{
    http.BaseAddress = new Uri(baseUrl);
})
.AddHttpMessageHandler<AuthMessageHandler>();

// ===== token service (do logowania) =====
builder.Services.AddHttpClient<ApiTokenService>(http =>
{
    http.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();            // sesja przed auth
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();


// ================== Services (w tym pliku dla prostoty) ==================

public sealed class AuthMessageHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _ctx;

    public AuthMessageHandler(IHttpContextAccessor ctx) => _ctx = ctx;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _ctx.HttpContext?.Session.GetString("AccessToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class ApiTokenService
{
    private readonly HttpClient _http;
    public ApiTokenService(HttpClient http) => _http = http;

    private sealed class TokenDto
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    public async Task<string?> GetTokenAsync(string username, string password, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync("/api/security/generatetoken",
            new { username, password }, ct);

        if (!resp.IsSuccessStatusCode) return null;

        var dto = await resp.Content.ReadFromJsonAsync<TokenDto>(cancellationToken: ct);
        return dto?.Token;
    }
}

public sealed class ApiClient
{
    private readonly HttpClient _http;
    public ApiClient(HttpClient http) => _http = http;

    // --- Students ---
    public async Task<List<StudentItem>> GetStudentsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/students", ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new();

        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException();

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<StudentItem>>(cancellationToken: ct) ?? new();
    }

    public sealed record StudentItem(Guid Id, string Name);

    // --- Courses ---
    public async Task<(bool ok, string? err, List<CourseItem> data)> TryGetCoursesAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/courses", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return (false, "Endpoint /api/courses nie istnieje (404).", new());

        if (!resp.IsSuccessStatusCode)
            return (false, $"API error: {(int)resp.StatusCode} {resp.ReasonPhrase}", new());

        var list = await resp.Content.ReadFromJsonAsync<List<CourseItem>>(cancellationToken: ct) ?? new();
        return (true, null, list);
    }

    public sealed record CourseItem(Guid Id, string Title);

    // --- Enrollments ---
    public async Task<(bool ok, string? err, List<EnrollmentItem> data)> TryGetEnrollmentsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/enrollments", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return (false, "Endpoint /api/enrollments nie istnieje (404).", new());

        if (!resp.IsSuccessStatusCode)
            return (false, $"API error: {(int)resp.StatusCode} {resp.ReasonPhrase}", new());

        var list = await resp.Content.ReadFromJsonAsync<List<EnrollmentItem>>(cancellationToken: ct) ?? new();
        return (true, null, list);
    }

    public sealed record EnrollmentItem(Guid Id, Guid StudentId, Guid CourseId);
}
