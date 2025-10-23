namespace IntelliFin.ClientManagement.Data;

/// <summary>
/// Hardcoded sanctions lists for manual AML screening (Phase 1)
/// NOTE: Will be replaced with external API integration in future
/// Sources: OFAC SDN, UN Security Council, EU Consolidated List
/// </summary>
public static class SanctionsList
{
    /// <summary>
    /// OFAC Specially Designated Nationals (SDN) List
    /// Sample entries for demonstration and testing
    /// </summary>
    public static readonly List<SanctionedEntity> OfacList = new()
    {
        // High-profile political figures
        new() {
            Name = "VLADIMIR PUTIN",
            Aliases = new[] { "Vladimir Vladimirovich Putin", "V. Putin", "Vladmir Putin" },
            EntityType = "Individual",
            Program = "UKRAINE-EO13661",
            Country = "RU",
            Description = "President of the Russian Federation"
        },
        new() {
            Name = "KIM JONG UN",
            Aliases = new[] { "Kim Jong-un", "Kim Jong Un", "KIM JONG-UN" },
            EntityType = "Individual",
            Program = "DPRK",
            Country = "KP",
            Description = "Supreme Leader of North Korea"
        },
        new() {
            Name = "BASHAR AL-ASSAD",
            Aliases = new[] { "Bashar al-Asad", "Bachar al-Assad", "BASHAR HAFEZ AL-ASSAD" },
            EntityType = "Individual",
            Program = "SYRIA",
            Country = "SY",
            Description = "President of Syria"
        },
        new() {
            Name = "NICOLAS MADURO",
            Aliases = new[] { "Nicolás Maduro Moros", "Nicolas Maduro Moros", "MADURO MOROS" },
            EntityType = "Individual",
            Program = "VENEZUELA",
            Country = "VE",
            Description = "President of Venezuela"
        },

        // Test names for automated testing
        new() {
            Name = "SANCTIONED PERSON",
            Aliases = new[] { "Sanctioned Individual", "Test Sanctioned" },
            EntityType = "Individual",
            Program = "TEST",
            Country = "XX",
            Description = "Test entry for sanctions screening"
        },
        new() {
            Name = "JOHN DOE SANCTIONED",
            Aliases = new[] { "Johnny Sanctioned", "J.D. Sanctioned" },
            EntityType = "Individual",
            Program = "TEST",
            Country = "XX",
            Description = "Test entry for fuzzy matching"
        },

        // Additional OFAC entries (sample)
        new() {
            Name = "ALEXANDER LUKASHENKO",
            Aliases = new[] { "Alyaksandr Lukashenka", "Aleksandr Lukashenko", "A. Lukashenko" },
            EntityType = "Individual",
            Program = "BELARUS",
            Country = "BY",
            Description = "President of Belarus"
        },
        new() {
            Name = "YEVGENY PRIGOZHIN",
            Aliases = new[] { "Evgeny Prigozhin", "Yevgeniy Prigozhin", "Evgeniy Viktorovich Prigozhin" },
            EntityType = "Individual",
            Program = "RUSSIA-EO13661",
            Country = "RU",
            Description = "Russian businessman"
        },
        new() {
            Name = "SERGEY LAVROV",
            Aliases = new[] { "Sergei Lavrov", "Sergey Viktorovich Lavrov" },
            EntityType = "Individual",
            Program = "UKRAINE-EO13661",
            Country = "RU",
            Description = "Minister of Foreign Affairs of Russia"
        },
        new() {
            Name = "RAUL CASTRO",
            Aliases = new[] { "Raúl Castro", "Raul Castro Ruz", "Raúl Modesto Castro Ruz" },
            EntityType = "Individual",
            Program = "CUBA",
            Country = "CU",
            Description = "Former President of Cuba"
        }
    };

