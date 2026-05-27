# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core 8.0 MVC personal blog app. SQL Server + EF Core (Code-First) + ASP.NET Identity with cookie auth. Razor views styled with **Tailwind CSS** (compiled to `wwwroot/css/output.css`), Quill rich-text editor (CDN) for blog content.

The solution file `Personal Blog Application.slnx` lives at the repo root; the actual project lives in the nested `Personal Blog Application/` directory (note the space — quote it on the command line).

## Common commands

Run from inside the project directory (`Personal Blog Application/`):

```powershell
npm install                             # first-time setup — required, see Tailwind note below
dotnet restore
dotnet build
dotnet run                              # serves http://localhost:5000, https://localhost:7000
dotnet ef migrations add <Name>
dotnet ef database update               # apply EF migrations to BlogDb
dotnet ef migrations remove
npm run css:watch                       # rebuild Tailwind on save during development
```

There is no test project.

**Tailwind build is wired into MSBuild.** `Personal Blog Application.csproj` defines a `<Target Name="Tailwind" BeforeTargets="Build">` that shells out to `npm run css:build` (input `wwwroot/css/site.css` → output `wwwroot/css/output.css`). If `node_modules/` is missing the build will fail, so `npm install` must be run at least once. Editing `tailwind.config.js`, `postcss.config.js`, or `wwwroot/css/site.css` triggers a CSS rebuild via the `UpToDateCheckBuilt` items in the csproj.

## Database setup

`appsettings.json` ships with a Trusted_Connection string to `Server=localhost;Database=BlogDb`. Override it for SQL auth as shown in `README.md`. The DB schema is owned by EF migrations under `Migrations/` — never edit the schema by hand; add a new migration.

On every app startup `SeedData.InitializeAsync` runs (called from `Program.cs`) and:
- creates the `ADMIN` and `USER` roles if missing (role names are **uppercase** — `User.IsInRole("ADMIN")` is case-sensitive in checks throughout the code),
- creates an admin account `admin@blogapp.com` / `Admin@123` if missing,
- seeds 3 sample blog posts if the `Blogs` table is empty.

## Architecture

**Auth & authorization.** Cookie auth is configured in `Program.cs` with custom paths: `/auth/login`, `/auth/logout`, `/auth/access-denied`. Two named policies exist — `AdminOnly` (ADMIN) and `UserOnly` (USER or ADMIN) — but most controllers currently use `[Authorize]` or `[Authorize(Roles = "ADMIN")]` directly. `AuthController` is `[AllowAnonymous]`. Newly-registered users are auto-assigned the `USER` role in `AuthController.Register`. `AuthController.Login` rejects users with `IsActive == false` before calling `PasswordSignInAsync`.

**Data layer.** `AppDbContext : IdentityDbContext<User>` lives in `Personal_Blog_Application.Data` — a single `using Personal_Blog_Application.Data;` covers both `AppDbContext` and `SeedData`.

Domain model:
- `User : IdentityUser` adds `AvatarUrl`, `IsActive` (admins can deactivate accounts), and navigation collections for `Blogs` and `Comments`.
- `Blog` has a `Status` field with three values: `"DRAFT"`, `"PUBLISHED"`, `"PRIVATE"` (plain strings, not an enum). Status drives visibility — see below. Also carries `Priority` (1–5) which influences sort order.
- `Comment` belongs to a `Blog` and a `User` via `string CreatedBy`.
- Cascade rules in `OnModelCreating`: `User→Blogs` cascades, `Blog→Comments` cascades, but **`User→Comments` uses `NoAction`** to avoid multiple-cascade-path errors on SQL Server. Preserve this when adding relationships. When deleting a user, manually `RemoveRange` their comments first (see `UsersController.Delete`) before letting cascade handle their blogs.

