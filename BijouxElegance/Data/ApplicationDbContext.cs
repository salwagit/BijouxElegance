using BijouxElegance.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace BijouxElegance.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed data
            modelBuilder.Entity<Category>().HasData(
                new Category { CategoryId = 1, Name = "Colliers", Description = "Colliers en or et argent", IconClass = "fas fa-gem" },
                new Category { CategoryId = 2, Name = "Bracelets", Description = "Bracelets élégants", IconClass = "fas fa-ring" },
                new Category { CategoryId = 3, Name = "Bagues", Description = "Bagues de toutes sortes", IconClass = "fas fa-gem" },
                new Category { CategoryId = 4, Name = "Boucles d'oreilles", Description = "Boucles d'oreilles raffinées", IconClass = "fas fa-star" }
            );
        }
    }
}