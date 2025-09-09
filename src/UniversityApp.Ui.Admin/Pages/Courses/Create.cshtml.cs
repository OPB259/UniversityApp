using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UniversityApp.Ui.Admin.Pages.Courses;

public class CreateModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public CreateModel(IHttpClientFactory http) => _http = http;

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public void OnGet() { }

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

            var resp = await client.PostAsJsonAsync("/api/courses", new { name = Input.Name });
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

    public class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = "";
    }
}
