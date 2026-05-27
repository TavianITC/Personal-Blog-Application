using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Personal_Blog_Application.Services.Auth;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        // Access denied
        [Route("/auth/access-denied")]
        public IActionResult AccessDenied() => View();

        // GET: /auth/login
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity!.IsAuthenticated)
                return RedirectToAction("Index", "Home");
            return View();
        }

        // POST: /auth/login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel loginModel)
        {
            if (!ModelState.IsValid)
                return View(loginModel);

            var result = await _auth.LoginAsync(loginModel);
            if (result.Success)
                return RedirectToAction("Index", "Home");

            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err);
            return View(loginModel);
        }

        // POST: /auth/logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _auth.LogoutAsync();
            return RedirectToAction("Index", "Home");
        }

        // GET: /auth/register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity!.IsAuthenticated)
                return RedirectToAction("Index", "Blogs");
            return View();
        }

        // POST: /auth/register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel registerModel)
        {
            if (!ModelState.IsValid)
                return View(registerModel);

            var result = await _auth.RegisterAsync(registerModel);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Registration successful.";
                return RedirectToAction("Login");
            }

            if (result.FieldErrors != null)
            {
                foreach (var (field, message) in result.FieldErrors)
                    ModelState.AddModelError(field, message);
            }
            foreach (var err in result.Errors)
                ModelState.AddModelError(string.Empty, err);

            return View(registerModel);
        }
    }
}
