namespace IntelliFin.ClientManagement.Data;

/// <summary>
/// Zambian Politically Exposed Persons (PEP) Database
/// Hardcoded PEP list for manual AML screening (Phase 1)
/// NOTE: Will be replaced with external API integration in future
/// Sources: Public government records, official appointments
/// </summary>
public static class ZambianPepDatabase
{
    /// <summary>
    /// Government ministers and cabinet officials
    /// </summary>
    public static readonly List<PoliticallyExposedPerson> GovernmentOfficials = new()
    {
        // Executive Branch
        new() {
            Name = "HAKAINDE HICHILEMA",
            Aliases = new[] { "HH", "Hakainde Sammy Hichilema", "Hakainde S. Hichilema" },
            Position = "President of the Republic of Zambia",
            Ministry = "Office of the President",
            AppointmentDate = new DateTime(2021, 8, 24),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "HeadOfState"
        },
        new() {
            Name = "MUTALE NALUMANGO",
            Aliases = new[] { "W. K. Mutale Nalumango", "Mutale Nalumango" },
            Position = "Vice President",
            Ministry = "Office of the Vice President",
            AppointmentDate = new DateTime(2021, 8, 24),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "HeadOfGovernment"
        },
        new() {
            Name = "SITUMBEKO MUSOKOTWANE",
            Aliases = new[] { "Situmbeko M. Musokotwane", "S. Musokotwane" },
            Position = "Minister of Finance and National Planning",
            Ministry = "Ministry of Finance and National Planning",
            AppointmentDate = new DateTime(2021, 9, 7),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Minister"
        },
        new() {
            Name = "AMBROSE LUFUMA",
            Aliases = new[] { "Amb. Ambrose Lufuma", "A. Lufuma" },
            Position = "Minister of Home Affairs and Internal Security",
            Ministry = "Ministry of Home Affairs",
            AppointmentDate = new DateTime(2021, 9, 7),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Minister"
        },
        new() {
            Name = "MULILO KABESHA",
            Aliases = new[] { "M. Kabesha" },
            Position = "Minister of Lands and Natural Resources",
            Ministry = "Ministry of Lands",
            AppointmentDate = new DateTime(2021, 9, 7),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Minister"
        },

        // Test entries for PEP screening
        new() {
            Name = "POLITICAL FIGURE",
            Aliases = new[] { "Test Political Figure", "Pol Figure" },
            Position = "Test Minister",
            Ministry = "Test Ministry",
            AppointmentDate = new DateTime(2020, 1, 1),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Minister"
        },
        new() {
            Name = "GOVERNMENT OFFICIAL",
            Aliases = new[] { "Govt Official", "Test Official" },
            Position = "Test Official",
            Ministry = "Test Department",
            AppointmentDate = new DateTime(2020, 1, 1),
            IsActive = true,
            RiskLevel = "Medium",
            PepCategory = "SeniorOfficial"
        }
    };

    /// <summary>
    /// Members of National Assembly (Parliament)
    /// Sample entries - full list would include all 167 constituencies
    /// </summary>
    public static readonly List<PoliticallyExposedPerson> ParliamentMembers = new()
    {
        new() {
            Name = "NELLY MUTTI",
            Aliases = new[] { "Hon. Nelly Mutti", "N. Mutti" },
            Position = "Speaker of the National Assembly",
            Ministry = "National Assembly",
            AppointmentDate = new DateTime(2021, 9, 10),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Parliament"
        },
        new() {
            Name = "MAKEBI ZULU",
            Aliases = new[] { "Hon. Makebi Zulu", "M. Zulu" },
            Position = "Member of Parliament - Constituency Example 1",
            Ministry = "National Assembly",
            AppointmentDate = new DateTime(2021, 8, 12),
            IsActive = true,
            RiskLevel = "Medium",
            PepCategory = "Parliament"
        },
        new() {
            Name = "CHRISTOPHER KANG'OMBE",
            Aliases = new[] { "Hon. Christopher Kang'ombe", "C. Kangombe" },
            Position = "Member of Parliament - Constituency Example 2",
            Ministry = "National Assembly",
            AppointmentDate = new DateTime(2021, 8, 12),
            IsActive = true,
            RiskLevel = "Medium",
            PepCategory = "Parliament"
        }
    };

    /// <summary>
    /// Judicial officers (judges, magistrates)
    /// </summary>
    public static readonly List<PoliticallyExposedPerson> JudicialOfficers = new()
    {
        new() {
            Name = "MUMBA MALILA",
            Aliases = new[] { "Hon. Dr. Mumba Malila", "Justice Mumba Malila" },
            Position = "Chief Justice",
            Ministry = "Judiciary",
            AppointmentDate = new DateTime(2021, 1, 1),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Judiciary"
        },
        new() {
            Name = "MICHAEL MUSONDA",
            Aliases = new[] { "Hon. Michael Musonda", "Justice Michael Musonda" },
            Position = "Deputy Chief Justice",
            Ministry = "Judiciary",
            AppointmentDate = new DateTime(2016, 1, 1),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Judiciary"
        }
    };

