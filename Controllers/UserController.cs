using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nikii_Pic.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Nikii_Pic.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly NikiiPicContext _context;
        public UserController(NikiiPicContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Profile()
        {
            var userName = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
            if (user == null) return RedirectToAction("Login", "Account");
            var imageCount = await _context.Images.CountAsync(i => i.UserId == user.Id && !i.IsDeleted);
            var deletedCount = await _context.Images.CountAsync(i => i.UserId == user.Id && i.IsDeleted);
            var folderCount = await _context.ImageFolder.CountAsync(f => f.UserId == user.Id);
            ViewBag.ImageCount = imageCount;
            ViewBag.DeletedCount = deletedCount;
            ViewBag.FolderCount = folderCount;
            return View(user);
        }
    }
} 