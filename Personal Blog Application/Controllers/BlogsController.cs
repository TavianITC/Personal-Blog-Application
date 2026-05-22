using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Data.BlogApp.Data;
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

        // GET /blogs
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("ADMIN");

            // ADMIN can see all blogs, USER can only see their own blogs
            var query = _context.Blogs
                .Include(b => b.User)
                .AsQueryable();

            if (!isAdmin)
                query = query.Where(b => b.CreatedBy == userId);

            var blogs = await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(blogs);
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
                Content = model.Content, // HTML from TinyMCE
                Priority = model.Priority,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId!
            };

            _context.Blogs.Add(blog);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Blog post created successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}