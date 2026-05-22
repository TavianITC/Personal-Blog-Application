using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Personal_Blog_Application.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
    }
}
