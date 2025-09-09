using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UniversityApp.Ui.Admin.Pages.Enrollments;

public class EditModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public EditModel(IHttpClientFactory http) => _http = http;

    [BindProperty] public InputModel Input { get; set; } = new();
    public List<StudentDto> Students { get; set; } = new();
    public List<CourseDto> Courses { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        var client = _http.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Lookup lists
        Students = await client.GetFromJsonAsync<List<StudentDto>>("/api/students") ?? new();
        Courses = await client.GetFromJsonAsync<List<CourseDto>>("/api/courses") ?? new();

        // Current enrollment
        var e = await client.GetFromJsonAsync<EnrollmentDto>($"/api/enrollments/{id}");
        if (e is null) return RedirectToPage("Index");

        Input.Id = e.Id;
        Input.StudentId = e.StudentId;
        Input.CourseId = e.CourseId;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await ReloadLookupsAsync();
            return Page();
        }

        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        var client = _http.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            StudentId = (int?)Input.StudentId,
            CourseId = (int?)Input.CourseId
        };

        var resp = await client.PutAsJsonAsync($"/api/enrollments/{Input.Id}", payload);

        if (!resp.IsSuccessStatusCode)
        {
            Error = $"{(int)resp.StatusCode} - {resp.ReasonPhrase}";
            await ReloadLookupsAsync();
            return Page();
        }

        return RedirectToPage("Index");
    }

    private async Task ReloadLookupsAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return;

        var client = _http.CreateClient("WebApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Students = await client.GetFromJsonAsync<List<StudentDto>>("/api/students") ?? new();
        Courses = await client.GetFromJsonAsync<List<CourseDto>>("/api/courses") ?? new();
    }

    public class InputModel
    {
        [Required] public int Id { get; set; }
        [Required][Display(Name = "Student")] public int StudentId { get; set; }
        [Required][Display(Name = "Course")] public int CourseId { get; set; }
    }

    public record StudentDto(int Id, string Name, string Email);
    public record CourseDto(int Id, string Title, int Credits);

    public record EnrollmentDto(
        int Id, int StudentId, string StudentName,
        int CourseId, string CourseTitle, DateTime EnrolledAt);
}
