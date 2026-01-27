using EntityLayer.Concrete;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DataAcsessLayer.Concrete.Context
{
    public class VpnDbContext : DbContext
    {
        public VpnDbContext(DbContextOptions<VpnDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<VpnServer> VpnServers { get; set; }
        public DbSet<UserVpn> UserVpns { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);
            modelBuilder.Entity<UserVpn>()
    .HasOne(x => x.User)
    .WithMany()
    .HasForeignKey(x => x.UserId);

            modelBuilder.Entity<UserVpn>()
                .HasOne(x => x.VpnServer)
                .WithMany()
                .HasForeignKey(x => x.VpnServerId);

            modelBuilder.Entity<Subscription>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);
        }
    }

}
