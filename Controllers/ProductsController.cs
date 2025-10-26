using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeWeb.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CafeWeb.Controllers
{
    [Route("Admin/Products")]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(AppDbContext context, IWebHostEnvironment env, ILogger<ProductsController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        // ========================================
        // GET: /Admin/Products - Trang quản lý sản phẩm
        // ========================================
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            if (!CheckAuth())
            {
                TempData["Error"] = "Vui lòng đăng nhập để tiếp tục!";
                return RedirectToAction("Login", "Accounts");
            }

            try
            {
                ViewBag.PendingOrders = await _context.Orders
                    .CountAsync(o => o.Status == "pending");

                return View("~/Views/Admin/Products.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Products Index");
                ViewBag.PendingOrders = 0;
                return View("~/Views/Admin/Products.cshtml");
            }
        }

        // ========================================
        // GET: /Admin/Products/GetAll - Lấy tất cả sản phẩm
        // ========================================
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized", data = new List<object>() });
            }

            try
            {
                var products = await _context.Products
                    .OrderByDescending(p => p.Id)
                    .Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        category = p.Category ?? "Khác",
                        price = p.Price,
                        image = p.Image ?? "",
                        description = p.Description ?? "",
                        isActive = p.IsActive
                    })
                    .ToListAsync();

                _logger.LogInformation($"GetAll: Returning {products.Count} products");
                return Json(new { success = true, data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAll");
                return Json(new { success = false, message = ex.Message, data = new List<object>() });
            }
        }

        // ========================================
        // GET: /Admin/Products/GetAllActive - Lấy sản phẩm đang hoạt động
        // ========================================
        [HttpGet("GetAllActive")]
        public async Task<IActionResult> GetAllActive()
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized", data = new List<object>() });
            }

            try
            {
                var products = await _context.Products
                    .Where(p => p.IsActive == true)
                    .OrderBy(p => p.Name)
                    .Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        category = p.Category ?? "Khác",
                        price = p.Price,
                        image = p.Image ?? "",
                        description = p.Description ?? "",
                        isActive = p.IsActive
                    })
                    .ToListAsync();

                return Json(new { success = true, data = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllActive");
                return Json(new { success = false, message = ex.Message, data = new List<object>() });
            }
        }

        // ========================================
        // GET: /Admin/Products/Get/{id} - Lấy thông tin 1 sản phẩm
        // ========================================
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var product = await _context.Products
                    .Where(p => p.Id == id)
                    .Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        category = p.Category ?? "Khác",
                        price = p.Price,
                        image = p.Image ?? "",
                        description = p.Description ?? "",
                        isActive = p.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (product == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
                }

                return Json(new { success = true, data = product });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting product {id}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Products/Create - Tạo sản phẩm mới
        // ========================================
        [HttpPost("Create")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
        public async Task<IActionResult> Create([FromForm] ProductCreateModel model)
        {
            _logger.LogInformation("=== CREATE PRODUCT REQUEST ===");
            _logger.LogInformation($"Name: {model.Name}, Category: {model.Category}, Price: {model.Price}");

            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
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

                // Xử lý upload ảnh
                string imagePath = null;
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    var uploadResult = await SaveProductImage(model.ImageFile);
                    if (!uploadResult.success)
                    {
                        return Json(new { success = false, message = uploadResult.message });
                    }
                    imagePath = uploadResult.path;
                }

                // Tạo sản phẩm mới
                var product = new Product
                {
                    Name = model.Name.Trim(),
                    Category = model.Category.Trim(),
                    Price = model.Price,
                    Description = model.Description?.Trim() ?? "",
                    Image = imagePath ?? "",
                    IsActive = model.IsActive
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Product created successfully with ID: {product.Id}");

                return Json(new 
                { 
                    success = true, 
                    message = "Tạo sản phẩm thành công", 
                    data = new 
                    {
                        id = product.Id,
                        name = product.Name,
                        category = product.Category,
                        price = product.Price,
                        image = product.Image,
                        description = product.Description,
                        isActive = product.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                return Json(new { success = false, message = "Lỗi khi tạo sản phẩm: " + ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Products/Update - Cập nhật sản phẩm
        // ========================================
        [HttpPost("Update")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
        public async Task<IActionResult> Update([FromForm] ProductUpdateModel model)
        {
            _logger.LogInformation($"=== UPDATE PRODUCT {model.Id} ===");

            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var product = await _context.Products.FindAsync(model.Id);
                if (product == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
                }

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

                // Xử lý upload ảnh mới (nếu có)
                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    // Xóa ảnh cũ
                    if (!string.IsNullOrEmpty(product.Image))
                    {
                        DeleteProductImage(product.Image);
                    }

                    // Lưu ảnh mới
                    var uploadResult = await SaveProductImage(model.ImageFile);
                    if (!uploadResult.success)
                    {
                        return Json(new { success = false, message = uploadResult.message });
                    }
                    product.Image = uploadResult.path;
                }

                // Cập nhật thông tin
                product.Name = model.Name.Trim();
                product.Category = model.Category.Trim();
                product.Price = model.Price;
                product.Description = model.Description?.Trim() ?? "";
                product.IsActive = model.IsActive;

                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Product {product.Id} updated successfully");

                return Json(new 
                { 
                    success = true, 
                    message = "Cập nhật sản phẩm thành công",
                    data = new 
                    {
                        id = product.Id,
                        name = product.Name,
                        category = product.Category,
                        price = product.Price,
                        image = product.Image,
                        description = product.Description,
                        isActive = product.IsActive
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating product {model.Id}");
                return Json(new { success = false, message = "Lỗi khi cập nhật sản phẩm: " + ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Products/Delete/{id} - Xóa sản phẩm
        // ========================================
        [HttpPost("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
                }

                // Kiểm tra xem sản phẩm có trong đơn hàng nào không
                var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);

                if (hasOrders)
                {
                    return Json(new 
                    { 
                        success = false, 
                        message = "Không thể xóa sản phẩm này vì đã có trong đơn hàng. Bạn có thể đặt trạng thái 'Tạm ngưng' thay thế." 
                    });
                }

                // Xóa ảnh
                if (!string.IsNullOrEmpty(product.Image))
                {
                    DeleteProductImage(product.Image);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Product {id} deleted successfully");
                return Json(new { success = true, message = "Xóa sản phẩm thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting product {id}");
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa sản phẩm: " + ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Products/ToggleStatus/{id}
        // ========================================
        [HttpPost("ToggleStatus/{id}")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy sản phẩm" });
                }

                product.IsActive = !product.IsActive;
                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                return Json(new 
                { 
                    success = true, 
                    message = $"Đã {(product.IsActive ? "kích hoạt" : "tạm ngưng")} sản phẩm",
                    isActive = product.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling status for product {id}");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // ========================================
        // HELPER: LƯU ẢNH SẢN PHẨM
        // ========================================
        private async Task<(bool success, string message, string path)> SaveProductImage(IFormFile imageFile)
        {
            try
            {
                // Validate file size (max 5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    return (false, "File quá lớn. Kích thước tối đa là 5MB", null);
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    return (false, "Chỉ chấp nhận file ảnh: JPG, PNG, GIF, WEBP", null);
                }

                // Validate content type
                var allowedContentTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                if (!allowedContentTypes.Contains(imageFile.ContentType.ToLowerInvariant()))
                {
                    return (false, "Định dạng file không hợp lệ", null);
                }

                // Generate unique filename
                string fileName = $"{Guid.NewGuid()}{extension}";
                
                // Upload folder path
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "products");
                
                // Create directory if not exists
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    _logger.LogInformation($"Created directory: {uploadsFolder}");
                }

                // Full file path
                string filePath = Path.Combine(uploadsFolder, fileName);

                // Save file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"Image saved: {filePath}");

                // Return relative path
                return (true, "Upload thành công", $"/uploads/products/{fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image");
                return (false, "Lỗi khi lưu ảnh: " + ex.Message, null);
            }
        }

        // ========================================
        // HELPER: XÓA ẢNH SẢN PHẨM
        // ========================================
        private void DeleteProductImage(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                    return;

                string relativePath = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.Combine(_env.WebRootPath, relativePath);

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogInformation($"Image deleted: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting image: {imagePath}");
            }
        }

        // ========================================
        // HELPER: Kiểm tra đăng nhập và phân quyền
        // ========================================
        private bool CheckAuth()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");
            
            return userId.HasValue && 
                   !string.IsNullOrEmpty(role) && 
                   (role == "admin" || role == "staff");
        }
    }

  
}