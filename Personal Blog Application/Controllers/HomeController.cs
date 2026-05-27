using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Blogs;
using Personal_Blog_Application.ViewModels;
using System.Diagnostics;

namespace Personal_Blog_Application.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IBlogService _blogs;
        private readonly UserManager<User> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            IBlogService blogs,
            UserManager<User> userManager)
        {
            _logger = logger;
            _blogs = blogs;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new HomeIndexViewModel();

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User)!;
                vm.Feed = (await _blogs.GetHomeFeedAsync(userId)).ToList();
            }

            return View(vm);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
