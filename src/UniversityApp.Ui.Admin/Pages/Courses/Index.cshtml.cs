using Microsoft.AspNetCore.Authorization;

namespace UniversityApp.Ui.Admin.Pages.Courses;

[Authorize]
public class IndexModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly ApiClient _api;
    public IndexModel(ApiClient api) => _api = api;

    public List<ApiClient.CourseItem> Items { get; private set; } = new();
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var (ok, err, data) = await _api.TryGetCoursesAsync(ct);
        if (!ok) Error = err;
        Items = data;
    }
}
