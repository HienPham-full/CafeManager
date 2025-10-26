using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CafeWeb.Models;

namespace CafeWeb.Controllers
{
    public class AccountsController : Controller
    {
        private readonly AppDbContext _context;

        public AccountsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Accounts/Login
        [HttpGet]
        public IActionResult Login()
        {
            // Nếu đã đăng nhập, chuyển hướng về trang tương ứng
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                var role = HttpContext.Session.GetString("UserRole");
                if (role == "admin" || role == "staff")
                    return RedirectToAction("Dashboard", "Admin");
                else
                    return RedirectToAction("Index", "Users");
            }
            return View();
        }

        // POST: /Accounts/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            // Tìm user trong database
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

            if (user == null)
            {
                ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác!";
                return View();
            }

            // Lưu thông tin vào Session
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("FullName", user.FullName ?? user.Username);
            HttpContext.Session.SetString("UserRole", user.Role);

            // Chuyển hướng theo role
            if (user.Role == "admin" || user.Role == "staff")
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else
            {
                return RedirectToAction("Index", "Users");
            }
        }

        // GET: /Accounts/Register
        [HttpGet]
        public IActionResult Register()
        {
            // Nếu đã đăng nhập, chuyển về trang chủ
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                var role = HttpContext.Session.GetString("UserRole");
                if (role == "admin" || role == "staff")
                    return RedirectToAction("Dashboard", "Admin");
                else
                    return RedirectToAction("Index", "Users");
            }
            return View();
        }

        // POST: /Accounts/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string password, string confirmPassword, string fullName)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp!";
                return View();
            }

            if (password.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự!";
                return View();
            }

            // Kiểm tra username đã tồn tại chưa
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (existingUser != null)
            {
                ViewBag.Error = "Tên đăng nhập đã tồn tại!";
                return View();
            }

            // Tạo user mới
            var newUser = new User
            {
                Username = username,
                Password = password, // Trong thực tế nên hash password
                FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName,
                Role = "user",
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Tự động đăng nhập sau khi đăng ký
            HttpContext.Session.SetInt32("UserId", newUser.Id);
            HttpContext.Session.SetString("Username", newUser.Username);
            HttpContext.Session.SetString("FullName", newUser.FullName);
            HttpContext.Session.SetString("UserRole", newUser.Role);

            ViewBag.Success = "Đăng ký thành công!";
            return RedirectToAction("Index", "Users");
        }

        // GET: /Accounts/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}