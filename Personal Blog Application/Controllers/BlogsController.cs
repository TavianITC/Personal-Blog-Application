using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Blogs;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize]
    public class BlogsController : Controller
    {
        private readonly IBlogService _blogs;
        private readonly UserManager<User> _userManager;

        public BlogsController(IBlogService blogs, UserManager<User> userManager)
        {
            _blogs = blogs;
            _userManager = userManager;
        }

        // GET /blogs?search=&author=&sort=&priority=&page=
        public async Task<IActionResult> Index(
            [FromQuery] string? search,
            [FromQuery] string? author,
            [FromQuery] string? sort,
            [FromQuery] int? priority,
            [FromQuery] int page = 1)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");

            var blogs = await _blogs.GetFeedAsync(search, author, sort, priority, userId, isAdmin, page);

            ViewBag.FilterSearch = search;
            ViewBag.FilterAuthor = author;
            ViewBag.FilterSort = sort;
            ViewBag.FilterPriority = priority;
            return View(blogs);
        }

        // GET /blogs/mine?search=&status=&sort=&priority=&page=
        public async Task<IActionResult> Mine(
            [FromQuery] string? search,
            [FromQuery] string? sort,
            [FromQuery] string? status,
            [FromQuery] int? priority,
            [FromQuery] int page = 1)
        {
            var userId = _userManager.GetUserId(User)!;
            var blogs = await _blogs.GetMineAsync(search, sort, status, priority, userId, page);
            var counts = await _blogs.GetMyStatusCountsAsync(userId);

            ViewBag.FilterSearch = search;
            ViewBag.FilterSort = sort;
            ViewBag.FilterStatus = status;
            ViewBag.FilterPriority = priority;
            ViewBag.StatusCounts = counts;
            return View(blogs);
        }

        // GET /blogs/detail/5?commentPage=
        public async Task<IActionResult> Detail(int id, int commentPage = 1)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");
            var from = Request.Query["from"].ToString();

            var result = await _blogs.GetDetailAsync(id, userId, isAdmin, from, commentPage);
            return result.Status switch
            {
                ResultStatus.NotFound => NotFound(),
                ResultStatus.Forbidden => Forbid(),
                _ => View(result.Value)
            };
        }

        // GET /blogs/create
        public IActionResult Create() => View(new BlogCreateViewModel());

        // POST /blogs/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = _userManager.GetUserId(User)!;
            var result = await _blogs.CreateAsync(model, userId);

            TempData["Success"] = result.Value!.Status switch
            {
                "DRAFT" => "Draft saved.",
                "PRIVATE" => "Blog saved as private.",
                _ => "Blog published successfully."
            };
            return RedirectToAction(nameof(Mine));
        }

        // GET /blogs/edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");

            var result = await _blogs.GetForEditAsync(id, userId, isAdmin);
            if (result.Status == ResultStatus.NotFound) return NotFound();
            if (result.Status == ResultStatus.Forbidden) return Forbid();

            ViewBag.BlogId = id;
            return View(result.Value);
        }

        // POST /blogs/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BlogCreateViewModel model, string? from)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.BlogId = id;
                return View(model);
            }

            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");

            var result = await _blogs.UpdateAsync(id, model, userId, isAdmin);
            if (result.Status == ResultStatus.NotFound) return NotFound();
            if (result.Status == ResultStatus.Forbidden) return Forbid();

            TempData["Success"] = "Blog updated successfully.";

            if (!string.IsNullOrEmpty(from))
                return RedirectToAction(nameof(Detail), new { id, from });
            return RedirectToAction(nameof(Mine));
        }

        // POST /blogs/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? from)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");

            var result = await _blogs.DeleteAsync(id, userId, isAdmin);
            if (result.Status == ResultStatus.NotFound) return NotFound();
            if (result.Status == ResultStatus.Forbidden) return Forbid();

            TempData["Success"] = "Blog deleted.";

            if (from == "index")
                return RedirectToAction(nameof(Index));
            return RedirectToAction(nameof(Mine));
        }
    }
}
