using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.ViewModels;
using System.Diagnostics;

namespace Personal_Blog_Application.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            AppDbContext context,
            UserManager<User> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new HomeIndexViewModel();

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                vm.Feed = await _context.Blogs
                    .Include(b => b.User)
                    .Include(b => b.Comments)
                    .AsSplitQuery()
                    .Where(b => b.Status == "PUBLISHED" && b.CreatedBy != userId)
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(20)
                    .ToListAsync();
            }

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
