using System;

namespace Nikii_Pic.Models
{
    public class StorageSetting
    {
        public int Id { get; set; }
        public string StorageType { get; set; } // 'File' 或 'Database'
        public DateTime UpdateTime { get; set; }
    }
} 