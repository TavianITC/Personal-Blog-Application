# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core 8.0 MVC personal blog app. SQL Server + EF Core (Code-First) + ASP.NET Identity with cookie auth. Razor views styled with **Tailwind CSS** (compiled to `wwwroot/css/output.css`), Quill rich-text editor (CDN) for blog content.

The solution file `Personal Blog Application.slnx` lives at the repo root; the actual project lives in the nested `Personal Blog Application/` directory (note the space — quote it on the command line).

## Common commands

Run from inside the project directory (`Personal Blog Application/`):

```powershell
dotnet restore
dotnet build
dotnet run                              # serves http://localhost:5000, https://localhost:7000
dotnet ef migrations add <Name>
dotnet ef database update               # apply EF migrations to BlogDb
dotnet ef migrations remove

# Tailwind pipeline (npm) — required, _Layout.cshtml links wwwroot/css/output.css
npm install
npm run css:build                       # one-shot minified build
npm run css:watch                       # rebuild on .cshtml change during dev
```

There is no test project.

## Database setup

`appsettings.json` ships with a Trusted_Connection string to `Server=localhost;Database=BlogDb`. Override it for SQL auth as shown in `README.md`. The DB schema is owned by EF migrations under `Migrations/` — never edit the schema by hand; add a new migration.

On every app startup `SeedData.InitializeAsync` runs (called from `Program.cs`) and:
- creates the `ADMIN` and `USER` roles if missing (role names are **uppercase** — `User.IsInRole("ADMIN")` is case-sensitive in checks throughout the code),
- creates an admin account `admin@blogapp.com` / `Admin@123` if missing,
- seeds 3 sample blog posts if the `Blogs` table is empty.

## Architecture

**Auth & authorization.** Cookie auth is configured in `Program.cs` with custom paths: `/auth/login`, `/auth/logout`, `/auth/access-denied`. Two named policies exist — `AdminOnly` (ADMIN) and `UserOnly` (USER or ADMIN) — but most controllers currently use `[Authorize]` or `[Authorize(Roles = "ADMIN")]` directly. `AuthController` is `[AllowAnonymous]`. Newly-registered users are auto-assigned the `USER` role in `AuthController.Register`. `AuthController.Login` rejects users with `IsActive == false` before calling `PasswordSignInAsync`.

**Data layer.** `AppDbContext : IdentityDbContext<User>` lives in `Personal_Blog_Application.Data` alongside `SeedData`.

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

**Avatar / navbar plumbing.** `ProfileController.Index` shows the user's profile; `POST /profile/avatar` validates `IFormFile` (extension ∈ {.jpg/.jpeg/.png/.gif}, content type ∈ {image/jpeg, image/png, image/gif}, ≤ 2 MB) and saves to `wwwroot/avatars/{guid}.ext`. `User.AvatarUrl` stores the URL (`/avatars/{name}`). The previous file is best-effort deleted on update so the folder doesn't grow.

Because the navbar shows the avatar on every page, a global async action filter — `Filters/PopulateUserNavigationFilter` registered in `Program.cs` — loads the signed-in user once per request and writes `ViewBag.AvatarUrl`. `_Layout.cshtml` reads `ViewBag.AvatarUrl` (falling back to initials). If you ever optimize this, the obvious upgrade is to bake `AvatarUrl` into a cookie claim via `IUserClaimsPrincipalFactory<User>` and refresh on avatar change.

**Blog content is HTML.** `BlogCreateViewModel.Content` is the raw HTML produced by Quill (initialized in `Views/Blogs/Create.cshtml`). Quill writes its HTML into the hidden `Content` field on form `formdata` event. Any new editor view must replicate this binding or the field will submit empty. The `ViewHelpers.Excerpt(html, max)` helper strips tags + decodes entities for list/card excerpts — use it instead of rendering raw HTML in summaries.

**ViewModels vs Models.** Controllers bind to `ViewModels/*` (e.g. `BlogCreateViewModel`, `LoginViewModel`, `UserEditViewModel`, `AvatarUploadViewModel`) and never expose entity types directly to views for write operations. Read views can take entities (e.g. `_BlogCard.cshtml` is `@model Blog`).

**Shared partials & helpers.**
- `Views/Shared/_BlogCard.cshtml` — reusable summary card consumed by Posts feed and Home. Tunables passed via `ViewData`: `Compact` (bool), `ShowStatusPill` (bool), `ExcerptLength` (int), `From` (string forwarded as `?from=` on the Detail link so Edit/back-nav can return to the right list).
- `Views/Shared/_CommentItem.cshtml` + `_CommentSection.cshtml` — comment list + form; the section partial owns the AJAX JS for the page.
- `Helpers/ViewHelpers.cs` — static `Excerpt`, `Initials`, `RelativeTime` used across views. Registered globally via `Views/_ViewImports.cshtml`.

**UI / Tailwind.** `_Layout.cshtml` is the site shell. Tailwind is configured in `tailwind.config.js`:
- Custom palette (`black`, `gray-*`, status colors `draft`/`private`/`published`/`success`/`danger`, `error`) — extend the config rather than hardcoding hex values in classes.
- Custom font families (`font-display` = DM Serif Display, `font-sans` = DM Sans), `max-w-content` (1120px), `h-nav` / `spacing.nav` (60px), `shadow-dropdown`.
- Tailwind scans `Views/**/*.cshtml`, `Pages/**/*.cshtml`, `Areas/**/*.cshtml` — new utility classes only appear in `output.css` after `npm run css:build` (or `css:watch`).

**Vendored JS libs.** Bootstrap is gone, but jQuery + jquery-validation are still vendored via `libman.json` into `wwwroot/lib/` (run `libman restore` if those folders are empty). `_Layout.cshtml` also references `~/lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js` which is **not** declared in `libman.json` — add it to libman or drop the file in by hand if validation scripts 404.
