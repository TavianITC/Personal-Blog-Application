# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core 8.0 MVC personal blog app. SQL Server + EF Core (Code-First) + ASP.NET Identity with cookie auth. Razor views, Bootstrap 5, Quill rich-text editor (CDN) for blog content.

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

**Auth & authorization.** Cookie auth is configured in `Program.cs` with custom paths: `/auth/login`, `/auth/logout`, `/auth/access-denied`. Two named policies exist — `AdminOnly` (ADMIN) and `UserOnly` (USER or ADMIN) — but most controllers currently use `[Authorize]` or `[Authorize(Roles = "ADMIN")]` directly. `AuthController` is `[AllowAnonymous]`. Newly-registered users are auto-assigned the `USER` role in `AuthController.Register`.

**Data layer.** `AppDbContext : IdentityDbContext<User>` lives in `Personal_Blog_Application.Data` — a single `using Personal_Blog_Application.Data;` covers both `AppDbContext` and `SeedData`.

Domain model:
- `User : IdentityUser` adds `AvatarUrl`, `IsActive`, and navigation collections for `Blogs` and `Comments`.
- `Blog` and `Comment` use `string CreatedBy` as the FK to `User.Id`.
- Cascade rules in `OnModelCreating`: `User→Blogs` cascades, `Blog→Comments` cascades, but **`User→Comments` uses `NoAction`** to avoid multiple-cascade-path errors on SQL Server. Preserve this when adding relationships.

**Per-role data visibility.** `BlogService.GetFeedAsync` filters by role — `ADMIN` sees every post, everyone else sees only `b.Status == "PUBLISHED"`. `GetMineAsync` is always scoped to the caller's posts. Apply the same `(userId, isAdmin)` parameter pattern for any new list endpoint.

**Service layer.** Business logic + data access live in `Services/<Feature>/` (one folder per feature, each with `IFooService.cs` + `FooService.cs`). Controllers are thin: bind input, call service, map result to `IActionResult`. Services never touch `HttpContext`, `ModelState`, or `IActionResult` — they receive `userId` / `isAdmin` (or `currentUserId`) as parameters and return `OperationResult` / `OperationResult<T>` from `Services/Common`. The result carries a `ResultStatus` (`Success`, `ValidationError`, `NotFound`, `Forbidden`, `Conflict`) plus optional `Errors` list or `FieldErrors` dictionary; the controller maps these to `NotFound()` / `Forbid()` / `ModelState.AddModelError`. Things that must stay in the controller: TempData/ViewBag, AJAX detection (`X-Requested-With` header), `RenderPartialAsync` (uses `ICompositeViewEngine` + `ControllerContext`), and `SignInManager.RefreshSignInAsync` after username changes. Register every new service in `Program.cs` with `AddScoped`.

**Blog content is HTML.** `BlogCreateViewModel.Content` is the raw HTML produced by Quill (initialized in `Views/Blogs/Create.cshtml`). Quill writes its HTML into the hidden `Content` field on form `formdata` event. Any new editor view must replicate this binding or the field will submit empty.

**ViewModels vs Models.** Controllers bind to `ViewModels/*` (e.g. `BlogCreateViewModel`, `LoginViewModel`) and never expose entity types directly to views for write operations.

**Layout & UI.** `Views/Shared/_Layout.cshtml` carries the entire site shell including a large inline `<style>` block with CSS variables (`--black`, `--gray-*`, `--font-display`, etc.) — page-level styles in views reference those variables. Bootstrap/jQuery/jquery-validation are vendored via `libman.json` into `wwwroot/lib/`; run `libman restore` if those folders are empty. Note: `_Layout.cshtml` references `jquery-validation-unobtrusive` which is **not** in `libman.json` — add it to libman or download it if validation scripts 404. Tailwind utilities (compiled to `wwwroot/css/output.css`) coexist with the hand-rolled CSS variable system — both are loaded.

**View helpers.** `Helpers/ViewHelpers.cs` provides `Excerpt(html, max)` (strips tags + truncates HTML), `Initials(name)`, and `RelativeTime(utc)`. Use these in Razor views rather than re-implementing — they assume Quill-style HTML content and the project's UTC-stored timestamps.

**Controller status.** `AuthController`, `BlogsController`, `CommentsController`, `UsersController`, and `HomeController` are fully implemented and refactored onto the service layer (constructor takes `I<Feature>Service`, not `AppDbContext`). `ProfileController` is still a `[Authorize]` stub (no actions) — the navbar links `Profile/Index` and `Profile/Avatar` so adding those actions is the natural next step. When you build it, follow the service-layer pattern: create `Services/Profile/IProfileService.cs` + `ProfileService.cs` first. Avatar uploads land in `wwwroot/avatars/` (gitignored / runtime-created).

**Uploads.** `wwwroot/avatars/` is the upload target for user avatar images. It is created at runtime and not checked in — code that writes there must `Directory.CreateDirectory` defensively.
