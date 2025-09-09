using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UniversityApp.Ui.Admin.Pages.Students;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IHttpContextAccessor _ctx;

    public IndexModel(IHttpClientFactory httpFactory, IHttpContextAccessor ctx)
    {
        _httpFactory = httpFactory;
        _ctx = ctx;
    }

    public List<StudentListItem> Students { get; private set; } = new();
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // token z sesji (zapisany podczas logowania)
        var token = _ctx.HttpContext!.Session.GetString("AccessToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            // brak tokenu -> wracamy na login
            return RedirectToPage("/Account/Login", new { returnUrl = "/Students" });
        }

        var client = _httpFactory.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var resp = await client.GetAsync("api/students", HttpContext.RequestAborted);
        if (!resp.IsSuccessStatusCode)
        {
            Error = $"{(int)resp.StatusCode} {resp.ReasonPhrase}";
            return Page();
        }

        // Bezpieczna, odporna deserializacja (name albo firstName/lastName, id jako string)
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: HttpContext.RequestAborted);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                string id = el.TryGetProperty("id", out var idEl) ? idEl.ToString() : "";
                string name =
                    el.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                        ? nameEl.GetString() ?? ""
                        : $"{(el.TryGetProperty("firstName", out var fn) ? fn.GetString() : "")} {(el.TryGetProperty("lastName", out var ln) ? ln.GetString() : "")}".Trim();

                if (!string.IsNullOrWhiteSpace(name))
                    Students.Add(new StudentListItem(id, name));
            }
        }

        return Page();
    }

    public record StudentListItem(string Id, string Name);
}
