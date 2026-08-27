using AgendaBuddy.Library.Entities;
using MongoDB.Bson;

namespace AgendaBuddy.Library.Data;

public static class DevelopmentSeedData
{
    public static List<ProviderEntity> Providers() =>
    [
        new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Sarah",
            LastName = "Mitchell",
            Email = "sarah.mitchell@agendabuddy.dev",
            IsActive = true,
            ServiceEntities =
            [
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Personal Training Session",
                    Description = "One-on-one fitness coaching tailored to your goals",
                    Fee = 75.00m,
                    FeeType = FeeType.Hourly,
                    IsActive = true
                },
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Group Fitness Class",
                    Description = "High-energy group workout for up to 10 participants",
                    Fee = 25.00m,
                    FeeType = FeeType.Fixed,
                    IsActive = true
                },
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Monthly Coaching Plan",
                    Description = "4 sessions per month with nutrition guidance and progress tracking",
                    Fee = 250.00m,
                    FeeType = FeeType.Subscription,
                    IsActive = true
                }
            ]
        },
        new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "James",
            LastName = "Okafor",
            Email = "james.okafor@agendabuddy.dev",
            IsActive = true,
            ServiceEntities =
            [
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Python Tutoring",
                    Description = "Beginner to intermediate Python programming lessons",
                    Fee = 60.00m,
                    FeeType = FeeType.Hourly,
                    IsActive = true
                },
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Full-Stack Bootcamp Prep",
                    Description = "Intensive prep for coding bootcamp interviews and assessments",
                    Fee = 500.00m,
                    FeeType = FeeType.Fixed,
                    IsActive = true
                },
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Weekly Code Review",
                    Description = "Weekly 1:1 code review sessions with mentorship",
                    Fee = 200.00m,
                    FeeType = FeeType.Subscription,
                    IsActive = true
                }
            ]
        },
        new ProviderEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Maria",
            LastName = "Gonzalez",
            Email = "maria.gonzalez@agendabuddy.dev",
            IsActive = true,
            ServiceEntities =
            [
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Individual Therapy Session",
                    Description = "CBT-based therapy session for anxiety and stress management",
                    Fee = 120.00m,
                    FeeType = FeeType.Hourly,
                    IsActive = true
                },
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Couples Counseling",
                    Description = "Joint session focused on communication and relationship health",
                    Fee = 150.00m,
                    FeeType = FeeType.Hourly,
                    IsActive = true
                },
                new ServiceEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = "Mindfulness Workshop",
                    Description = "90-minute guided mindfulness and meditation workshop",
                    Fee = 45.00m,
                    FeeType = FeeType.Fixed,
                    IsActive = true
                }
            ]
        }
    ];

    public static List<CustomerEntity> Customers() =>
    [
        new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Alex",
            LastName = "Chen",
            Email = "alex.chen@agendabuddy.dev"
        },
        new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "Priya",
            LastName = "Sharma",
            Email = "priya.sharma@agendabuddy.dev"
        },
        new CustomerEntity
        {
            Id = ObjectId.GenerateNewId(),
            FirstName = "David",
            LastName = "Thompson",
            Email = "david.thompson@agendabuddy.dev"
        }
    ];

    public const string DefaultPassword = "DevPass123!";
}
