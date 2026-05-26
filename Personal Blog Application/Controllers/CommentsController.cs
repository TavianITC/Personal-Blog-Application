using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Personal_Blog_Application.Data;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public CommentsController(
            AppDbContext context,
            UserManager<User> userManager,
            ICompositeViewEngine viewEngine,
            ITempDataProvider tempDataProvider)
        {
            _context = context;
            _userManager = userManager;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        // POST /comments/create
        // The form lives inside BlogDetailViewModel, so fields arrive as
        // NewComment.X — Bind prefix strips the wrapper when binding.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "NewComment")] CommentCreateViewModel model)
        {
            var blog = await _context.Blogs.FindAsync(model.BlogId);
            if (blog == null) return NotFound();

            if (blog.Status != "PUBLISHED" && !CanModerate(blog))
                return Forbid();

            model.Content = (model.Content ?? string.Empty).Trim();

            if (!ModelState.IsValid)
            {
                if (IsAjax())
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToArray();
                    return new JsonResult(new { ok = false, errors }) { StatusCode = 400 };
                }

                var vm = await BuildDetailViewModelAsync(blog.Id);
                if (vm == null) return NotFound();
                vm.NewComment = model;
                return View("~/Views/Blogs/Detail.cshtml", vm);
            }

            var comment = new Comment
            {
                BlogId = blog.Id,
                Content = model.Content,
                CreatedBy = _userManager.GetUserId(User)!,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            // Re-fetch with the User nav property so the partial can render the author
            // Because when create Comment, only CreatedBy is set, User is not populated until we query it again.
            // The Blog is not needed because we don't need Blog's data in the partial
            var saved = await _context.Comments
                .Include(c => c.User)
                .FirstAsync(c => c.Id == comment.Id);

            if (IsAjax())
            {
                var html = await RenderPartialAsync("_CommentItem", saved);
                var count = await _context.Comments.CountAsync(c => c.BlogId == blog.Id);
                return Json(new { ok = true, html, count });
            }

            TempData["Success"] = "Comment posted.";
            return RedirectToAction(
                "Detail", "Blogs",
                new { id = blog.Id, from = model.From },
                fragment: "comments");
        }

        // POST /comments/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null) return NotFound();

            if (!CanDelete(comment)) return Forbid();

            var blogId = comment.BlogId;
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            if (IsAjax())
            {
                var count = await _context.Comments.CountAsync(c => c.BlogId == blogId);
                return Json(new { ok = true, count });
            }

            TempData["Success"] = "Comment deleted.";
            return RedirectToAction(
                "Detail", "Blogs",
                new { id = blogId },
                fragment: "comments");
        }

        // ── helpers ──────────────────────────────────────────────────

        // Anyone signed in can comment on a PUBLISHED post; for non-PUBLISHED
        // (DRAFT / PRIVATE) only the owner or ADMIN may comment.
        private bool CanModerate(Blog blog)
        {
            if (User.IsInRole("ADMIN")) return true;
            return blog.CreatedBy == _userManager.GetUserId(User);
        }

        // A comment can be deleted by its author OR an ADMIN.
        private bool CanDelete(Comment comment)
        {
            if (User.IsInRole("ADMIN")) return true;
            return comment.CreatedBy == _userManager.GetUserId(User);
        }

        private bool IsAjax() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        private async Task<BlogDetailViewModel?> BuildDetailViewModelAsync(int blogId)
        {
            var blog = await _context.Blogs
                .Include(b => b.User)
                .Include(b => b.Comments).ThenInclude(c => c.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(b => b.Id == blogId);

            if (blog == null) return null;

            return new BlogDetailViewModel
            {
                Blog = blog,
                Comments = blog.Comments.OrderByDescending(c => c.CreatedAt).ToList(),
                NewComment = new CommentCreateViewModel { BlogId = blogId }
            };
        }

        // Renders a partial view to a string. Used to return rendered HTML
        // in AJAX JSON responses so the same Razor template is the single
        // source of truth for both server-rendered and AJAX paths.
        private async Task<string> RenderPartialAsync<TModel>(string viewName, TModel model)
        {
            var viewResult = _viewEngine.FindView(ControllerContext, viewName, isMainPage: false);
            if (!viewResult.Success)
                throw new InvalidOperationException(
                    $"Partial view '{viewName}' not found. Searched: {string.Join(", ", viewResult.SearchedLocations)}");

            var viewData = new ViewDataDictionary<TModel>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            { Model = model };

            var tempData = new TempDataDictionary(HttpContext, _tempDataProvider);

            await using var sw = new StringWriter();
            var viewContext = new ViewContext(
                ControllerContext,
                viewResult.View,
                viewData,
                tempData,
                sw,
                new HtmlHelperOptions());
            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
        }
    }
}
