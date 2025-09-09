using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UniversityApp.Ui.Admin.Pages.Enrollments;

public class DeleteModel : PageModel
{
    private readonly IHttpClientFactory _http;
    public DeleteModel(IHttpClientFactory http) => _http = http;

    [BindProperty] public int Id { get; set; }
    public string? Error { get; set; }

    public IActionResult OnGet(int id)
    {
        Id = id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token)) return Redirect("/Account/Login");

        try
        {
            var client = _http.CreateClient("WebApi");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.DeleteAsync($"/api/enrollments/{Id}");
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
}
