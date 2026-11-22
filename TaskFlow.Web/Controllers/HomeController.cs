using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Where the cookie middleware sends users who are signed in but not
        // allowed to do something (for example after Forbid()).
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;

            return View("Status", StatusViewModel.For(StatusCodes.Status403Forbidden));
        }

        // Re-executed by UseStatusCodePagesWithReExecute for empty 404/403
        // responses, so the original status code is kept.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Status(int code)
        {
            return View(StatusViewModel.For(code));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
