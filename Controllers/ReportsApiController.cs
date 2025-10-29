using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeWeb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CafeWeb.Controllers.Admin
{
    [Route("Admin/api")]
    [ApiController]
    public class ReportsApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReportsApiController> _logger;

        public ReportsApiController(AppDbContext context, ILogger<ReportsApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Admin/api/reports?period=today
        [HttpGet("reports")]
        public async Task<IActionResult> GetReports(string period = "today")
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);
                var (prevStartDate, prevEndDate) = GetPreviousDateRange(period);

                // Get current period data
                var currentData = await GetPeriodData(startDate, endDate);
                
                // Get previous period data for comparison
                var previousData = await GetPeriodData(prevStartDate, prevEndDate);

                // Calculate growth percentages
                var revenueGrowth = CalculateGrowth(currentData.TotalRevenue, previousData.TotalRevenue);
                var ordersGrowth = CalculateGrowth(currentData.TotalOrders, previousData.TotalOrders);
                var avgGrowth = CalculateGrowth(currentData.AvgOrderValue, previousData.AvgOrderValue);
                var successGrowth = CalculateGrowth(currentData.SuccessRate, previousData.SuccessRate);

                // Get chart data
                var chartData = await GetChartData(startDate, endDate, period);

                // Get table data
                var tableData = await GetTableData(startDate, endDate, period);

                var result = new
                {
                    success = true,
                    totalRevenue = currentData.TotalRevenue,
                    totalOrders = currentData.TotalOrders,
                    avgOrderValue = currentData.AvgOrderValue,
                    successRate = currentData.SuccessRate,
                    revenueGrowth = revenueGrowth,
                    ordersGrowth = ordersGrowth,
                    avgGrowth = avgGrowth,
                    successGrowth = successGrowth,
                    chartData = chartData,
                    tableData = tableData
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetReports");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải báo cáo: " + ex.Message });
            }
        }

        // GET: /Admin/api/reports/export?period=today&format=csv
        [HttpGet("reports/export")]
        public async Task<IActionResult> ExportReports(string period = "today", string format = "csv")
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);
                var tableData = await GetTableData(startDate, endDate, period);

                if (format.ToLower() == "csv")
                {
                    var csv = GenerateCSV(tableData);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                    var fileName = $"bao-cao-doanh-thu-{period}-{DateTime.Now:yyyyMMdd}.csv";
                    
                    return File(bytes, "text/csv", fileName);
                }

                return BadRequest(new { success = false, message = "Định dạng không hỗ trợ" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExportReports");
                return StatusCode(500, new { success = false, message = "Lỗi khi xuất báo cáo: " + ex.Message });
            }
        }

        // GET: /Admin/api/reports/top-products?period=today&limit=10
        [HttpGet("reports/top-products")]
        public async Task<IActionResult> GetTopProducts(string period = "today", int limit = 10)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                var products = await (
    from oi in _context.OrderItems
    join o in _context.Orders on oi.OrderId equals o.Id
    join p in _context.Products on oi.ProductId equals p.Id
    where o.CreatedAt >= startDate
        && o.CreatedAt < endDate
        && (o.Status == "processing" || o.Status == "done")
    group new { oi, o, p } by new { p.Id, p.Name, p.Category, p.Price } into g
    orderby g.Sum(x => x.oi.Quantity * x.oi.Price) descending
    select new
    {
        id = g.Key.Id,
        name = g.Key.Name,
        category = g.Key.Category ?? "Khác",
        price = g.Key.Price,
        orderCount = g.Select(x => x.o.Id).Distinct().Count(),
        totalQuantity = g.Sum(x => x.oi.Quantity),
        totalRevenue = g.Sum(x => x.oi.Quantity * x.oi.Price)
    }
)
.Take(limit)
.ToListAsync();


                return Ok(new { success = true, products = products });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopProducts");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải sản phẩm bán chạy: " + ex.Message });
            }
        }

        // GET: /Admin/api/reports/summary?period=today
        [HttpGet("reports/summary")]
        public async Task<IActionResult> GetSummary(string period = "today")
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);
                var data = await GetPeriodData(startDate, endDate);

                // Get additional metrics
                var orders = await _context.Orders
                    .Where(o => o.CreatedAt >= startDate && o.CreatedAt < endDate)
                    .ToListAsync();

                var uniqueCustomers = orders
                    .Select(o => o.CustomerName)
                    .Distinct()
                    .Count();

                var pendingOrders = orders.Count(o => o.Status == "pending");
                var processingOrders = orders.Count(o => o.Status == "processing");
                var completedOrders = orders.Count(o => o.Status == "done");
                var cancelledOrders = orders.Count(o => o.Status == "cancelled");

                var result = new
                {
                    success = true,
                    totalRevenue = data.TotalRevenue,
                    totalOrders = data.TotalOrders,
                    avgOrderValue = data.AvgOrderValue,
                    successRate = data.SuccessRate,
                    uniqueCustomers = uniqueCustomers,
                    pendingOrders = pendingOrders,
                    processingOrders = processingOrders,
                    completedOrders = completedOrders,
                    cancelledOrders = cancelledOrders
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSummary");
                return StatusCode(500, new { success = false, message = "Lỗi khi tải tóm tắt: " + ex.Message });
            }
        }

        #region Helper Methods

        private (DateTime startDate, DateTime endDate) GetDateRange(string period)
        {
            var now = DateTime.Now;
            
            return period.ToLower() switch
            {
                "today" => (now.Date, now.Date.AddDays(1)),
                "week" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(1)),
                "month" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
                "year" => (new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1)),
                _ => (now.Date, now.Date.AddDays(1))
            };
        }

        private (DateTime startDate, DateTime endDate) GetPreviousDateRange(string period)
        {
            var now = DateTime.Now;
            
            return period.ToLower() switch
            {
                "today" => (now.Date.AddDays(-1), now.Date),
                "week" => (now.Date.AddDays(-(int)now.DayOfWeek - 7), now.Date.AddDays(-(int)now.DayOfWeek)),
                "month" => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1)),
                "year" => (new DateTime(now.Year - 1, 1, 1), new DateTime(now.Year, 1, 1)),
                _ => (now.Date.AddDays(-1), now.Date)
            };
        }

        private async Task<PeriodData> GetPeriodData(DateTime startDate, DateTime endDate)
        {
            var orders = await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt < endDate)
                .ToListAsync();

            var totalOrders = orders.Count;
            var totalRevenue = orders
                .Where(o => o.Status == "processing" || o.Status == "done")
                .Sum(o => o.Total);
            var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
            var completedOrders = orders.Count(o => o.Status == "done");
            var successRate = totalOrders > 0 ? (completedOrders * 100.0m / totalOrders) : 0;

            return new PeriodData
            {
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                AvgOrderValue = avgOrderValue,
                SuccessRate = successRate
            };
        }

        private async Task<object> GetChartData(DateTime startDate, DateTime endDate, string period)
        {
            var orders = await _context.Orders
                .Where(o => o.CreatedAt >= startDate 
                    && o.CreatedAt < endDate
                    && (o.Status == "processing" || o.Status == "done"))
                .ToListAsync();

      var groupedData = period.ToLower() switch
{
    "today" => orders
        .GroupBy(o => (object)o.CreatedAt.Hour)
        .Select(g => new
        {
            DateGroup = g.Key,
            OrderCount = g.Count(),
            Revenue = g.Sum(o => o.Total),
            Label = $"{g.Key}:00"
        })
        .OrderBy(x => x.DateGroup)
        .ToList(),

    "week" => orders
        .GroupBy(o => (object)o.CreatedAt.Date)
        .Select(g => new
        {
            DateGroup = g.Key,
            OrderCount = g.Count(),
            Revenue = g.Sum(o => o.Total),
            Label = ((DateTime)g.Key).ToString("dd/MM")
        })
        .OrderBy(x => (DateTime)x.DateGroup)
        .ToList(),

    "month" => orders
        .GroupBy(o => (object)o.CreatedAt.Date)
        .Select(g => new
        {
            DateGroup = g.Key,
            OrderCount = g.Count(),
            Revenue = g.Sum(o => o.Total),
            Label = ((DateTime)g.Key).ToString("dd/MM")
        })
        .OrderBy(x => (DateTime)x.DateGroup)
        .ToList(),

    "year" => orders
        .GroupBy(o => (object)o.CreatedAt.Month)
        .Select(g => new
        {
            DateGroup = g.Key,
            OrderCount = g.Count(),
            Revenue = g.Sum(o => o.Total),
            Label = $"T{g.Key}"
        })
        .OrderBy(x => (int)x.DateGroup)
        .ToList(),

    _ => orders
        .GroupBy(o => (object)o.CreatedAt.Date)
        .Select(g => new
        {
            DateGroup = g.Key,
            OrderCount = g.Count(),
            Revenue = g.Sum(o => o.Total),
            Label = ((DateTime)g.Key).ToString("dd/MM")
        })
        .OrderBy(x => (DateTime)x.DateGroup)
        .ToList()
};


            return new
            {
                labels = groupedData.Select(x => x.Label).ToList(),
                revenues = groupedData.Select(x => x.Revenue).ToList(),
                orders = groupedData.Select(x => x.OrderCount).ToList()
            };
        }

        private async Task<List<object>> GetTableData(DateTime startDate, DateTime endDate, string period)
        {
            var orders = await _context.Orders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt < endDate)
                .ToListAsync();

            var groupedData = period.ToLower() switch
            {
                "year" => orders
                    .GroupBy(o => o.CreatedAt.Month)
                    .Select(g => new
                    {
                        DateGroup = (object)g.Key,
                        TotalOrders = g.Count(),
                        Revenue = g.Where(o => o.Status == "processing" || o.Status == "done").Sum(o => o.Total),
                        Completed = g.Count(o => o.Status == "done"),
                        Cancelled = g.Count(o => o.Status == "cancelled"),
                        DateLabel = $"Tháng {g.Key}"
                    })
                    .OrderByDescending(x => (int)x.DateGroup)
                    .ToList(),

                _ => orders
                    .GroupBy(o => o.CreatedAt.Date)
                    .Select(g => new
                    {
                        DateGroup = (object)g.Key,
                        TotalOrders = g.Count(),
                        Revenue = g.Where(o => o.Status == "processing" || o.Status == "done").Sum(o => o.Total),
                        Completed = g.Count(o => o.Status == "done"),
                        Cancelled = g.Count(o => o.Status == "cancelled"),
                        DateLabel = g.Key.ToString("dd/MM/yyyy")
                    })
                    .OrderByDescending(x => (DateTime)x.DateGroup)
                    .ToList()
            };

            var tableData = groupedData.Select(g => new
            {
                date = g.DateLabel,
                totalOrders = g.TotalOrders,
                revenue = g.Revenue,
                avgOrder = g.TotalOrders > 0 ? g.Revenue / g.TotalOrders : 0,
                completed = g.Completed,
                cancelled = g.Cancelled
            }).ToList<object>();

            return tableData;
        }

        private decimal CalculateGrowth(decimal current, decimal previous)
        {
            if (previous == 0) return 0;
            return Math.Round(((current - previous) / previous) * 100, 1);
        }

        private string GenerateCSV(List<object> tableData)
        {
            var csv = new System.Text.StringBuilder();
            
            // Add UTF-8 BOM
            csv.Append("\ufeff");
            
            // Add header
            csv.AppendLine("Ngày,Số đơn,Doanh thu,Trung bình/Đơn,Hoàn thành,Đã hủy");
            
            // Add data rows
            foreach (dynamic row in tableData)
            {
                csv.AppendLine($"{row.date},{row.totalOrders},{row.revenue},{row.avgOrder},{row.completed},{row.cancelled}");
            }
            
            return csv.ToString();
        }

        #endregion

        #region Data Models

        private class PeriodData
        {
            public int TotalOrders { get; set; }
            public decimal TotalRevenue { get; set; }
            public decimal AvgOrderValue { get; set; }
            public decimal SuccessRate { get; set; }
        }

        #endregion
    }
}