using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nikii_Pic.Models
{
    public class ImageFolder
    {
        [Key]
        public int Id { get; set; } // 文件夹主键，自增

        [Required]
        public int UserId { get; set; } // 所属用户ID

        [Required]
        [StringLength(100)]
        public string FolderName { get; set; } = string.Empty; // 文件夹名称

        public bool IsShared { get; set; } // 是否共享

        public DateTime CreateTime { get; set; } // 创建时间

        // 导航属性
        public User? User { get; set; }
        public ICollection<Image> Images { get; set; } = new List<Image>();
    }
} 