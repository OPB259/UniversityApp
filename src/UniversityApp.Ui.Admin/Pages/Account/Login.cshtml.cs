using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

public class LoginModel : PageModel
{
    private readonly ApiTokenService _tokens;
    public LoginModel(ApiTokenService tokens) { _tokens = tokens; }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var jwt = await _tokens.AcquireAsync(Input.Username, Input.Password);
        if (jwt is null)
        {
            Error = "B³êdny login lub has³o.";
            return Page();
        }

        var name = HttpContext.Session.GetString(SessionKeys.User) ?? Input.Username;
        var role = HttpContext.Session.GetString(SessionKeys.Role) ?? "User";

        var claims = new List<Claim> {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return RedirectToPage("/Index");
    }

    public class LoginInput
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