**Per-status / per-role blog visibility.** Visibility filters live in the controller, not as a global query filter:
- `BlogsController.Index` is the *community feed*: non-admins see `Status == "PUBLISHED"` only; ADMIN sees every post (moderation view).
- `BlogsController.Mine` is always scoped to the current user's own posts — admins included.
- `BlogsController.Detail` returns `Forbid()` if the post is not PUBLISHED and the viewer isn't the owner or ADMIN.
- `CommentsController.Create` blocks comments on non-PUBLISHED posts unless the caller is the owner or ADMIN.

Apply this same "owner-or-admin" pattern (`CanModify` / `CanDelete` private helpers in each controller) when you add new mutation endpoints.

**Comments: AJAX + same-template-everywhere.** The comment box on `Views/Blogs/Detail.cshtml` (rendered via `Views/Shared/_CommentSection.cshtml`) posts via `fetch` with `X-Requested-With: XMLHttpRequest`. `CommentsController.Create`/`Delete` detect the AJAX header and return JSON. For create, the server re-renders the `_CommentItem` partial to a string via `_viewEngine.FindView(...) + RenderAsync` (see `RenderPartialAsync<T>`) and ships the HTML in the JSON payload — so the same Razor template is the single source of truth for both the server-rendered list and AJAX inserts. Don't duplicate the markup in JS. The form lives inside `BlogDetailViewModel`, so the action uses `[Bind(Prefix = "NewComment")]` to strip the wrapper on bind.

**Avatar / navbar plumbing.** `ProfileController` is thin and delegates to `IProfileService` (`Services/Profile/`). The service validates `IFormFile` (extension ∈ {.jpg/.jpeg/.png/.gif}, content type ∈ {image/jpeg, image/png, image/gif}, ≤ 2 MB), re-encodes the input through ImageSharp into a center-cropped 200×200 JPEG @ Q90, and writes `wwwroot/avatars/{guid}.jpg` regardless of input format. `User.AvatarUrl` stores the URL (`/avatars/{name}`). The previous file is best-effort deleted on update so the folder doesn't grow.

Because the navbar shows the avatar on every page, a global async action filter — `Filters/PopulateUserNavigationFilter` registered in `Program.cs` — loads the signed-in user once per request and writes `ViewBag.AvatarUrl`. `_Layout.cshtml` reads `ViewBag.AvatarUrl` (falling back to initials). If you ever optimize this, the obvious upgrade is to bake `AvatarUrl` into a cookie claim via `IUserClaimsPrincipalFactory<User>` and refresh on avatar change.

**Per-role data visibility.** `BlogService.GetFeedAsync` filters by role — `ADMIN` sees every post, everyone else sees only `b.Status == "PUBLISHED"`. `GetMineAsync` is always scoped to the caller's posts. Apply the same `(userId, isAdmin)` parameter pattern for any new list endpoint.

**Service layer.** Business logic + data access live in `Services/<Feature>/` (one folder per feature, each with `IFooService.cs` + `FooService.cs`). Controllers are thin: bind input, call service, map result to `IActionResult`. Services never touch `HttpContext`, `ModelState`, or `IActionResult` — they receive `userId` / `isAdmin` (or `currentUserId`) as parameters and return `OperationResult` / `OperationResult<T>` from `Services/Common`. The result carries a `ResultStatus` (`Success`, `ValidationError`, `NotFound`, `Forbidden`, `Conflict`) plus optional `Errors` list or `FieldErrors` dictionary; the controller maps these to `NotFound()` / `Forbid()` / `ModelState.AddModelError`. Things that must stay in the controller: TempData/ViewBag, AJAX detection (`X-Requested-With` header), `RenderPartialAsync` (uses `ICompositeViewEngine` + `ControllerContext`), and `SignInManager.RefreshSignInAsync` after username changes. Register every new service in `Program.cs` with `AddScoped`.

