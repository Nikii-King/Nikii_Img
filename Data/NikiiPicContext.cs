using Microsoft.EntityFrameworkCore;
using Nikii_Pic.Models;

namespace Nikii_Pic.Data
{
    public class NikiiPicContext : DbContext
    {
        public NikiiPicContext(DbContextOptions<NikiiPicContext> options)
            : base(options)
        {
        }

        public DbSet<Image> Images { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ImageFolder> ImageFolder { get; set; }
        public DbSet<StorageSetting> StorageSetting { get; set; }
    }
}