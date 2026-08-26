using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IApiService _apiService;

    public AccountController(IApiService apiService)
    {
        _apiService = apiService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Chat");
        }
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Get IP and User-Agent
        model.DireccionIP = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        model.Navegador = Request.Headers["User-Agent"].ToString();

        var response = await _apiService.LoginAsync(model);

        if (!response.Exitoso || response.Usuario == null || !response.IdSesion.HasValue)
        {
            ModelState.AddModelError(string.Empty, response.Error ?? "Usuario o contraseña incorrectos.");
            return View(model);
        }

        // Create claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, response.Usuario.IdUsuario.ToString()),
            new Claim(ClaimTypes.Name, response.Usuario.UsuarioNombre),
            new Claim(ClaimTypes.Email, response.Usuario.Correo),
            new Claim("FullName", $"{response.Usuario.Nombres} {response.Usuario.Apellidos}"),
            new Claim("SessionId", response.IdSesion.Value.ToString())
        };

        if (!string.IsNullOrEmpty(response.Token))
        {
            claims.Add(new Claim("JwtToken", response.Token));
        }

        // Add role claims
        foreach (var rol in response.Usuario.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, rol));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Chat");
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var sessionIdClaim = User.FindFirst("SessionId")?.Value;
            if (int.TryParse(sessionIdClaim, out var sessionId))
            {
                await _apiService.LogoutAsync(sessionId);
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult DebugClaims()
    {
        var claims = User.Claims.Select(c => new { Type = c.Type, Value = c.Value });
        return Json(claims);
    }
}