**Pagination.** List endpoints use `X.PagedList.Mvc.Core` + `X.PagedList.EF` (`ToPagedListAsync` on `IQueryable<T>` runs `Skip/Take + Count` at the DB; `UserAdminService.ListAsync` runs Skip/Take manually + wraps in `StaticPagedList` since it needs to project to a VM after fetching). Services return `IPagedList<T>` directly — controllers don't repaginate. Default page size is `PaginationDefaults.PageSize` (10) in `Services/Common/` — never reach across feature folders for it (don't `using Personal_Blog_Application.Services.Blogs;` just to read a page-size constant). Services clamp `page <= 0` to 1 so query strings can be passed through. In views, render with `@Html.PagedListPager(model, pageBuilder, options)` where `pageBuilder` is a `Func<int, string>` that **re-uses the current filter/sort query params** so pagination plays nicely with filtering. Applying or changing a filter resets to page 1 (handled in the JS filter-form submit handler). Read `Model.TotalItemCount` for the "X posts" header, not `Model.Count` (which is the current page's items). `BlogDetailViewModel.Comments` is `IPagedList<Comment>`; AJAX comment create still prepends to the live list and the count updates from the server-returned `count`. The `Mine` view uses server-side `?status=` filter (PUBLISHED/PRIVATE/DRAFT) instead of client-side tab grouping so pagination stays correct inside each bucket — tab badge counts come from `IBlogService.GetMyStatusCountsAsync`. Pager visual styling is centralised in `wwwroot/css/site.css` under `.app-pager` — wrap the `@Html.PagedListPager(...)` in `<nav class="app-pager flex justify-center">` to get consistent, centered pagination across the app.

**ViewModels vs Models.** Controllers bind to `ViewModels/*` (e.g. `BlogCreateViewModel`, `LoginViewModel`, `UserEditViewModel`, `AvatarUploadViewModel`) and never expose entity types directly to views for write operations. Read views can take entities (e.g. `_BlogCard.cshtml` is `@model Blog`).

**Shared partials & helpers.**
- `Views/Shared/_BlogCard.cshtml` — reusable summary card consumed by Posts feed and Home. Tunables passed via `ViewData`: `Compact` (bool), `ShowStatusPill` (bool), `ExcerptLength` (int), `From` (string forwarded as `?from=` on the Detail link so Edit/back-nav can return to the right list).
- `Views/Shared/_CommentItem.cshtml` + `_CommentSection.cshtml` — comment list + form; the section partial owns the AJAX JS for the page.
- `Helpers/ViewHelpers.cs` — static `Excerpt`, `Initials`, `RelativeTime` used across views. Registered globally via `Views/_ViewImports.cshtml`.

**Layout & UI.** `Views/Shared/_Layout.cshtml` carries the entire site shell including a large inline `<style>` block with CSS variables (`--black`, `--gray-*`, `--font-display`, etc.) — page-level styles in views reference those variables. Bootstrap/jQuery/jquery-validation are vendored via `libman.json` into `wwwroot/lib/`; run `libman restore` if those folders are empty. Note: `_Layout.cshtml` references `jquery-validation-unobtrusive` which is **not** in `libman.json` — add it to libman or download it if validation scripts 404. Tailwind utilities (compiled to `wwwroot/css/output.css`) coexist with the hand-rolled CSS variable system — both are loaded.

**View helpers.** `Helpers/ViewHelpers.cs` provides `Excerpt(html, max)` (strips tags + truncates HTML), `Initials(name)`, and `RelativeTime(utc)`. Use these in Razor views rather than re-implementing — they assume Quill-style HTML content and the project's UTC-stored timestamps.

**Controller status.** All six controllers (`AuthController`, `BlogsController`, `CommentsController`, `HomeController`, `ProfileController`, `UsersController`) are fully implemented and refactored onto the service layer — each constructor takes `I<Feature>Service` rather than `AppDbContext`. Avatar uploads land in `wwwroot/avatars/` (gitignored / runtime-created).

**Uploads.** `wwwroot/avatars/` is the upload target for user avatar images. It is created at runtime and not checked in — code that writes there must `Directory.CreateDirectory` defensively.
