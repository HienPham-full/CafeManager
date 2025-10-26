using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace CafeWeb.Controllers
{
    [Route("Users")]
    public class UsersController : Controller
    {
        // GET: /Users/Index
        [HttpGet("Index")]
        [HttpGet("")]
        public IActionResult Index()
        {
            // Kiểm tra đăng nhập
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Accounts");
            }

            ViewBag.Username = HttpContext.Session.GetString("Username");
            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            ViewBag.Role = HttpContext.Session.GetString("UserRole");

            return View();
        }
    }
}