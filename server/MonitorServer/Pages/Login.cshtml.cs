using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MonitorServer.Data;
using System.Security.Claims;

namespace MonitorServer.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    public LoginModel(AppDbContext db) => _db = db;

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        var user = _db.AdminUsers.FirstOrDefault(u => u.Username == Username);
        if (user == null || !BCryptLite.Verify(Password, user.PasswordHash))
        {
            Error = "用户名或密码错误";
            return Page();
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, user.Username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        _db.AuditLogs.Add(new MonitorServer.Models.AuditLog { Username = user.Username, Action = "登录", Time = DateTime.Now });
        await _db.SaveChangesAsync();

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }
}
