using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CafeWeb.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CafeWeb.Controllers
{
    [Route("Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ========================================
        // TEST API - Kiểm tra kết nối
        // ========================================
        [HttpGet("Test")]
        public IActionResult Test()
        {
            return Json(new { success = true, message = "API working!" });
        }

        // ========================================
        // DASHBOARD - Trang chủ quản trị
        // ========================================
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            if (!CheckAuth())
            {
                TempData["Error"] = "Vui lòng đăng nhập để tiếp tục!";
                return RedirectToAction("Login", "Accounts");
            }

            try
            {
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                // Thống kê doanh thu
                ViewBag.TodayRevenue = await _context.Payments
                    .Where(p => p.PaidAt.Date == today)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                ViewBag.WeekRevenue = await _context.Payments
                    .Where(p => p.PaidAt >= startOfWeek)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                ViewBag.MonthRevenue = await _context.Payments
                    .Where(p => p.PaidAt >= startOfMonth)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                ViewBag.TotalRevenue = await _context.Payments
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                // Thống kê đơn hàng
                ViewBag.TodayOrders = await _context.Orders
                    .CountAsync(o => o.CreatedAt.Date == today);

                ViewBag.WeekOrders = await _context.Orders
                    .CountAsync(o => o.CreatedAt >= startOfWeek);

                ViewBag.MonthOrders = await _context.Orders
                    .CountAsync(o => o.CreatedAt >= startOfMonth);

                ViewBag.PendingOrders = await _context.Orders
                    .CountAsync(o => o.Status == "pending");

                ViewBag.ProcessingOrders = await _context.Orders
                    .CountAsync(o => o.Status == "processing");

                ViewBag.DoneOrders = await _context.Orders
                    .CountAsync(o => o.Status == "done");

                ViewBag.CancelledOrders = await _context.Orders
                    .CountAsync(o => o.Status == "cancelled");

                // Thống kê sản phẩm
                ViewBag.TotalProducts = await _context.Products.CountAsync();
                
                ViewBag.ActiveProducts = await _context.Products
                    .CountAsync(p => p.IsActive == true);

                ViewBag.InactiveProducts = await _context.Products
                    .CountAsync(p => p.IsActive == false);

                // Danh sách đơn hàng gần đây
                ViewBag.RecentOrders = await _context.Orders
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .Select(o => new
                    {
                        o.Id,
                        o.CustomerName,
                        o.Total,
                        o.Status,
                        o.CreatedAt
                    })
                    .ToListAsync();

                // Top 5 sản phẩm bán chạy
                ViewBag.TopProducts = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Include(oi => oi.Product)
                    .Where(oi => oi.Order.Status == "done")
                    .GroupBy(oi => new 
                    { 
                        oi.ProductId, 
                        oi.Product.Name, 
                        oi.Product.Price 
                    })
                    .Select(g => new
                    {
                        ProductId = g.Key.ProductId,
                        Name = g.Key.Name,
                        Price = g.Key.Price,
                        SoldCount = g.Sum(oi => oi.Quantity)
                    })
                    .OrderByDescending(p => p.SoldCount)
                    .Take(5)
                    .ToListAsync();

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in Dashboard: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["Error"] = "Đã có lỗi xảy ra khi tải dữ liệu Dashboard!";
                return View();
            }
        }

        // ========================================
        // PRODUCTS API - Quản lý sản phẩm
        // ========================================
        
        // API: Lấy tất cả sản phẩm đang hoạt động (cho dropdown)
        [HttpGet("Products/GetAllActive")]
        public async Task<IActionResult> GetAllActiveProducts()
        {
            try
            {
                Console.WriteLine("========================================");
                Console.WriteLine("🔍 API CALLED: GetAllActiveProducts");
                Console.WriteLine($"⏰ Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("========================================");

                // Test database connection
                var canConnect = await _context.Database.CanConnectAsync();
                Console.WriteLine($"📊 Database connection: {(canConnect ? "✅ OK" : "❌ FAILED")}");

                if (!canConnect)
                {
                    return Json(new { 
                        success = false, 
                        message = "Không thể kết nối database" 
                    });
                }

                // Count total products
                var totalProducts = await _context.Products.CountAsync();
                Console.WriteLine($"📦 Total products in database: {totalProducts}");

                // Count active products
                var activeCount = await _context.Products.CountAsync(p => p.IsActive);
                Console.WriteLine($"✅ Active products: {activeCount}");

                if (totalProducts == 0)
                {
                    Console.WriteLine("⚠️ WARNING: No products in database!");
                    return Json(new List<object>());
                }

                // Get active products - FIX: Không dùng == true
                var products = await _context.Products
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        price = p.Price,
                        category = p.Category
                    })
                    .ToListAsync();

                Console.WriteLine($"✅ API: Returning {products.Count} active products");
                
                foreach (var p in products)
                {
                    Console.WriteLine($"  - ID: {p.id}, Name: {p.name}, Price: {p.price}");
                }
                
                return Json(products);
            }
            catch (Exception ex)
            {
                Console.WriteLine("========================================");
                Console.WriteLine("❌ API ERROR: GetAllActiveProducts");
                Console.WriteLine($"Error message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine("========================================");
                
                return StatusCode(500, new { 
                    success = false, 
                    message = ex.Message
                });
            }
        }

        // API: Thêm sản phẩm mới
        [HttpPost("Products/Create")]
        public async Task<IActionResult> CreateProduct([FromForm] ProductCreateModel model)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                Console.WriteLine("========================================");
                Console.WriteLine("📦 API CALLED: CreateProduct");
                Console.WriteLine($"Name: {model.Name}");
                Console.WriteLine($"Category: {model.Category}");
                Console.WriteLine($"Price: {model.Price}");
                Console.WriteLine($"IsActive: {model.IsActive}");
                Console.WriteLine("========================================");
                
                // Validate
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    return Json(new { success = false, message = "Tên sản phẩm không được để trống" });
                }

                if (string.IsNullOrWhiteSpace(model.Category))
                {
                    return Json(new { success = false, message = "Danh mục không được để trống" });
                }

                if (model.Price <= 0)
                {
                    return Json(new { success = false, message = "Giá phải lớn hơn 0" });
                }

                if (model.Price < 1000)
                {
                    return Json(new { success = false, message = "Giá phải ít nhất 1,000đ" });
                }

                // Xử lý upload ảnh
                string imagePath = null;
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    Console.WriteLine($"📸 Processing image: {model.ImageFile.FileName}");
                    imagePath = await SaveProductImage(model.ImageFile);
                }

                // Tạo sản phẩm mới
                var product = new Product
                {
                    Name = model.Name.Trim(),
                    Category = model.Category.Trim(),
                    Price = model.Price,
                    Description = model.Description?.Trim(),
                    Image = imagePath,
                    IsActive = model.IsActive
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ API: Product created successfully with ID: {product.Id}");

                return Json(new { success = true, message = "Thêm sản phẩm thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ API Error CreateProduct: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "Lỗi khi thêm sản phẩm: " + ex.Message });
            }
        }

        // ========================================
        // HELPER: Lưu ảnh sản phẩm
        // ========================================
        private async Task<string> SaveProductImage(IFormFile imageFile)
        {
            try
            {
                // Kiểm tra kích thước file (tối đa 5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    throw new Exception("File quá lớn. Kích thước tối đa là 5MB");
                }

                // Kiểm tra định dạng file
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    throw new Exception("Chỉ chấp nhận file ảnh: jpg, jpeg, png, gif, webp");
                }

                // Tạo tên file unique
                string fileName = Guid.NewGuid().ToString() + extension;
                
                // Đường dẫn thư mục lưu ảnh
                string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "products");
                
                // Tạo thư mục nếu chưa tồn tại
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Đường dẫn file đầy đủ
                string filePath = Path.Combine(uploadsFolder, fileName);

                // Lưu file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                Console.WriteLine($"✅ Image saved: {fileName}");

                // Trả về đường dẫn tương đối
                return "/images/products/" + fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error SaveProductImage: {ex.Message}");
                throw;
            }
        }

        // ========================================
        // HELPER: Kiểm tra đăng nhập
        // ========================================
        private bool CheckAuth()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            
            return userId.HasValue && 
                   !string.IsNullOrEmpty(role) && 
                   (role == "admin" || role == "staff");
        }

        // ========================================
        // API: Lấy thống kê
        // ========================================
        [HttpGet("api/stats")]
        public async Task<IActionResult> GetStats(string period = "today")
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                DateTime startDate;
                var today = DateTime.Today;

                switch (period.ToLower())
                {
                    case "week":
                        startDate = today.AddDays(-7);
                        break;
                    case "month":
                        startDate = today.AddDays(-30);
                        break;
                    case "year":
                        startDate = today.AddYears(-1);
                        break;
                    default:
                        startDate = today;
                        break;
                }

                var revenue = await _context.Payments
                    .Where(p => p.PaidAt >= startDate)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                var orders = await _context.Orders
                    .CountAsync(o => o.CreatedAt >= startDate);

                var pendingOrders = await _context.Orders
                    .CountAsync(o => o.Status == "pending");

                return Json(new
                {
                    success = true,
                    revenue = revenue,
                    orders = pendingOrders,
                    totalOrders = orders,
                    period = period
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ API Error GetStats: {ex.Message}");
                return Json(new 
                { 
                    success = false, 
                    message = ex.Message 
                });
            }
        }

        // ========================================
        // PAGES - Các trang khác
        // ========================================

        // ❌ XÓA ACTION NÀY - Đã có ReportsController riêng
        // [HttpGet("Reports")]
        // public async Task<IActionResult> Reports()
        // {
        //     ... code cũ ...
        // }

        [HttpGet("Settings")]
        public async Task<IActionResult> Settings()
        {
            if (!CheckAuth())
            {
                TempData["Error"] = "Vui lòng đăng nhập để tiếp tục!";
                return RedirectToAction("Login", "Accounts");
            }

            var role = HttpContext.Session.GetString("UserRole");
            if (role != "admin")
            {
                TempData["Error"] = "Chỉ Admin mới có quyền truy cập Cài đặt!";
                return RedirectToAction("Dashboard");
            }

            ViewBag.PendingOrders = await _context.Orders
                .CountAsync(o => o.Status == "pending");

            return View();
        }
    }

    // ========================================
    // MODELS
    // ========================================
    public class ProductCreateModel
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public IFormFile? ImageFile { get; set; }
        public bool IsActive { get; set; } = true;
    }
}