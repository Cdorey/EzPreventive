using EzNutrition.Server.Data.Entities;
using EzNutrition.Shared.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EzNutrition.Server.Data
{
    public class ApplicationDbContext(DbContextOptions options) : IdentityDbContext(options)
    {
        public DbSet<Notice> Notices { get; set; }

        public DbSet<ProfessionalCertificationRequest> ProfessionalCertificationRequests { get; set; }
    }
}