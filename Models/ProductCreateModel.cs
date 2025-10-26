using Microsoft.AspNetCore.Http;

namespace CafeWeb.Models
{
    public class ProductCreateModel
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public IFormFile ImageFile { get; set; }
    }

    public class ProductUpdateModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public IFormFile ImageFile { get; set; }
    }
}