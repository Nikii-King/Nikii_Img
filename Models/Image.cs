using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nikii_Pic.Models
{
    [Table("Image")]
    public class Image
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        [Required]
        public DateTime UploadTime { get; set; }
        
        [Required]
        public long FileSize { get; set; }
        
        [Required]
        [StringLength(50)]
        public string FileType { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? ImageSource { get; set; }
        
        [StringLength(100)]
        public string? ImageCategory { get; set; }
        
        [StringLength(500)]
        public string? ImageUrl { get; set; }
        
        public bool IsDeleted { get; set; }
        
        public int? FolderId { get; set; } // 所属文件夹ID，可为空
        
        public byte[]? Data { get; set; } // 数据库存储图片内容
        
        // 导航属性
        public User? User { get; set; }
        public ImageFolder? Folder { get; set; }
    }
} 