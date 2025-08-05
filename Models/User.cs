using System.ComponentModel.DataAnnotations;

namespace Nikii_Pic.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string UserName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;
        
        public bool IsAdmin { get; set; }
        
        public bool IsApproved { get; set; }
        
        public DateTime RegisterTime { get; set; }
        
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;
        
        // 导航属性
        public ICollection<Image> Images { get; set; } = new List<Image>();
    }
} 