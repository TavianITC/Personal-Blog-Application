using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Personal_Blog_Application.Models;
using Personal_Blog_Application.Services.Comments;
using Personal_Blog_Application.Services.Common;
using Personal_Blog_Application.ViewModels;

namespace Personal_Blog_Application.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly ICommentService _comments;
        private readonly UserManager<User> _userManager;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;

        public CommentsController(
            ICommentService comments,
            UserManager<User> userManager,
            ICompositeViewEngine viewEngine,
            ITempDataProvider tempDataProvider)
        {
            _comments = comments;
            _userManager = userManager;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        // POST /comments/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "NewComment")] CommentCreateViewModel model)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");

            // Surface field errors picked up by data annotations to the AJAX/JSON
            // caller before the service is even invoked.
            if (!ModelState.IsValid)
                return await HandleValidationFailureAsync(model);

            var result = await _comments.CreateAsync(model, userId, isAdmin);
            switch (result.Status)
            {
                case ResultStatus.NotFound: return NotFound();
                case ResultStatus.Forbidden: return Forbid();
                case ResultStatus.ValidationError:
                    ApplyFieldErrors(result.FieldErrors);
                    return await HandleValidationFailureAsync(model);
            }

            var created = result.Value!;
            if (IsAjax())
            {
                var html = await RenderPartialAsync("_CommentItem", created.Comment);
                return Json(new { ok = true, html, count = created.CommentCount });
            }

            TempData["Success"] = "Comment posted.";
            return RedirectToAction(
                "Detail", "Blogs",
                new { id = created.Comment.BlogId, from = model.From },
                fragment: "comments");
        }

        // POST /comments/delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole("ADMIN");

            var result = await _comments.DeleteAsync(id, userId, isAdmin);
            if (result.Status == ResultStatus.NotFound) return NotFound();
            if (result.Status == ResultStatus.Forbidden) return Forbid();

            var deleted = result.Value!;
            if (IsAjax())
                return Json(new { ok = true, count = deleted.CommentCount });

            TempData["Success"] = "Comment deleted.";
            return RedirectToAction(
                "Detail", "Blogs",
                new { id = deleted.BlogId },
                fragment: "comments");
        }

        // ── helpers ──────────────────────────────────────────────────

        private async Task<IActionResult> HandleValidationFailureAsync(CommentCreateViewModel model)
        {
            if (IsAjax())
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToArray();
                return new JsonResult(new { ok = false, errors }) { StatusCode = 400 };
            }

            var vm = await _comments.BuildDetailViewModelAsync(model.BlogId);
            if (vm == null) return NotFound();
            vm.NewComment = model;
            return View("~/Views/Blogs/Detail.cshtml", vm);
        }

        private void ApplyFieldErrors(IReadOnlyDictionary<string, string>? fieldErrors)
        {
            if (fieldErrors == null) return;
            foreach (var (field, message) in fieldErrors)
                ModelState.AddModelError($"NewComment.{field}", message);
        }

        private bool IsAjax() =>
            Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // Renders a partial view to a string so AJAX JSON responses can return
        // server-rendered HTML — same Razor template, no duplication.
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
