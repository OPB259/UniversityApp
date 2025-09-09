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

    public class InputModel
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var (ok, token, err) = await _tokens.GetTokenAsync(Input.Username, Input.Password);
        if (!ok || string.IsNullOrWhiteSpace(token))
        {
            ModelState.AddModelError(string.Empty, err ?? "Login failed.");
            return Page();
        }

        // Zaloguj cookie (prosto – tylko Name)
        var claims = new List<Claim> { new(ClaimTypes.Name, Input.Username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return RedirectToPage("/Index");
    }
}
