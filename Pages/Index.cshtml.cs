using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Nikii_Pic.Data;
using Nikii_Pic.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Nikii_Pic.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly NikiiPicContext _context;
        private readonly IConfiguration _config;
        public string LogoText { get; set; } = "";
        public string SystemTitle { get; set; } = "";
        public string Announcement { get; set; } = "";

        public IndexModel(ILogger<IndexModel> logger, NikiiPicContext context, IConfiguration config)
        {
            _logger = logger;
            _context = context;
            _config = config;
        }

        public List<Image> LatestImages { get; set; } = new List<Image>();

        public void OnGet()
        {
            // 获取最新的12张未删除图片
            LatestImages = _context.Images
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.UploadTime)
                .Take(12)
                .ToList();
            // 读取首页动态文字
            LogoText = _config["HomePage:LogoText"] ?? "Nikii Pic";
            SystemTitle = _config["HomePage:SystemTitle"] ?? "Nikii 图床管理系统";
            Announcement = _config["HomePage:Announcement"] ?? "欢迎使用Nikii图床！";
        }
    }
}
