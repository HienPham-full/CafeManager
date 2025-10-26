using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeWeb.Models
{
    [Table("payments")]
    public class Payment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [Column("amount", TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("payment_method")]
        public string PaymentMethod { get; set; } = "cash";

        [Column("paid_by")]
        public int? PaidBy { get; set; }

        [Column("paid_at")]
        public DateTime PaidAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        [ForeignKey("PaidBy")]
        public User? PaidByUser { get; set; }
    }
}