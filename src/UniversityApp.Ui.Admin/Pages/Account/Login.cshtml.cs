using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace UniversityApp.Ui.Admin.Pages.Account;

public class LoginModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
    private readonly ApiTokenService _tokens;

    public LoginModel(ApiTokenService tokens) => _tokens = tokens;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required] public string Username { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var token = await _tokens.GetTokenAsync(Input.Username, Input.Password, ct);
        if (string.IsNullOrWhiteSpace(token))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return Page();
        }

        // 1) zapisz token do sesji
        HttpContext.Session.SetString("AccessToken", token);

        // 2) cookie auth
        var claims = new[] { new Claim(ClaimTypes.Name, Input.Username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        // 3) na Students
        return RedirectToPage("/Students/Index");
    }
}
