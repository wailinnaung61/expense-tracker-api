using expense_tracker_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Seed;

public static class DatabaseSeeder
{
    public static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed sample data here
        // Example for MemberProfile:
        // modelBuilder.Entity<MemberProfile>().HasData(
        //     new MemberProfile
        //     {
        //         Id = "seed-user-1",
        //         Email = "admin@example.com",
        //         Username = "admin",
        //         CreatedAt = DateTime.UtcNow
        //     }
        // );

        // Add more seed data as needed
    }
}
