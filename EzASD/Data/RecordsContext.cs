using EzASD.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Syroot.Windows.IO;
using System.IO;

namespace EzASD.Data
{
    public class RecordsContext : DbContext
    {
        public RecordsContext()
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

        public DbSet<PositiveSignRecord>? PositiveSignRecords { get; set; }

        public DbSet<Chat23aRecord>? Chat23aRecords { get; set; }
    }
}
