using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Seed;

/// <summary>Seeds the major Hindu festivals for the current seed year (2026).</summary>
public static class FestivalSeed
{
    public const int SeedYear = 2026;

    private static readonly (string Name, string Description, int Month, int Day)[] Festivals2026 =
    {
        ("Makar Sankranti", "Harvest festival marking the sun's transition into Capricorn", 1, 14),
        ("Vasant Panchami", "Worship of Goddess Saraswati, welcoming spring", 1, 23),
        ("Maha Shivaratri", "The great night of Lord Shiva", 2, 15),
        ("Holi", "Festival of colours celebrating the triumph of good over evil", 3, 3),
        ("Ugadi / Gudi Padwa", "Hindu New Year for the Deccan region", 3, 19),
        ("Rama Navami", "Birth of Lord Rama", 3, 27),
        ("Hanuman Jayanti", "Birth of Lord Hanuman", 4, 2),
        ("Guru Purnima", "Honouring spiritual gurus and teachers", 7, 29),
        ("Raksha Bandhan", "Celebrating the sacred bond between brothers and sisters", 8, 28),
        ("Krishna Janmashtami", "Birth of Lord Krishna", 9, 4),
        ("Ganesh Chaturthi", "Birth of Lord Ganesha", 9, 14),
        ("Navaratri Begins", "Nine nights devoted to Goddess Durga", 10, 11),
        ("Dussehra (Vijayadashami)", "Victory of good over evil", 10, 20),
        ("Karwa Chauth", "Fast observed for the well-being of one's spouse", 10, 29),
        ("Diwali (Deepavali)", "Festival of lights", 11, 8),
        ("Govardhan Puja", "Worship of Govardhan Hill", 11, 10),
        ("Bhai Dooj", "Celebrating the bond between brothers and sisters", 11, 11),
        ("Chhath Puja", "Worship of the Sun God, Surya", 11, 15),
    };

    public static Festival[] Data()
    {
        var result = new Festival[Festivals2026.Length];
        for (var i = 0; i < Festivals2026.Length; i++)
        {
            var (name, description, month, day) = Festivals2026[i];
            result[i] = new Festival
            {
                Id = new Guid($"fe510000-0000-0000-0000-{(i + 1):D12}"),
                Name = name,
                Description = description,
                Year = SeedYear,
                Date = new DateOnly(SeedYear, month, day),
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            };
        }
        return result;
    }
}
