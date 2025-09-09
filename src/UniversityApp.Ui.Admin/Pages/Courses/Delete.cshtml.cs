using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UniversityApp.Ui.Admin.Pages.Courses;

public class DeleteModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public DeleteModel(IHttpClientFactory http) => _http = http;

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    // UWAGA: nie nazywamy tego "Course", ¿eby nie kolidowa³o
    public CourseDto? Item { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Id = id;

        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        var client = _http.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Item = await client.GetFromJsonAsync<CourseDto>($"/api/courses/{id}");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        var client = _http.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.DeleteAsync($"/api/courses/{Id}");
        if (!resp.IsSuccessStatusCode)
        {
            Error = $"{(int)resp.StatusCode} - {resp.ReasonPhrase}";
            // ¿eby strona coœ wyœwietli³a, gdy delete siê nie uda
            Item = Item ?? new CourseDto(Id, "", 0);
            return Page();
        }

        return RedirectToPage("Index");
    }

    public record CourseDto(int Id, string Title, int Credits);
}
