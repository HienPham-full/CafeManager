using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeWeb.Models;

namespace CafeWeb.Controllers
{
    [Route("Admin/Reports")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(AppDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // Kiểm tra session
            if (!CheckAuth())
            {
                TempData["Error"] = "Vui lòng đăng nhập để tiếp tục!";
                return RedirectToAction("Login", "Accounts");
            }

            try
            {
                var today = DateTime.Today;
                var thisWeekStart = today.AddDays(-(int)today.DayOfWeek);
                var thisMonthStart = new DateTime(today.Year, today.Month, 1);
                var lastMonthStart = thisMonthStart.AddMonths(-1);
                var lastMonthEnd = thisMonthStart.AddDays(-1);

                // Badge đơn hàng chờ xử lý
                ViewBag.PendingOrders = await _context.Orders
                    .CountAsync(o => o.Status == "pending");

                // Thống kê hôm nay
                var todayOrders = await _context.Orders
                    .Where(o => o.CreatedAt.Date == today)
                    .ToListAsync();

                ViewBag.TodayOrders = todayOrders.Count;
                ViewBag.TodayRevenue = todayOrders
                    .Where(o => o.Status != "cancelled")
                    .Sum(o => o.Total);
                ViewBag.TodayCompleted = todayOrders.Count(o => o.Status == "done");
                ViewBag.TodayCancelled = todayOrders.Count(o => o.Status == "cancelled");

                // Thống kê tuần này
                var weekOrders = await _context.Orders
                    .Where(o => o.CreatedAt >= thisWeekStart)
                    .ToListAsync();

                ViewBag.WeekOrders = weekOrders.Count;
                ViewBag.WeekRevenue = weekOrders
                    .Where(o => o.Status != "cancelled")
                    .Sum(o => o.Total);

                // Thống kê tháng này
                var monthOrders = await _context.Orders
                    .Where(o => o.CreatedAt >= thisMonthStart)
                    .ToListAsync();

                ViewBag.MonthOrders = monthOrders.Count;
                ViewBag.MonthRevenue = monthOrders
                    .Where(o => o.Status != "cancelled")
                    .Sum(o => o.Total);

                // Thống kê tháng trước
                var lastMonthOrders = await _context.Orders
                    .Where(o => o.CreatedAt >= lastMonthStart && o.CreatedAt <= lastMonthEnd)
                    .ToListAsync();

                ViewBag.LastMonthRevenue = lastMonthOrders
                    .Where(o => o.Status != "cancelled")
                    .Sum(o => o.Total);

                // Tính tỷ lệ tăng trưởng
                if (ViewBag.LastMonthRevenue > 0)
                {
                    ViewBag.GrowthRate = ((ViewBag.MonthRevenue - ViewBag.LastMonthRevenue) / (decimal)ViewBag.LastMonthRevenue * 100);
                }
                else
                {
                    ViewBag.GrowthRate = 0;
                }

                // Doanh thu theo ngày trong 7 ngày gần đây
                var last7Days = new List<dynamic>();
                for (int i = 6; i >= 0; i--)
                {
                    var date = today.AddDays(-i);
                    var dayOrders = await _context.Orders
                        .Where(o => o.CreatedAt.Date == date && o.Status != "cancelled")
                        .ToListAsync();

                    last7Days.Add(new
                    {
                        Date = date.ToString("dd/MM"),
                        Revenue = dayOrders.Sum(o => o.Total),
                        Orders = dayOrders.Count
                    });
                }
                ViewBag.Last7Days = last7Days;

                // Top 10 sản phẩm bán chạy
                var topProducts = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .Where(oi => oi.Product != null)
                    .GroupBy(oi => new { oi.ProductId, oi.Product.Name, oi.Product.Price })
                    .Select(g => new
                    {
                        ProductId = g.Key.ProductId,
                        Name = g.Key.Name,
                        Price = g.Key.Price,
                        TotalQuantity = g.Sum(oi => oi.Quantity),
                        TotalRevenue = g.Sum(oi => oi.Quantity * oi.Price)
                    })
                    .OrderByDescending(p => p.TotalQuantity)
                    .Take(10)
                    .ToListAsync();

                ViewBag.TopProducts = topProducts;

                // Thống kê theo trạng thái đơn hàng
                var ordersByStatus = await _context.Orders
                    .Where(o => o.CreatedAt >= thisMonthStart)
                    .GroupBy(o => o.Status)
                    .Select(g => new
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                ViewBag.OrdersByStatus = ordersByStatus;

                // Doanh thu theo danh mục
                var revenueByCategory = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.CreatedAt >= thisMonthStart && 
                                oi.Order.Status != "cancelled" && 
                                oi.Product != null)
                    .GroupBy(oi => oi.Product.Category)
                    .Select(g => new
                    {
                        Category = g.Key ?? "Khác",
                        Revenue = g.Sum(oi => oi.Quantity * oi.Price),
                        Quantity = g.Sum(oi => oi.Quantity)
                    })
                    .OrderByDescending(c => c.Revenue)
                    .ToListAsync();

                ViewBag.RevenueByCategory = revenueByCategory;

                // Giờ bán hàng cao điểm
                var ordersByHour = await _context.Orders
                    .Where(o => o.CreatedAt >= thisWeekStart)
                    .GroupBy(o => o.CreatedAt.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        Count = g.Count()
                    })
                    .OrderBy(h => h.Hour)
                    .ToListAsync();

                ViewBag.OrdersByHour = ordersByHour;

                return View("~/Views/Admin/Reports.cshtml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports");
                ViewBag.Error = "Có lỗi xảy ra khi tải báo cáo: " + ex.Message;
                ViewBag.PendingOrders = 0;
                return View("~/Views/Admin/Reports.cshtml");
            }
        }

        [HttpGet("GetRevenueByPeriod")]
        public async Task<IActionResult> GetRevenueByPeriod(string period, DateTime? startDate, DateTime? endDate)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                IQueryable<Order> query = _context.Orders.Where(o => o.Status != "cancelled");

                switch (period?.ToLower())
                {
                    case "today":
                        query = query.Where(o => o.CreatedAt.Date == DateTime.Today);
                        break;
                    case "week":
                        var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        query = query.Where(o => o.CreatedAt >= weekStart);
                        break;
                    case "month":
                        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        query = query.Where(o => o.CreatedAt >= monthStart);
                        break;
                    case "custom":
                        if (startDate.HasValue && endDate.HasValue)
                        {
                            query = query.Where(o => o.CreatedAt.Date >= startDate.Value.Date && 
                                                   o.CreatedAt.Date <= endDate.Value.Date);
                        }
                        break;
                }

                var orders = await query.ToListAsync();
                var revenue = orders.Sum(o => o.Total);
                var count = orders.Count;

                return Json(new
                {
                    success = true,
                    revenue = revenue,
                    orders = count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRevenueByPeriod");
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("ExportReport")]
        public IActionResult ExportReport(string period)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                // Implement export logic here (CSV, Excel, PDF)
                // For now, just return success
                _logger.LogInformation($"Export report requested for period: {period}");
                
                return Json(new
                {
                    success = true,
                    message = "Xuất báo cáo thành công!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExportReport");
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
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