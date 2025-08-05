using Microsoft.AspNetCore.Mvc;
using Nikii_Pic.Data;
using Nikii_Pic.Models;
using Nikii_Pic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Nikii_Pic.Controllers
{
    public class AccountController : Controller
    {
        private readonly NikiiPicContext _context;
        private readonly MailService _mailService;

        public AccountController(NikiiPicContext context, MailService mailService)
        {
            _context = context;
            _mailService = mailService;
        }

        private string Sha256Base64(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string captcha)
        {
            var sessionCaptcha = HttpContext.Session.GetString("ImgCode");
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (string.IsNullOrEmpty(sessionCaptcha) || sessionCaptcha != captcha)
            {
                if (isAjax)
                    return Json(new { success = false, msg = "图形验证码错误" });
                ViewBag.Error = "图形验证码错误";
                return View();
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (user == null || !user.IsApproved)
            {
                if (isAjax)
                    return Json(new { success = false, msg = user == null ? "用户不存在" : "账号未审核通过" });
                ViewBag.Error = user == null ? "用户不存在" : "账号未审核通过";
                return View();
            }
            var hash = Sha256Base64(password);
            if (user.PasswordHash == hash)
            {
                // 登录成功，写入Cookie
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
                    new Claim("UserId", user.Id.ToString())
                };
                var claimsIdentity = new ClaimsIdentity(claims, "MyCookie");
                await HttpContext.SignInAsync("MyCookie", new ClaimsPrincipal(claimsIdentity));
                if (isAjax)
                    return Json(new { success = true });
                return RedirectToAction("Index", "Home");
            }
            if (isAjax)
                return Json(new { success = false, msg = "用户名或密码错误" });
            ViewBag.Error = "用户名或密码错误";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string password, string email, string code, string captcha)
        {
            // 不再校验确认密码
            if (await _context.Users.AnyAsync(u => u.UserName == username))
                return Json(new { success = false, msg = "用户名已存在" });
            if (await _context.Users.AnyAsync(u => u.Email == email))
                return Json(new { success = false, msg = "邮箱已被注册" });
            // 校验图形验证码
            var sessionCaptcha = HttpContext.Session.GetString("ImgCode");
            if (string.IsNullOrEmpty(sessionCaptcha) || sessionCaptcha != captcha)
                return Json(new { success = false, msg = "图形验证码错误" });
            // 校验邮箱验证码
            var sessionCode = HttpContext.Session.GetString("RegisterCode");
            var sessionEmail = HttpContext.Session.GetString("RegisterEmail");
            var expireStr = HttpContext.Session.GetString("RegisterCodeExpire");
            var used = HttpContext.Session.GetString("RegisterCodeUsed");
            if (string.IsNullOrEmpty(sessionCode) || string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(expireStr) || used == "1")
                return Json(new { success = false, msg = "邮箱验证码无效或已用" });
            if (sessionEmail != email)
                return Json(new { success = false, msg = "邮箱与验证码不匹配" });
            if (sessionCode != code)
                return Json(new { success = false, msg = "邮箱验证码错误" });
            if (DateTime.TryParse(expireStr, out var expire) && DateTime.Now > expire)
                return Json(new { success = false, msg = "邮箱验证码已过期" });
            // 标记为已用
            HttpContext.Session.SetString("RegisterCodeUsed", "1");
            var user = new User
            {
                UserName = username,
                Email = email,
                IsAdmin = false,
                IsApproved = false, // 未审核
                RegisterTime = DateTime.Now
            };
            user.PasswordHash = Sha256Base64(password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            // 清除验证码
            HttpContext.Session.Remove("RegisterCode");
            HttpContext.Session.Remove("RegisterEmail");
            HttpContext.Session.Remove("RegisterCodeExpire");
            HttpContext.Session.Remove("RegisterCodeUsed");
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SendRegisterCode(string email, string captcha)
        {
            // 校验Session中的图形验证码
            var sessionCaptcha = HttpContext.Session.GetString("ImgCode");
            if (string.IsNullOrEmpty(sessionCaptcha) || sessionCaptcha != captcha)
            {
                return Json(new { success = false, msg = "图形验证码错误" });
            }
            try
            {
                // 生成验证码
                var code = new Random().Next(100000, 999999).ToString();
                var expire = DateTime.Now.AddMinutes(5);
                // 保存验证码、邮箱、过期时间、已用状态到Session
                HttpContext.Session.SetString("RegisterCode", code);
                HttpContext.Session.SetString("RegisterEmail", email);
                HttpContext.Session.SetString("RegisterCodeExpire", expire.ToString("O"));
                HttpContext.Session.SetString("RegisterCodeUsed", "0");
                await _mailService.SendAsync(email, "注册验证码", $"您的注册验证码为：<b>{code}</b>，5分钟内有效。");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "发送邮件失败：" + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult VerifyRegisterCode(string email, string code)
        {
            var sessionCode = HttpContext.Session.GetString("RegisterCode");
            var sessionEmail = HttpContext.Session.GetString("RegisterEmail");
            var expireStr = HttpContext.Session.GetString("RegisterCodeExpire");
            var used = HttpContext.Session.GetString("RegisterCodeUsed");
            if (string.IsNullOrEmpty(sessionCode) || string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(expireStr) || used == "1")
                return Json(new { success = false, msg = "验证码无效或已用" });
            if (sessionEmail != email)
                return Json(new { success = false, msg = "邮箱不匹配" });
            if (sessionCode != code)
                return Json(new { success = false, msg = "验证码错误" });
            if (DateTime.TryParse(expireStr, out var expire) && DateTime.Now > expire)
                return Json(new { success = false, msg = "验证码已过期" });
            // 标记为已用
            HttpContext.Session.SetString("RegisterCodeUsed", "1");
            return Json(new { success = true });
        }

        // 用户审核中心（仅管理员）
        public async Task<IActionResult> Review(bool showHistory = false)
        {
            // 这里只做简单演示，实际应做登录和权限校验
            if (showHistory)
            {
                var users = await _context.Users.Where(u => u.IsApproved).OrderByDescending(u => u.RegisterTime).ToListAsync();
                return View("ReviewHistory", users);
            }
            else
            {
                var users = await _context.Users.Where(u => !u.IsApproved).OrderByDescending(u => u.RegisterTime).ToListAsync();
                return View(users);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null && !user.IsApproved)
            {
                user.IsApproved = true;
                await _context.SaveChangesAsync();
                await _mailService.SendAsync(user.Email, "审核通过通知", $"您的账号已通过审核，可以登录使用系统。");
            }
            return RedirectToAction("Review");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null && !user.IsApproved)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                await _mailService.SendAsync(user.Email, "审核未通过通知", $"很抱歉，您的账号未通过审核。如有疑问请联系管理员。");
            }
            return RedirectToAction("Review");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("MyCookie");
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult GetCaptcha()
        {
            var code = new Random().Next(1000, 9999).ToString();
            HttpContext.Session.SetString("ImgCode", code);
            return Json(new { code });
        }

        // 存储方式管理（仅管理员）
        [HttpGet]
        public async Task<IActionResult> StorageSetting()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (user == null || !user.IsAdmin)
                return Unauthorized();
            var setting = await _context.StorageSetting.OrderByDescending(s => s.UpdateTime).FirstOrDefaultAsync();
            ViewBag.StorageType = setting?.StorageType ?? "File";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> StorageSetting(string storageType)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
            if (user == null || !user.IsAdmin)
                return Unauthorized();
            var setting = new Models.StorageSetting
            {
                StorageType = storageType,
                UpdateTime = DateTime.Now
            };
            _context.StorageSetting.Add(setting);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult SiteConfig()
        {
            var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (user == null || !user.IsAdmin)
                return Unauthorized();
            // 读取配置
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            var json = System.IO.File.ReadAllText(configPath);
            var jObj = JObject.Parse(json);
            var homePage = jObj["HomePage"];
            ViewBag.LogoText = homePage?["LogoText"]?.ToString() ?? "";
            ViewBag.SystemTitle = homePage?["SystemTitle"]?.ToString() ?? "";
            ViewBag.Announcement = homePage?["Announcement"]?.ToString() ?? "";
            ViewBag.Footer = homePage?["Footer"]?.ToString() ?? "";
            ViewBag.Domain = homePage?["Domain"]?.ToString() ?? "";
            return View();
        }

        [HttpPost]
        public IActionResult SiteConfig(string logoText, string systemTitle, string announcement, string footer, string domain)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (user == null || !user.IsAdmin)
                return Unauthorized();
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            var json = System.IO.File.ReadAllText(configPath);
            var jObj = JObject.Parse(json);
            if (jObj["HomePage"] == null) jObj["HomePage"] = new JObject();
            jObj["HomePage"]["LogoText"] = logoText;
            jObj["HomePage"]["SystemTitle"] = systemTitle;
            jObj["HomePage"]["Announcement"] = announcement;
            jObj["HomePage"]["Footer"] = footer;
            jObj["HomePage"]["Domain"] = domain;
            System.IO.File.WriteAllText(configPath, jObj.ToString());
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult FixAllImageUrls()
        {
            var user = _context.Users.FirstOrDefault(u => u.UserName == User.Identity.Name);
            if (user == null || !user.IsAdmin)
                return Unauthorized();
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            var json = System.IO.File.ReadAllText(configPath);
            var jObj = JObject.Parse(json);
            var domain = jObj["HomePage"]?["Domain"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(domain))
                return Json(new { success = false, msg = "请先在系统配置中设置主域名！" });
            domain = domain.TrimEnd('/');
            var images = _context.Images.Where(i => !i.IsDeleted).ToList();
            foreach (var img in images)
            {
                img.ImageUrl = domain + "/Image/Show/" + img.Id;
            }
            _context.SaveChanges();
            return Json(new { success = true, count = images.Count });
        }
    }
} 