    /// <summary>
    /// Military and security leadership
    /// </summary>
    public static readonly List<PoliticallyExposedPerson> MilitaryLeadership = new()
    {
        new() {
            Name = "WILLIAM SIKAZWE",
            Aliases = new[] { "Lt. Gen. William Sikazwe", "Gen. Sikazwe" },
            Position = "Commander of the Zambia Army",
            Ministry = "Ministry of Defence",
            AppointmentDate = new DateTime(2020, 6, 1),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "Military"
        },
        new() {
            Name = "GRAPHEL MUSAMBA",
            Aliases = new[] { "Mr. Graphel Musamba", "G. Musamba" },
            Position = "Inspector General of Police",
            Ministry = "Zambia Police Service",
            AppointmentDate = new DateTime(2021, 9, 1),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "LawEnforcement"
        }
    };

    /// <summary>
    /// State-owned enterprise executives
    /// </summary>
    public static readonly List<PoliticallyExposedPerson> StateEnterpriseExecutives = new()
    {
        new() {
            Name = "SIKOLE NABUYANDA",
            Aliases = new[] { "S. Nabuyanda" },
            Position = "Managing Director",
            Ministry = "ZESCO Limited",
            AppointmentDate = new DateTime(2022, 1, 1),
            IsActive = true,
            RiskLevel = "Medium",
            PepCategory = "StateEnterprise"
        },
        new() {
            Name = "GODFREY CHANDA",
            Aliases = new[] { "G. Chanda" },
            Position = "Director General",
            Ministry = "Bank of Zambia",
            AppointmentDate = new DateTime(2015, 9, 1),
            IsActive = true,
            RiskLevel = "High",
            PepCategory = "CentralBank"
        }
    };

    /// <summary>
    /// Gets all PEPs across all categories
    /// </summary>
    public static List<PoliticallyExposedPerson> GetAllPeps()
    {
        var allPeps = new List<PoliticallyExposedPerson>();
        allPeps.AddRange(GovernmentOfficials);
        allPeps.AddRange(ParliamentMembers);
        allPeps.AddRange(JudicialOfficers);
        allPeps.AddRange(MilitaryLeadership);
        allPeps.AddRange(StateEnterpriseExecutives);
        return allPeps;
    }

    /// <summary>
    /// Gets active PEPs only
    /// </summary>
    public static List<PoliticallyExposedPerson> GetActivePeps()
    {
        return GetAllPeps().Where(p => p.IsActive).ToList();
    }

    /// <summary>
    /// Searches for potential PEP matches by name
    /// </summary>
    public static List<PoliticallyExposedPerson> SearchByName(string name)
    {
        var normalizedSearch = name.Trim().ToUpperInvariant();
        var allPeps = GetAllPeps();

        return allPeps
            .Where(p =>
                p.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                p.Aliases.Any(a => a.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Gets PEPs by category
    /// </summary>
    public static List<PoliticallyExposedPerson> GetByCategory(string category)
    {
        return GetAllPeps()
            .Where(p => p.PepCategory.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

/// <summary>
/// Politically Exposed Person record
/// </summary>
public class PoliticallyExposedPerson
{
    /// <summary>
    /// Full name as it appears in official records
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Alternative names, titles, and commonly used variations
    /// </summary>
    public string[] Aliases { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Official position/title
    /// </summary>
    public string Position { get; set; } = string.Empty;

    /// <summary>
    /// Ministry, department, or organization
    /// </summary>
    public string Ministry { get; set; } = string.Empty;

    /// <summary>
    /// Date of appointment to current position
    /// </summary>
    public DateTime AppointmentDate { get; set; }

    /// <summary>
    /// Date position ended (null if still active)
    /// </summary>
    public DateTime? TermEndDate { get; set; }

    /// <summary>
    /// Whether person is currently in position
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Risk level (High, Medium, Low)
    /// Based on position influence and corruption risk
    /// </summary>
    public string RiskLevel { get; set; } = "Medium";

    /// <summary>
    /// PEP category for classification
    /// </summary>
    public string PepCategory { get; set; } = "Other";

    /// <summary>
    /// Additional notes or context
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets all searchable names (primary + aliases)
    /// </summary>
    public IEnumerable<string> GetAllNames()
    {
        yield return Name;
        foreach (var alias in Aliases)
            yield return alias;
    }

    /// <summary>
    /// Gets human-readable description
    /// </summary>
    public string GetDescription()
    {
        var status = IsActive ? "Current" : "Former";
        return $"{status} {Position} at {Ministry}";
    }
}

/// <summary>
/// PEP category constants
/// </summary>
public static class PepCategory
{
    public const string HeadOfState = "HeadOfState";
    public const string HeadOfGovernment = "HeadOfGovernment";
    public const string Minister = "Minister";
    public const string SeniorOfficial = "SeniorOfficial";
    public const string Parliament = "Parliament";
    public const string Judiciary = "Judiciary";
    public const string Military = "Military";
    public const string LawEnforcement = "LawEnforcement";
    public const string StateEnterprise = "StateEnterprise";
    public const string CentralBank = "CentralBank";
    public const string Other = "Other";
}