    /// <summary>
    /// UN Security Council Consolidated List
    /// Sample entries from UN sanctions programs
    /// </summary>
    public static readonly List<SanctionedEntity> UnList = new()
    {
        new() {
            Name = "OSAMA BIN LADEN",
            Aliases = new[] { "Usama bin Ladin", "Osama bin Muhammed bin Awad bin Ladin" },
            EntityType = "Individual",
            Program = "UN-ISIL-AL-QAIDA",
            Country = "SA",
            Description = "Former Al-Qaida leader (deceased)"
        },
        new() {
            Name = "AYMAN AL-ZAWAHIRI",
            Aliases = new[] { "Ayman al-Zawahri", "Aiman Muhammad Rabi al-Zawahiri" },
            EntityType = "Individual",
            Program = "UN-ISIL-AL-QAIDA",
            Country = "EG",
            Description = "Al-Qaida leader (deceased)"
        },
        new() {
            Name = "ABU BAKR AL-BAGHDADI",
            Aliases = new[] { "Ibrahim Awad Ibrahim al-Badri", "Abu Du'a", "Dr. Ibrahim" },
            EntityType = "Individual",
            Program = "UN-ISIL-AL-QAIDA",
            Country = "IQ",
            Description = "Former ISIL leader (deceased)"
        }
    };

    /// <summary>
    /// EU Consolidated Sanctions List
    /// Sample entries from EU sanctions regimes
    /// </summary>
    public static readonly List<SanctionedEntity> EuList = new()
    {
        new() {
            Name = "VLADIMIR PUTIN",
            Aliases = new[] { "Vladimir Vladimirovich Putin" },
            EntityType = "Individual",
            Program = "EU-UKRAINE",
            Country = "RU",
            Description = "President of Russian Federation"
        },
        new() {
            Name = "SERGEI SHOIGU",
            Aliases = new[] { "Sergey Shoigu", "Sergei Kuzhugetovich Shoigu" },
            EntityType = "Individual",
            Program = "EU-UKRAINE",
            Country = "RU",
            Description = "Minister of Defence of Russia"
        },
        new() {
            Name = "IGOR SECHIN",
            Aliases = new[] { "Igor Ivanovich Sechin" },
            EntityType = "Individual",
            Program = "EU-UKRAINE",
            Country = "RU",
            Description = "CEO of Rosneft"
        }
    };

    /// <summary>
    /// Gets all sanctions lists combined
    /// </summary>
    public static List<SanctionedEntity> GetAllSanctions()
    {
        var allSanctions = new List<SanctionedEntity>();
        allSanctions.AddRange(OfacList);
        allSanctions.AddRange(UnList);
        allSanctions.AddRange(EuList);
        return allSanctions;
    }

    /// <summary>
    /// Searches for potential sanctions matches
    /// </summary>
    public static List<SanctionedEntity> SearchByName(string name)
    {
        var normalizedSearch = name.Trim().ToUpperInvariant();
        var allSanctions = GetAllSanctions();

        return allSanctions
            .Where(s => 
                s.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                s.Aliases.Any(a => a.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}

/// <summary>
/// Sanctioned entity (individual or organization)
/// </summary>
public class SanctionedEntity
{
    /// <summary>
    /// Primary name as listed
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Alternative names and aliases
    /// </summary>
    public string[] Aliases { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Entity type (Individual, Entity, Vessel, Aircraft)
    /// </summary>
    public string EntityType { get; set; } = "Individual";

    /// <summary>
    /// Sanctions program (OFAC-SDN, DPRK, UKRAINE, etc.)
    /// </summary>
    public string Program { get; set; } = string.Empty;

    /// <summary>
    /// Country code (ISO 3166-1 alpha-2)
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Description or additional information
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Date added to list (if known)
    /// </summary>
    public DateTime? DateAdded { get; set; }

    /// <summary>
    /// Gets all searchable names (primary + aliases)
    /// </summary>
    public IEnumerable<string> GetAllNames()
    {
        yield return Name;
        foreach (var alias in Aliases)
            yield return alias;
    }
}
