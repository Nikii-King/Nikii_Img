using Microsoft.AspNetCore.Mvc;
using Nikii_Pic.Data;
using Nikii_Pic.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;

namespace Nikii_Pic.Controllers
{
    public class ImageController : Controller
    {
        private readonly NikiiPicContext _context;
        private readonly string _uploadPath;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public ImageController(NikiiPicContext context, IWebHostEnvironment env, IConfiguration config)
        {
            _context = context;
            _env = env;
            _config = config;
            _uploadPath = Path.Combine(env.WebRootPath, "uploads");
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null) return RedirectToAction("Login", "Account");
            ViewBag.Folders = user.IsAdmin
                ? await _context.ImageFolder.ToListAsync()
                : await _context.ImageFolder.Where(f => f.UserId == user.Id || f.IsShared).ToListAsync();
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string? imageSource, string? imageCategory, int? folderId)
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null) return RedirectToAction("Login", "Account");
            ViewBag.Folders = user.IsAdmin
                ? await _context.ImageFolder.ToListAsync()
                : await _context.ImageFolder.Where(f => f.UserId == user.Id || f.IsShared).ToListAsync();
            if (file == null || file.Length == 0)
            {
                ViewBag.Message = "请选择要上传的图片。";
                return View();
            }

            // 验证文件类型
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                ViewBag.Message = "只支持上传图片文件（JPG、PNG、GIF、BMP、WEBP）。";
                return View();
            }

            // 验证文件大小（10MB限制）
            if (file.Length > 10 * 1024 * 1024)
            {
                ViewBag.Message = "图片大小不能超过10MB。";
                return View();
            }

            // 获取当前存储方式
            var storageSetting = await _context.StorageSetting.OrderByDescending(s => s.UpdateTime).FirstOrDefaultAsync();
            var storageType = storageSetting?.StorageType ?? "File";

            var domain = _config["HomePage:Domain"];

            if (storageType == "Database")
            {
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    var image = new Image
                    {
                        UserId = user.Id,
                        FileName = file.FileName,
                        FilePath = "", // 数据库存储无需路径
                        UploadTime = DateTime.Now,
                        FileSize = file.Length,
                        FileType = file.ContentType,
                        ImageSource = imageSource,
                        ImageCategory = imageCategory,
                        ImageUrl = "", // 先留空，保存后再赋值
                        IsDeleted = false,
                        FolderId = folderId,
                        Data = ms.ToArray()
                    };
                    _context.Images.Add(image);
                    await _context.SaveChangesAsync();
                    // 保存ImageUrl为Show接口绝对地址
                    image.ImageUrl = (string.IsNullOrEmpty(domain)
                        ? Url.Action("Show", "Image", new { id = image.Id }, Request.Scheme, Request.Host.ToString())
                        : domain.TrimEnd('/') + "/Image/Show/" + image.Id);
                    await _context.SaveChangesAsync();
                    ViewBag.Message = "图片上传成功！（已存入数据库）";
                    ViewBag.ImageUrl = image.ImageUrl;
                    return View();
                }
            }
            else // File
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                var image = new Image
                {
                    UserId = user.Id,
                    FileName = file.FileName,
                    FilePath = "/uploads/" + fileName,
                    UploadTime = DateTime.Now,
                    FileSize = file.Length,
                    FileType = file.ContentType,
                    ImageSource = imageSource,
                    ImageCategory = imageCategory,
                    ImageUrl = "", // 先留空，保存后再赋值
                    IsDeleted = false,
                    FolderId = folderId,
                    Data = null
                };
                _context.Images.Add(image);
                await _context.SaveChangesAsync();
                image.ImageUrl = (string.IsNullOrEmpty(domain)
                    ? Url.Action("Show", "Image", new { id = image.Id }, Request.Scheme, Request.Host.ToString())
                    : domain.TrimEnd('/') + "/Image/Show/" + image.Id);
                await _context.SaveChangesAsync();
                ViewBag.Message = "图片上传成功！";
                ViewBag.ImageUrl = image.ImageUrl;
                return View();
            }
        }

        [Authorize]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 12, string? category = null, int? folderId = null)
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            IQueryable<Image> query;
            if (user.IsAdmin)
            {
                query = _context.Images.Where(i => !i.IsDeleted).OrderByDescending(i => i.UploadTime);
            }
            else
            {
                query = _context.Images.Where(i => (!i.IsDeleted) && (i.UserId == user.Id || (i.FolderId != null && _context.ImageFolder.Any(f => f.Id == i.FolderId && f.IsShared)))).OrderByDescending(i => i.UploadTime);
            }
            // 文件夹筛选
            if (folderId.HasValue)
            {
                query = query.Where(i => i.FolderId == folderId);
            }
            // 分类筛选
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(i => (i.ImageCategory ?? "") == category);
            }
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            var images = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Category = category;
            ViewBag.FolderId = folderId;
            ViewBag.Folders = await _context.ImageFolder.Where(f => f.UserId == user.Id || f.IsShared).ToListAsync();
            ViewBag.Categories = await _context.Images
                .Where(i => (user.IsAdmin || i.UserId == user.Id) && !i.IsDeleted && !string.IsNullOrEmpty(i.ImageCategory))
                .Select(i => i.ImageCategory)
                .Distinct()
                .ToListAsync();
            return View(images);
        }

        [Authorize]
        public async Task<IActionResult> Detail(int id)
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            var image = await _context.Images.Include(i => i.Folder)
                .FirstOrDefaultAsync(i => i.Id == id && (user.IsAdmin || i.UserId == user.Id) && !i.IsDeleted);
            if (image == null)
            {
                return NotFound();
            }
            ViewBag.Folders = user.IsAdmin
                ? await _context.ImageFolder.ToListAsync()
                : await _context.ImageFolder.Where(f => f.UserId == user.Id || f.IsShared).ToListAsync();
            return View(image);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Update(int id, string? imageSource, string? imageCategory, int? folderId)
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            var image = await _context.Images.FirstOrDefaultAsync(i => i.Id == id && (user.IsAdmin || i.UserId == user.Id) && !i.IsDeleted);
            if (image == null)
            {
                return NotFound();
            }
            image.ImageSource = imageSource;
            image.ImageCategory = imageCategory;
            image.FolderId = folderId;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Detail), new { id });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            var image = user.IsAdmin
                ? await _context.Images.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted)
                : await _context.Images.FirstOrDefaultAsync(i => i.Id == id && i.UserId == user.Id && !i.IsDeleted);
            if (image == null)
            {
                return NotFound();
            }
            image.IsDeleted = true;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> BulkDelete(List<int> selectedIds)
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            if (selectedIds != null && selectedIds.Count > 0)
            {
                var images = user.IsAdmin
                    ? await _context.Images.Where(i => selectedIds.Contains(i.Id) && !i.IsDeleted).ToListAsync()
                    : await _context.Images.Where(i => selectedIds.Contains(i.Id) && i.UserId == user.Id && !i.IsDeleted).ToListAsync();
                foreach (var img in images)
                {
                    img.IsDeleted = true;
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> BulkUpload()
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null) return RedirectToAction("Login", "Account");
            ViewBag.Folders = user.IsAdmin
                ? await _context.ImageFolder.ToListAsync()
                : await _context.ImageFolder.Where(f => f.UserId == user.Id || f.IsShared).ToListAsync();
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> BulkUpload(List<IFormFile> files, List<string> imageSources, List<string> imageCategories, List<int?> folderIds)
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null) return RedirectToAction("Login", "Account");
            ViewBag.Folders = user.IsAdmin
                ? await _context.ImageFolder.ToListAsync()
                : await _context.ImageFolder.Where(f => f.UserId == user.Id || f.IsShared).ToListAsync();
            // 获取当前存储方式
            var storageSetting = await _context.StorageSetting.OrderByDescending(s => s.UpdateTime).FirstOrDefaultAsync();
            var storageType = storageSetting?.StorageType ?? "File";
            if (files != null && files.Count > 0)
            {
                var newImages = new List<Image>();
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file != null && file.Length > 0)
                    {
                        if (storageType == "Database")
                        {
                            using (var ms = new MemoryStream())
                            {
                                await file.CopyToAsync(ms);
                                var image = new Image
                                {
                                    UserId = user.Id,
                                    FileName = file.FileName,
                                    FilePath = "",
                                    UploadTime = DateTime.Now,
                                    FileSize = file.Length,
                                    FileType = file.ContentType,
                                    ImageSource = imageSources.Count > i ? imageSources[i] : null,
                                    ImageCategory = imageCategories.Count > i ? imageCategories[i] : null,
                                    ImageUrl = "",
                                    IsDeleted = false,
                                    FolderId = folderIds.Count > i ? folderIds[i] : null,
                                    Data = ms.ToArray()
                                };
                                _context.Images.Add(image);
                                newImages.Add(image);
                            }
                        }
                        else // File
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);
                        var filePath = Path.Combine(uploadPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        var image = new Image
                        {
                            UserId = user.Id,
                            FileName = file.FileName,
                                FilePath = $"/uploads/{fileName}",
                            UploadTime = DateTime.Now,
                            FileSize = file.Length,
                            FileType = file.ContentType,
                            ImageSource = imageSources.Count > i ? imageSources[i] : null,
                            ImageCategory = imageCategories.Count > i ? imageCategories[i] : null,
                                ImageUrl = "",
                            IsDeleted = false,
                                FolderId = folderIds.Count > i ? folderIds[i] : null,
                                Data = null
                        };
                        _context.Images.Add(image);
                            newImages.Add(image);
                        }
                    }
                }
                await _context.SaveChangesAsync();
                // 批量生成ImageUrl
                foreach (var img in newImages)
                {
                    // 获取主域名
                    var domain = _config["HomePage:Domain"];
                    img.ImageUrl = (string.IsNullOrEmpty(domain) ? Url.Action("Show", "Image", new { id = img.Id }, Request.Scheme, Request.Host.ToString()) :
                        domain.TrimEnd('/') + "/Image/Show/" + img.Id);
                }
                await _context.SaveChangesAsync();
                TempData["BulkUploadMsg"] = $"成功上传{files.Count}张图片！";
            }
            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> FolderList()
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null) return RedirectToAction("Login", "Account");
            var folders = user.IsAdmin
                ? await _context.ImageFolder.OrderByDescending(f => f.CreateTime).ToListAsync()
                : await _context.ImageFolder.Where(f => f.UserId == user.Id).OrderByDescending(f => f.CreateTime).ToListAsync();
            return View(folders);
        }

        [Authorize]
        public IActionResult CreateFolder()
        {
            var userName = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.UserName == userName);
            if (user == null || !user.IsAdmin)
            {
                return Forbid();
            }
            return View();
        }

        [Authorize]
        [HttpPost]
        public IActionResult CreateFolder(string folderName, bool isShared)
        {
            var userName = User.Identity?.Name;
            var user = _context.Users.FirstOrDefault(u => u.UserName == userName);
            if (user == null || !user.IsAdmin)
            {
                return Forbid();
            }
            var folder = new ImageFolder
            {
                FolderName = folderName,
                IsShared = isShared,
                UserId = user.Id,
                CreateTime = DateTime.Now
            };
            _context.ImageFolder.Add(folder);
            _context.SaveChanges();
            return RedirectToAction("FolderList");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditFolder(int id)
        {
            var folder = await _context.ImageFolder.FindAsync(id);
            return View(folder);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> EditFolder(int id, string folderName, bool isShared)
        {
            var folder = await _context.ImageFolder.FindAsync(id);
            if (folder != null)
            {
                folder.FolderName = folderName;
                folder.IsShared = isShared;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("FolderList");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> DeleteFolder(int id)
        {
            var folder = await _context.ImageFolder.FindAsync(id);
            if (folder != null)
            {
                _context.ImageFolder.Remove(folder);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("FolderList");
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Show(int id)
        {
            var image = await _context.Images.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            if (image == null)
                return NotFound();
            // 判断存储方式
            var storageSetting = await _context.StorageSetting.OrderByDescending(s => s.UpdateTime).FirstOrDefaultAsync();
            var storageType = storageSetting?.StorageType ?? "File";
            if (storageType == "Database" || (!string.IsNullOrEmpty(image.FilePath) && string.IsNullOrEmpty(image.Data?.ToString()) == false))
            {
                if (image.Data == null)
                    return NotFound();
                return File(image.Data, image.FileType ?? "image/jpeg");
            }
            else // File
            {
                var filePath = Path.Combine(_env.WebRootPath, image.FilePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (!System.IO.File.Exists(filePath))
                    return NotFound();
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, image.FileType ?? "image/jpeg");
            }
        }
        #region  ocr识别
        #endregion
    }
} 