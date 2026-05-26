using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public AuthController(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // Access denied
        [Route("/auth/access-denied")]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: /auth/login
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Blogs");
            }

            return View();
        }

        // POST: /auth/login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel loginModel)
        {
            if (!ModelState.IsValid)
            {
                return View(loginModel);
            }

            // Find the user by email
            var user = await _userManager.FindByEmailAsync(loginModel.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(loginModel);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been deactived");
                return View(loginModel);
            }

            var result = await _signInManager.PasswordSignInAsync(user, loginModel.Password, loginModel.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Blogs");
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(loginModel);
        }

        // POST: /auth/logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Auth");
        }

        // GET: /auth/register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Blogs");
            }
            return View();
        }

        // POST: /auth/register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel registerModel)
        {
            if (!ModelState.IsValid)
                return View(registerModel);

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(registerModel.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Email already exists.");
                return View(registerModel);
            }

            // Check if username already exists
            var existingUsername = await _userManager.FindByNameAsync(registerModel.Username);
            if (existingUsername != null)
            {
                ModelState.AddModelError("Username", "Username already exists.");
                return View(registerModel);
            }

            var user = new User
            {
                UserName = registerModel.Username,
                Email = registerModel.Email,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, registerModel.Password);

            if (result.Succeeded)
            {
                // Assign the "USER" role to the newly registered user
                await _userManager.AddToRoleAsync(user, "USER");
                // Use TempData to show a one-time success message on the Login page
                TempData["SuccessMessage"] = "Registration successful.";
                return RedirectToAction("Login");
            }

            // If not successful, add errors to the model state
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(registerModel);
        }
    }
}
