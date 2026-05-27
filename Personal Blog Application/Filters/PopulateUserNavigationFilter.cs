using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Personal_Blog_Application.Models;

namespace Personal_Blog_Application.Filters
{
    // Loads the signed-in user once per request and exposes AvatarUrl via ViewBag
    // so _Layout's navbar can render the user's avatar on every page. Cheap enough
    // for this app (one Identity query); revisit with claims-based caching if it
    // ever shows up in profiling.
    public class PopulateUserNavigationFilter : IAsyncActionFilter
    {
        private readonly UserManager<User> _userManager;

        public PopulateUserNavigationFilter(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Controller is Controller controller &&
                context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(context.HttpContext.User);
                if (user != null)
                {
                    controller.ViewBag.AvatarUrl = user.AvatarUrl;
                }
            }

            await next();
        }
    }
}
