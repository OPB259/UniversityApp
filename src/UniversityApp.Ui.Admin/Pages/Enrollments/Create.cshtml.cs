using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UniversityApp.Ui.Admin.Pages.Enrollments;

public class CreateModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public CreateModel(IHttpClientFactory http) => _http = http;

    [BindProperty] public InputModel Input { get; set; } = new();
    public List<StudentDto> Students { get; set; } = new();
    public List<CourseDto> Courses { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var ok = await LoadLookupsAsync();
        if (!ok) return Redirect("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return Page();
        }

        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        var client = _http.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new { StudentId = Input.StudentId, CourseId = Input.CourseId };
        var resp = await client.PostAsJsonAsync("/api/enrollments", payload);

        if (!resp.IsSuccessStatusCode)
        {
            Error = $"{(int)resp.StatusCode} - {resp.ReasonPhrase}";
            await LoadLookupsAsync();
            return Page();
        }

        return RedirectToPage("Index");
    }

    private async Task<bool> LoadLookupsAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return false;

        var client = _http.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Students = await client.GetFromJsonAsync<List<StudentDto>>("/api/students") ?? new();
        Courses = await client.GetFromJsonAsync<List<CourseDto>>("/api/courses") ?? new();
        return true;
    }

    public class InputModel
    {
        [Required][Display(Name = "Student")] public int StudentId { get; set; }
        [Required][Display(Name = "Course")] public int CourseId { get; set; }
    }

    public record StudentDto(int Id, string Name, string Email);
    public record CourseDto(int Id, string Title, int Credits);
}
