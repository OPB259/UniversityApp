using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniversityApp.Ui.Admin.Pages.Students;

[Authorize]
public class IndexModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly ApiClient _api;
    public IndexModel(ApiClient api) => _api = api;

    public List<ApiClient.StudentItem> Items { get; private set; } = new();
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Items = await _api.GetStudentsAsync(ct);
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToPage("/Account/Login");
        }
        catch (HttpRequestException ex)
        {
            Error = $"API error: {ex.Message}";
            return Page();
        }
    }
}
