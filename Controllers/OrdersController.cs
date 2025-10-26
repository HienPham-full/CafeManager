using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using Newtonsoft.Json;

namespace CafeWeb.Controllers
{
    [Route("Admin/[controller]")]
    public class OrdersController : Controller
    {
        private readonly string _connectionString;

        public OrdersController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // ========================================
        // HELPER: Kiểm tra quyền truy cập
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
        // GET: /Admin/Orders - Trang danh sách đơn hàng
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
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT COUNT(*) FROM orders WHERE status = 'pending'";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        ViewBag.PendingOrders = Convert.ToInt32(await command.ExecuteScalarAsync());
                    }
                }
                
                return View("~/Views/Admin/Orders.cshtml");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Orders Index: {ex.Message}");
                ViewBag.PendingOrders = 0;
                return View("~/Views/Admin/Orders.cshtml");
            }
        }

        // ========================================
        // GET: /Admin/Orders/GetAll - Lấy tất cả đơn hàng
        // ========================================
        [HttpGet("GetAll")]
        public async Task<JsonResult> GetAll()
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized", data = new List<object>() });
            }

            var orders = new List<object>();
            try
            {
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var query = @"
                        SELECT 
                            o.id,
                            o.customer_name AS customerName,
                            o.phone,
                            o.total,
                            o.status,
                            o.created_at AS createdAt,
                            o.updated_at AS updatedAt,
                            COUNT(oi.id) AS itemCount
                        FROM orders o
                        LEFT JOIN order_items oi ON o.id = oi.order_id
                        GROUP BY o.id, o.customer_name, o.phone, o.total, o.status, o.created_at, o.updated_at
                        ORDER BY o.created_at DESC";

                    using (var command = new MySqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new
                            {
                                id = reader.GetInt32("id"),
                                customerName = reader.GetString("customerName"),
                                phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? "" : reader.GetString("phone"),
                                total = reader.GetDecimal("total"),
                                status = reader.GetString("status"),
                                createdAt = reader.GetDateTime("createdAt"),
                                updatedAt = reader.IsDBNull(reader.GetOrdinal("updatedAt")) ? (DateTime?)null : reader.GetDateTime("updatedAt"),
                                itemCount = reader.GetInt32("itemCount")
                            });
                        }
                    }
                }
                
                Console.WriteLine($"✅ GetAll Orders: Found {orders.Count} orders");
                return Json(new { success = true, data = orders });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetAll Orders: {ex.Message}");
                return Json(new { success = false, message = ex.Message, data = new List<object>() });
            }
        }

        // ========================================
        // GET: /Admin/Orders/GetDetails/{id} - Chi tiết đơn hàng
        // ========================================
        [HttpGet("GetDetails/{id}")]
        public async Task<JsonResult> GetDetails(int id)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var orderQuery = @"
                        SELECT 
                            id,
                            customer_name AS customerName,
                            phone,
                            total,
                            status,
                            created_at AS createdAt,
                            updated_at AS updatedAt
                        FROM orders
                        WHERE id = @id";

                    object orderData = null;
                    using (var command = new MySqlCommand(orderQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                orderData = new
                                {
                                    id = reader.GetInt32("id"),
                                    customerName = reader.GetString("customerName"),
                                    phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? "" : reader.GetString("phone"),
                                    total = reader.GetDecimal("total"),
                                    status = reader.GetString("status"),
                                    createdAt = reader.GetDateTime("createdAt"),
                                    updatedAt = reader.IsDBNull(reader.GetOrdinal("updatedAt")) ? (DateTime?)null : reader.GetDateTime("updatedAt")
                                };
                            }
                        }
                    }

                    if (orderData == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                    }

                    var itemsQuery = @"
                        SELECT 
                            oi.id,
                            oi.product_id AS productId,
                            p.name AS productName,
                            p.image AS productImage,
                            oi.quantity,
                            oi.price
                        FROM order_items oi
                        INNER JOIN products p ON oi.product_id = p.id
                        WHERE oi.order_id = @orderId";

                    var items = new List<object>();
                    using (var command = new MySqlCommand(itemsQuery, connection))
                    {
                        command.Parameters.AddWithValue("@orderId", id);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                items.Add(new
                                {
                                    id = reader.GetInt32("id"),
                                    productId = reader.GetInt32("productId"),
                                    productName = reader.GetString("productName"),
                                    productImage = reader.IsDBNull(reader.GetOrdinal("productImage")) ? "" : reader.GetString("productImage"),
                                    quantity = reader.GetInt32("quantity"),
                                    price = reader.GetDecimal("price")
                                });
                            }
                        }
                    }

                    var result = new
                    {
                        success = true,
                        data = new
                        {
                            id = ((dynamic)orderData).id,
                            customerName = ((dynamic)orderData).customerName,
                            phone = ((dynamic)orderData).phone,
                            total = ((dynamic)orderData).total,
                            status = ((dynamic)orderData).status,
                            createdAt = ((dynamic)orderData).createdAt,
                            updatedAt = ((dynamic)orderData).updatedAt,
                            items = items
                        }
                    };
                    
                    Console.WriteLine($"✅ GetDetails Order #{id}: Found {items.Count} items");
                    return Json(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in GetDetails: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Orders/Create - TẠO ĐƠN HÀNG MỚI (AJAX - FIX GIÁ NHÂN ĐÔI)
        // ========================================
        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] SimpleOrderCreateModel model)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("📦 ORDER CREATE REQUEST (AJAX)");
            Console.WriteLine("========================================");
            
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }
            
            try
            {
                Console.WriteLine($"👤 Customer: {model.CustomerName}");
                Console.WriteLine($"📞 Phone: {model.Phone ?? "N/A"}");
                Console.WriteLine($"🛍️  Product ID: {model.ProductId}");
                Console.WriteLine($"📦 Quantity: {model.Quantity}");
                Console.WriteLine($"💵 Unit Price: {model.Price:N0}đ");

                // Validation
                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(model.CustomerName))
                {
                    errors.Add("Tên khách hàng không được để trống");
                }

                if (model.ProductId <= 0)
                {
                    errors.Add("Vui lòng chọn sản phẩm hợp lệ");
                }

                if (model.Quantity <= 0)
                {
                    errors.Add("Số lượng phải lớn hơn 0");
                }

                if (model.Price <= 0)
                {
                    errors.Add("Giá sản phẩm phải lớn hơn 0");
                }

                if (errors.Any())
                {
                    Console.WriteLine("❌ Validation failed:");
                    errors.ForEach(e => Console.WriteLine($"   - {e}"));
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // ========== CALCULATE TOTAL (FIX: Không nhân đôi) ==========
                // Client đã tính: Total = Price * Quantity
                // Server chỉ cần dùng giá đơn vị để lưu vào order_items
                decimal orderTotal = model.Price * model.Quantity;
                Console.WriteLine($"💰 Calculated Total: {orderTotal:N0}đ");

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine("✅ Database connected");
                    
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // ===== VERIFY PRODUCT EXISTS =====
                            var productQuery = "SELECT id, name, price FROM products WHERE id = @id AND is_active = 1";
                            string productName = "";
                            decimal dbPrice = 0;
                            
                            using (var command = new MySqlCommand(productQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@id", model.ProductId);
                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    if (!await reader.ReadAsync())
                                    {
                                        throw new Exception($"Sản phẩm ID {model.ProductId} không tồn tại hoặc đã tạm ngưng");
                                    }
                                    productName = reader.GetString("name");
                                    dbPrice = reader.GetDecimal("price");
                                }
                            }
                            
                            Console.WriteLine($"✅ Product verified: {productName} - {dbPrice:N0}đ");

                            // ===== INSERT ORDER =====
                            var orderQuery = @"
                                INSERT INTO orders (customer_name, phone, total, status, created_at)
                                VALUES (@customerName, @phone, @total, @status, NOW());
                                SELECT LAST_INSERT_ID();";

                            int orderId;
                            using (var command = new MySqlCommand(orderQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@customerName", model.CustomerName.Trim());
                                command.Parameters.AddWithValue("@phone", 
                                    string.IsNullOrWhiteSpace(model.Phone) ? DBNull.Value : (object)model.Phone.Trim());
                                command.Parameters.AddWithValue("@total", orderTotal);
                                command.Parameters.AddWithValue("@status", "pending");
                                
                                Console.WriteLine($"📝 Inserting order with total: {orderTotal:N0}đ");
                                var result = await command.ExecuteScalarAsync();
                                orderId = Convert.ToInt32(result);
                                Console.WriteLine($"✅ Order created: ID = {orderId}");
                            }

                            // ===== INSERT ORDER ITEM (FIX: Dùng giá đơn vị, không nhân lại) =====
                            var itemQuery = @"
                                INSERT INTO order_items (order_id, product_id, quantity, price, created_at)
                                VALUES (@orderId, @productId, @quantity, @price, NOW())";

                            using (var command = new MySqlCommand(itemQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@orderId", orderId);
                                command.Parameters.AddWithValue("@productId", model.ProductId);
                                command.Parameters.AddWithValue("@quantity", model.Quantity);
                                // FIX: Lưu giá đơn vị (price), không phải tổng tiền
                                command.Parameters.AddWithValue("@price", model.Price);
                                
                                Console.WriteLine($"📝 Inserting item: {model.Quantity}x @ {model.Price:N0}đ");
                                int itemRows = await command.ExecuteNonQueryAsync();
                                Console.WriteLine($"✅ Order item created: {itemRows} row(s)");
                            }

                            // ===== COMMIT =====
                            await transaction.CommitAsync();
                            Console.WriteLine("\n✅✅✅ TRANSACTION COMMITTED!");
                            Console.WriteLine("========================================\n");
                            
                            return Json(new 
                            { 
                                success = true, 
                                message = $"Tạo đơn hàng #{orderId} thành công!",
                                orderId = orderId,
                                customerName = model.CustomerName,
                                total = orderTotal
                            });
                        }
                        catch (MySqlException mysqlEx)
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine($"\n❌ MySQL Error: {mysqlEx.Message}");
                            Console.WriteLine("🔄 Transaction rolled back\n");
                            
                            return Json(new { success = false, message = $"Lỗi database: {mysqlEx.Message}" });
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine($"\n❌ Transaction Error: {ex.Message}");
                            Console.WriteLine("🔄 Transaction rolled back\n");
                            
                            return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌❌❌ FATAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                Console.WriteLine("========================================\n");
                
                return Json(new { success = false, message = $"Lỗi nghiêm trọng: {ex.Message}" });
            }
        }

        // ========================================
        // POST: /Admin/Orders/CreateJson - ĐƠN PHỨC TẠP (nhiều item)
        // ========================================
        [HttpPost("CreateJson")]
        public async Task<JsonResult> CreateJson([FromBody] OrderCreateModel model)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                if (string.IsNullOrWhiteSpace(model.CustomerName))
                {
                    return Json(new { success = false, message = "Tên khách hàng không được để trống" });
                }

                if (model.Items == null || model.Items.Count == 0)
                {
                    return Json(new { success = false, message = "Đơn hàng phải có ít nhất 1 sản phẩm" });
                }

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // FIX: Tính tổng từ giá đơn vị * số lượng
                            decimal total = model.Items.Sum(item => item.Price * item.Quantity);

                            if (total <= 0)
                            {
                                return Json(new { success = false, message = "Tổng tiền phải lớn hơn 0" });
                            }

                            var orderQuery = @"
                                INSERT INTO orders (customer_name, phone, total, status, created_at)
                                VALUES (@customerName, @phone, @total, @status, NOW());
                                SELECT LAST_INSERT_ID();";

                            int orderId;
                            using (var command = new MySqlCommand(orderQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@customerName", model.CustomerName.Trim());
                                command.Parameters.AddWithValue("@phone", 
                                    string.IsNullOrWhiteSpace(model.Phone) ? DBNull.Value : (object)model.Phone.Trim());
                                command.Parameters.AddWithValue("@total", total);
                                command.Parameters.AddWithValue("@status", model.Status ?? "pending");
                                
                                var result = await command.ExecuteScalarAsync();
                                orderId = Convert.ToInt32(result);
                            }

                            var itemQuery = @"
                                INSERT INTO order_items (order_id, product_id, quantity, price, created_at)
                                VALUES (@orderId, @productId, @quantity, @price, NOW())";

                            foreach (var item in model.Items)
                            {
                                using (var command = new MySqlCommand(itemQuery, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@orderId", orderId);
                                    command.Parameters.AddWithValue("@productId", item.ProductId);
                                    command.Parameters.AddWithValue("@quantity", item.Quantity);
                                    // FIX: Lưu giá đơn vị
                                    command.Parameters.AddWithValue("@price", item.Price);
                                    await command.ExecuteNonQueryAsync();
                                }
                            }

                            await transaction.CommitAsync();
                            Console.WriteLine($"✅ Order #{orderId} created via JSON");
                            return Json(new { success = true, message = "Tạo đơn hàng thành công", orderId = orderId });
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine($"❌ CreateJson Error: {ex.Message}");
                            return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CreateJson Error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Orders/Update - CẬP NHẬT ĐƠN HÀNG
        // ========================================
        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] OrderCreateModel model)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                if (model.Id <= 0)
                {
                    return Json(new { success = false, message = "ID đơn hàng không hợp lệ" });
                }

                if (string.IsNullOrWhiteSpace(model.CustomerName))
                {
                    return Json(new { success = false, message = "Tên khách hàng không được để trống" });
                }

                if (model.Items == null || model.Items.Count == 0)
                {
                    return Json(new { success = false, message = "Đơn hàng phải có ít nhất 1 sản phẩm" });
                }

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // FIX: Tính lại tổng từ giá đơn vị
                            decimal total = model.Items.Sum(item => item.Price * item.Quantity);

                            if (total <= 0)
                            {
                                return Json(new { success = false, message = "Tổng tiền phải lớn hơn 0" });
                            }

                            var orderQuery = @"
                                UPDATE orders 
                                SET customer_name = @customerName,
                                    phone = @phone,
                                    total = @total,
                                    status = @status,
                                    updated_at = NOW()
                                WHERE id = @id";

                            using (var command = new MySqlCommand(orderQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@id", model.Id);
                                command.Parameters.AddWithValue("@customerName", model.CustomerName.Trim());
                                command.Parameters.AddWithValue("@phone", 
                                    string.IsNullOrWhiteSpace(model.Phone) ? DBNull.Value : (object)model.Phone.Trim());
                                command.Parameters.AddWithValue("@total", total);
                                command.Parameters.AddWithValue("@status", model.Status ?? "pending");
                                
                                int rows = await command.ExecuteNonQueryAsync();
                                if (rows == 0)
                                {
                                    return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                                }
                            }

                            var deleteQuery = "DELETE FROM order_items WHERE order_id = @orderId";
                            using (var command = new MySqlCommand(deleteQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@orderId", model.Id);
                                await command.ExecuteNonQueryAsync();
                            }

                            var itemQuery = @"
                                INSERT INTO order_items (order_id, product_id, quantity, price, created_at)
                                VALUES (@orderId, @productId, @quantity, @price, NOW())";

                            foreach (var item in model.Items)
                            {
                                using (var command = new MySqlCommand(itemQuery, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@orderId", model.Id);
                                    command.Parameters.AddWithValue("@productId", item.ProductId);
                                    command.Parameters.AddWithValue("@quantity", item.Quantity);
                                    command.Parameters.AddWithValue("@price", item.Price);
                                    await command.ExecuteNonQueryAsync();
                                }
                            }

                            await transaction.CommitAsync();
                            Console.WriteLine($"✅ Order #{model.Id} updated");
                            return Json(new { success = true, message = "Cập nhật đơn hàng thành công" });
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine($"❌ Update Error: {ex.Message}");
                            return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Update Error: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Orders/UpdateStatus - CẬP NHẬT TRẠNG THÁI
        // ========================================
        [HttpPost("UpdateStatus")]
        public async Task<JsonResult> UpdateStatus([FromBody] StatusUpdateModel model)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                if (model.Id <= 0)
                {
                    return Json(new { success = false, message = "ID đơn hàng không hợp lệ" });
                }

                var validStatuses = new[] { "pending", "processing", "done", "cancelled" };
                if (!validStatuses.Contains(model.Status))
                {
                    return Json(new { success = false, message = "Trạng thái không hợp lệ" });
                }

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var query = @"
                        UPDATE orders 
                        SET status = @status,
                            updated_at = NOW()
                        WHERE id = @id";

                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@id", model.Id);
                        command.Parameters.AddWithValue("@status", model.Status);
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"✅ Order #{model.Id} status → {model.Status}");
                            return Json(new { success = true, message = "Cập nhật trạng thái thành công" });
                        }
                        
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UpdateStatus Error: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // ========================================
        // POST: /Admin/Orders/Delete/{id} - XÓA ĐƠN HÀNG
        // ========================================
        [HttpPost("Delete/{id}")]
        public async Task<JsonResult> Delete(int id)
        {
            if (!CheckAuth())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                if (id <= 0)
                {
                    return Json(new { success = false, message = "ID không hợp lệ" });
                }

                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var checkQuery = "SELECT status FROM orders WHERE id = @id";
                    string status = null;
                    
                    using (var command = new MySqlCommand(checkQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);
                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            status = result.ToString();
                        }
                    }
                    
                    if (status == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                    }
                    
                    if (status == "done")
                    {
                        return Json(new { success = false, message = "Không thể xóa đơn hàng đã hoàn thành" });
                    }
                    
                    var deleteQuery = "DELETE FROM orders WHERE id = @id";
                    
                    using (var command = new MySqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        
                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"✅ Order #{id} deleted");
                            return Json(new { success = true, message = "Xóa đơn hàng thành công" });
                        }
                        
                        return Json(new { success = false, message = "Không thể xóa đơn hàng" });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Delete Error: {ex.Message}");
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa đơn hàng" });
            }
        }
    }

    // ========================================
    // MODELS
    // ========================================
    public class SimpleOrderCreateModel
    {
        public string CustomerName { get; set; }
        public string? Phone { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; } // Giá đơn vị
        public string? Note { get; set; }
    }

    public class OrderCreateModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string? Phone { get; set; }
        public string? Status { get; set; }
        public List<OrderItemModel> Items { get; set; }
    }

    public class OrderItemModel
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; } // Giá đơn vị
    }

    public class StatusUpdateModel
    {
        public int Id { get; set; }
        public string Status { get; set; }
    }
}