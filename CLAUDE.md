# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core 8.0 MVC personal blog app. SQL Server + EF Core (Code-First) + ASP.NET Identity with cookie auth. Razor views, Bootstrap 5, Quill rich-text editor (CDN) for blog content.

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
```

There is no test project.

## Database setup

`appsettings.json` ships with a Trusted_Connection string to `Server=localhost;Database=BlogDb`. Override it for SQL auth as shown in `README.md`. The DB schema is owned by EF migrations under `Migrations/` — never edit the schema by hand; add a new migration.

On every app startup `SeedData.InitializeAsync` runs (called from `Program.cs`) and:
- creates the `ADMIN` and `USER` roles if missing (role names are **uppercase** — `User.IsInRole("ADMIN")` is case-sensitive in checks throughout the code),
- creates an admin account `admin@blogapp.com` / `Admin@123` if missing,
- seeds 3 sample blog posts if the `Blogs` table is empty.

## Architecture

**Auth & authorization.** Cookie auth is configured in `Program.cs` with custom paths: `/auth/login`, `/auth/logout`, `/auth/access-denied`. Two named policies exist — `AdminOnly` (ADMIN) and `UserOnly` (USER or ADMIN) — but most controllers currently use `[Authorize]` or `[Authorize(Roles = "ADMIN")]` directly. `AuthController` is `[AllowAnonymous]`. Newly-registered users are auto-assigned the `USER` role in `AuthController.Register`.

**Data layer.** `AppDbContext : IdentityDbContext<User>` is declared inside a *nested* namespace `Personal_Blog_Application.Data.BlogApp.Data` — using statements need both `Personal_Blog_Application.Data` (for `SeedData`) and `Personal_Blog_Application.Data.BlogApp.Data` (for `AppDbContext`). Existing controllers already do this; follow the same pattern.

Domain model:
- `User : IdentityUser` adds `AvatarUrl`, `IsActive`, and navigation collections for `Blogs` and `Comments`.
- `Blog` and `Comment` use `string CreatedBy` as the FK to `User.Id`.
- Cascade rules in `OnModelCreating`: `User→Blogs` cascades, `Blog→Comments` cascades, but **`User→Comments` uses `NoAction`** to avoid multiple-cascade-path errors on SQL Server. Preserve this when adding relationships.

**Per-role data visibility.** `BlogsController.Index` filters in the controller, not via a global query filter: `ADMIN` sees all blogs, everyone else sees only `b.CreatedBy == userId`. Apply the same pattern for any other list endpoint you add.

**Blog content is HTML.** `BlogCreateViewModel.Content` is the raw HTML produced by Quill (initialized in `Views/Blogs/Create.cshtml`). Quill writes its HTML into the hidden `Content` field on form `formdata` event. Any new editor view must replicate this binding or the field will submit empty.

**ViewModels vs Models.** Controllers bind to `ViewModels/*` (e.g. `BlogCreateViewModel`, `LoginViewModel`) and never expose entity types directly to views for write operations.

**Layout & UI.** `Views/Shared/_Layout.cshtml` carries the entire site shell including a large inline `<style>` block with CSS variables (`--black`, `--gray-*`, `--font-display`, etc.) — page-level styles in views reference those variables. Bootstrap/jQuery/jquery-validation are vendored via `libman.json` into `wwwroot/lib/`; run `libman restore` if those folders are empty. Note: `_Layout.cshtml` references `jquery-validation-unobtrusive` which is **not** in `libman.json` — add it to libman or download it if validation scripts 404.

**Stub controllers.** `UsersController`, `ProfileController`, `CommentsController` exist with `[Authorize]` attributes but no actions yet — they are placeholders for the in-progress feature work, not dead code. The navbar already links to `Users/Index`, `Profile/Index`, and `Profile/Avatar`, so adding those actions is the natural next step.
