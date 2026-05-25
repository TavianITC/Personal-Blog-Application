using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize]
    public class BlogsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public BlogsController(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET /blogs?title=&author=&sort=
        // Posts feed. Regular users see the community (PUBLISHED only);
        // ADMIN sees every post regardless of status (moderation view).
        public async Task<IActionResult> Index(string? title, string? author, string? sort)
        {
            var isAdmin = User.IsInRole("ADMIN");

            IQueryable<Blog> query = _context.Blogs.Include(b => b.User);

            if (!isAdmin)
                query = query.Where(b => b.Status == "PUBLISHED");

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(b => b.Title.Contains(title));

            if (!string.IsNullOrWhiteSpace(author))
                query = query.Where(b => b.User.UserName!.Contains(author));

            query = ApplySort(query, sort);

            ViewBag.FilterTitle = title;
            ViewBag.FilterAuthor = author;
            ViewBag.FilterSort = sort;

            return View(await query.ToListAsync());
        }

        // GET /blogs/mine?title=&author=&sort=
        // Always scoped to the current user's own posts — ADMIN included.
        public async Task<IActionResult> Mine(string? title, string? author, string? sort)
        {
            var userId = _userManager.GetUserId(User);

            IQueryable<Blog> query = _context.Blogs
                .Include(b => b.User)
                .Where(b => b.CreatedBy == userId);

            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(b => b.Title.Contains(title));

            if (!string.IsNullOrWhiteSpace(author))
                query = query.Where(b => b.User.UserName!.Contains(author));

            query = ApplySort(query, sort);

            ViewBag.FilterTitle = title;
            ViewBag.FilterAuthor = author;
            ViewBag.FilterSort = sort;

            return View(await query.ToListAsync());
        }

        private static IQueryable<Blog> ApplySort(IQueryable<Blog> query, string? sort) => sort switch
        {
            "priority_asc" => query.OrderBy(b => b.Priority).ThenByDescending(b => b.CreatedAt),
            "priority_desc" or "priority" =>
                query.OrderByDescending(b => b.Priority).ThenByDescending(b => b.CreatedAt),
            _ => query.OrderByDescending(b => b.CreatedAt)
        };

        // GET /blogs/detail/5
        public async Task<IActionResult> Detail(int id)
        {
            var blog = await _context.Blogs
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (blog == null) return NotFound();

            // PRIVATE / DRAFT posts are only visible to their owner (or ADMIN)
            if (blog.Status != "PUBLISHED" && !CanModify(blog))
                return Forbid();

            return View(blog);
        }

        // GET /blogs/create
        public IActionResult Create()
        {
            return View(new BlogCreateViewModel());
        }

        // POST /blogs/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = _userManager.GetUserId(User);

            var blog = new Blog
            {
                Title = model.Title,
                Content = model.Content, // HTML from rich editor
                Priority = model.Priority,
                Status = model.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId!
            };

            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            TempData["Success"] = model.Status switch
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
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return NotFound();
            if (!CanModify(blog)) return Forbid();

            var vm = new BlogCreateViewModel
            {
                Title = blog.Title,
                Content = blog.Content,
                Priority = blog.Priority,
                Status = blog.Status
            };
            ViewBag.BlogId = id;
            return View(vm);
        }

        // POST /blogs/edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BlogCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.BlogId = id;
                return View(model);
            }

            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return NotFound();
            if (!CanModify(blog)) return Forbid();

            blog.Title = model.Title;
            blog.Content = model.Content;
            blog.Priority = model.Priority;
            blog.Status = model.Status;
            blog.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Blog updated successfully.";
            return RedirectToAction(nameof(Mine));
        }

        // POST /blogs/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null) return NotFound();
            if (!CanModify(blog)) return Forbid();

            _context.Blogs.Remove(blog);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Blog deleted.";
            return RedirectToAction(nameof(Mine));
        }

        private bool CanModify(Blog blog)
        {
            if (User.IsInRole("ADMIN")) return true;
            return blog.CreatedBy == _userManager.GetUserId(User);
        }
    }
}
