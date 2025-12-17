using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // Bunu ekle
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using Microsoft.AspNetCore.Identity; // Guid için lazım

namespace TeknikServis.Data.Context
{
    // IdentityDbContext<AppUser, IdentityRole<Guid>, Guid> şeklinde tanımlıyoruz
    public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<ServiceTicket> ServiceTickets { get; set; }
        public DbSet<DeviceType> DeviceTypes { get; set; }
        public DbSet<DeviceBrand> DeviceBrands { get; set; }
        public DbSet<TicketPhoto> TicketPhotos { get; set; }
        public DbSet<SparePart> SpareParts { get; set; }
        public DbSet<ServiceTicketPart> ServiceTicketParts { get; set; }
        public DbSet<UserBranch> UserBranches { get; set; }
        public DbSet<CompanySetting> CompanySettings { get; set; }
        public DbSet<SmsSetting> SmsSettings { get; set; }
        public DbSet<BranchInfo> BranchInfos { get; set; }
        public DbSet<PriceOffer> PriceOffers { get; set; }
        public DbSet<PriceOfferItem> PriceOfferItems { get; set; }

        public DbSet<CustomerMovement> CustomerMovements { get; set; }
        public DbSet<ReceiptSetting> ReceiptSettings { get; set; }
        public DbSet<EmailSetting> EmailSettings { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<SupportRequest> SupportRequests { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        // AppUsers dbset'ine gerek yok, IdentityDbContext içinde "Users" olarak zaten var.

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Identity tablolarının oluşması için bu şart!
            base.OnModelCreating(modelBuilder);

            // Mevcut ilişkilerimiz aynen duruyor
            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Branch)
                .WithMany(b => b.Employees)
                .HasForeignKey(u => u.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Customer>()
                .HasOne(c => c.Branch)
                .WithMany(b => b.Customers)
                .HasForeignKey(c => c.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserBranch>()
                 .HasIndex(ub => new { ub.UserId, ub.BranchId })
                 .IsUnique();

            modelBuilder.Entity<UserBranch>()
                .HasOne(ub => ub.AppUser)
                .WithMany(u => u.AuthorizedBranches)
                .HasForeignKey(ub => ub.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserBranch>()
                .HasOne(ub => ub.Branch)
                .WithMany()
                .HasForeignKey(ub => ub.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}