using EzASD.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Syroot.Windows.IO;
using System.IO;

namespace EzASD.Data
{
    public class DataContext : DbContext
    {
        public DataContext()
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var docPath = KnownFolders.Documents.Path;
            var dbPath = Path.Combine(docPath, "EzAsdRecords.db");
            // connect to sqlite database
            options.UseSqlite($"Data Source={dbPath}");
        }

        public DbSet<Child>? Children { get; set; }
    }
}
