using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Options: WebApi BaseUrl
builder.Services.Configure<WebApiOptions>(builder.Configuration.GetSection("WebApi"));

// AuthN/AuthZ (cookies)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Denied";
        o.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

builder.Services.AddRazorPages(o =>
{
    // Wymagaj autoryzacji domyœlnie
    o.Conventions.AuthorizeFolder("/");
    // Pozwól anonimom wejœæ na logowanie/wylogowanie/Denied
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/Logout");
    o.Conventions.AllowAnonymousToPage("/Account/Denied");
});

// HTTP + sesja na token
builder.Services.AddHttpClient("api", (sp, http) =>
{
    var opt = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
    http.BaseAddress = new Uri(opt.BaseUrl);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession();

builder.Services.AddScoped<ApiTokenService>();
builder.Services.AddScoped<ApiClient>();

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

// ----------------- Options/Services -----------------
public class WebApiOptions { public string BaseUrl { get; set; } = ""; }

public static class SessionKeys
{
    public const string Jwt = "jwt_token";
    public const string User = "user_name";
    public const string Role = "user_role";
}

public class ApiTokenService
{
    private readonly IHttpClientFactory _hf;
    private readonly IHttpContextAccessor _ctx;
    public ApiTokenService(IHttpClientFactory hf, IHttpContextAccessor ctx)
    {
        _hf = hf; _ctx = ctx;
    }

    public async Task<string?> AcquireAsync(string username, string password, CancellationToken ct = default)
    {
        var http = _hf.CreateClient("api");
        using var content = new StringContent(JsonSerializer.Serialize(new { username, password }), System.Text.Encoding.UTF8, "application/json");
        var resp = await http.PostAsync("/api/security/generatetoken", content, ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var s = await resp.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        var token = doc.RootElement.GetProperty("token").GetString();

        if (string.IsNullOrWhiteSpace(token)) return null;

        // rozkoduj JWT by wyci¹gn¹æ rolê i nazwê
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var name = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? username;
        var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "User";

        // zapisz do sesji
        _ctx.HttpContext!.Session.SetString(SessionKeys.Jwt, token);
        _ctx.HttpContext!.Session.SetString(SessionKeys.User, name);
        _ctx.HttpContext!.Session.SetString(SessionKeys.Role, role);

        return token;
    }

    public string? CurrentToken() => _ctx.HttpContext?.Session.GetString(SessionKeys.Jwt);
    public void Clear()
    {
        _ctx.HttpContext?.Session.Remove(SessionKeys.Jwt);
        _ctx.HttpContext?.Session.Remove(SessionKeys.User);
        _ctx.HttpContext?.Session.Remove(SessionKeys.Role);
    }
}

public class ApiClient
{
    private readonly IHttpClientFactory _hf;
    private readonly IHttpContextAccessor _ctx;
    public ApiClient(IHttpClientFactory hf, IHttpContextAccessor ctx) { _hf = hf; _ctx = ctx; }

    private HttpClient CreateAuthorized()
    {
        var http = _hf.CreateClient("api");
        var token = _ctx.HttpContext!.Session.GetString(SessionKeys.Jwt);
        if (!string.IsNullOrWhiteSpace(token))
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        return http;
    }

    // --- Students ---
    public record Student(int Id, string Name, string Email);
    public record CreateStudent(string Name, string Email);

    public async Task<List<Student>> GetStudentsAsync(CancellationToken ct = default)
    {
        var http = CreateAuthorized();
        var s = await http.GetStreamAsync("/api/students", ct);
        var data = await JsonSerializer.DeserializeAsync<List<Student>>(s, cancellationToken: ct, options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return data ?? new();
    }

    public async Task<bool> CreateStudentAsync(CreateStudent dto, CancellationToken ct = default)
    {
        var http = CreateAuthorized();
        using var content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json");
        var resp = await http.PostAsync("/api/students", content, ct);
        return resp.IsSuccessStatusCode;
    }
}
