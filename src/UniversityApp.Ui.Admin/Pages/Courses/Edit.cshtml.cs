using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UniversityApp.Ui.Admin.Pages.Courses;

public class EditModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public EditModel(IHttpClientFactory http) => _http = http;

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        try
        {
            var client = _http.CreateClient("WebApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var data = await client.GetFromJsonAsync<CourseDto>($"/api/courses/{id}");
            if (data == null) return RedirectToPage("Index");

            Input = new InputModel { Id = data.Id, Name = data.Name };
            return Page();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        try
        {
            var client = _http.CreateClient("WebApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PutAsJsonAsync($"/api/courses/{Input.Id}", new { name = Input.Name });
            if (!resp.IsSuccessStatusCode)
            {
                Error = $"{(int)resp.StatusCode} ({resp.ReasonPhrase})";
                return Page();
            }

            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    public record CourseDto(int Id, string Name);

    public class InputModel
    {
        [Required] public int Id { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = "";
    }
}
