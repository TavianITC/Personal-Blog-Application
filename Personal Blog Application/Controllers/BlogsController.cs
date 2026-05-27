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

        // GET /blogs?title=&author=&sort=&page=
        public async Task<IActionResult> Index(string? title, string? author, string? sort, int page = 1)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");

            var blogs = await _blogs.GetFeedAsync(title, author, sort, userId, isAdmin, page);

            ViewBag.FilterTitle = title;
            ViewBag.FilterAuthor = author;
            ViewBag.FilterSort = sort;
            return View(blogs);
        }

        // GET /blogs/mine?title=&status=&sort=&page=
        public async Task<IActionResult> Mine(string? title, string? sort, string? status, int page = 1)
        {
            var userId = _userManager.GetUserId(User)!;
            var blogs = await _blogs.GetMineAsync(title, sort, status, userId, page);
            var counts = await _blogs.GetMyStatusCountsAsync(userId);

            ViewBag.FilterTitle = title;
            ViewBag.FilterSort = sort;
            ViewBag.FilterStatus = status;
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
