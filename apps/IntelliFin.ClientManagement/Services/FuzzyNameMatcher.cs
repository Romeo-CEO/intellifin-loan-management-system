using System.Text;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for fuzzy name matching using Levenshtein distance and Soundex
/// Used for AML screening to detect potential matches with variations
/// </summary>
public class FuzzyNameMatcher
{
    private readonly ILogger<FuzzyNameMatcher> _logger;

    public FuzzyNameMatcher(ILogger<FuzzyNameMatcher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates match confidence between client name and sanctioned/PEP name
    /// Returns confidence score 0-100 and match type
    /// </summary>
    public MatchResult CalculateMatch(string clientName, string targetName, string[]? aliases = null)
    {
        // Normalize names for comparison
        var normalizedClient = NormalizeName(clientName);
        var normalizedTarget = NormalizeName(targetName);

        // Check exact match first
        if (string.Equals(normalizedClient, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Exact match found: {ClientName} = {TargetName}", clientName, targetName);
            return new MatchResult
            {
                Confidence = 100,
                MatchType = "Exact",
                MatchedName = targetName,
                LevenshteinDistance = 0,
                SoundexMatch = true
            };
        }

        // Check aliases for exact match
        if (aliases != null && aliases.Length > 0)
        {
            foreach (var alias in aliases)
            {
                var normalizedAlias = NormalizeName(alias);
                if (string.Equals(normalizedClient, normalizedAlias, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Exact alias match found: {ClientName} = {Alias}", clientName, alias);
                    return new MatchResult
                    {
                        Confidence = 98, // Slightly less than exact name match
                        MatchType = "ExactAlias",
                        MatchedName = alias,
                        LevenshteinDistance = 0,
                        SoundexMatch = true
                    };
                }
            }
        }

        // Calculate Levenshtein distance
        var levenshteinDistance = CalculateLevenshteinDistance(normalizedClient, normalizedTarget);
        var maxLength = Math.Max(normalizedClient.Length, normalizedTarget.Length);
        var levenshteinSimilarity = 1.0 - ((double)levenshteinDistance / maxLength);

        // Calculate Soundex match
        var clientSoundex = CalculateSoundex(normalizedClient);
        var targetSoundex = CalculateSoundex(normalizedTarget);
        var soundexMatch = clientSoundex == targetSoundex;

        // Calculate confidence score (weighted)
        // Levenshtein: 70% weight, Soundex: 30% weight
        var levenshteinScore = levenshteinSimilarity * 70;
        var soundexScore = soundexMatch ? 30 : 0;
        var totalConfidence = (int)(levenshteinScore + soundexScore);

        // Check aliases for fuzzy matches
        var bestAliasMatch = 0;
        var bestAlias = string.Empty;
        if (aliases != null && aliases.Length > 0)
        {
            foreach (var alias in aliases)
            {
                var normalizedAlias = NormalizeName(alias);
                var aliasDistance = CalculateLevenshteinDistance(normalizedClient, normalizedAlias);
                var aliasMaxLength = Math.Max(normalizedClient.Length, normalizedAlias.Length);
                var aliasSimilarity = 1.0 - ((double)aliasDistance / aliasMaxLength);
                var aliasScore = (int)(aliasSimilarity * 70);

                if (aliasScore > bestAliasMatch)
                {
                    bestAliasMatch = aliasScore;
                    bestAlias = alias;
                }
            }
        }

        // Use best match (target or alias)
        if (bestAliasMatch > totalConfidence)
        {
            totalConfidence = bestAliasMatch;
            targetName = bestAlias;
        }

        // Determine match type
        var matchType = soundexMatch ? "Phonetic" : 
                        totalConfidence >= 80 ? "HighSimilarity" : 
                        totalConfidence >= 60 ? "MediumSimilarity" : "LowSimilarity";

        _logger.LogDebug(
            "Fuzzy match calculated: {ClientName} vs {TargetName} = {Confidence}% ({MatchType})",
            clientName, targetName, totalConfidence, matchType);

        return new MatchResult
        {
            Confidence = Math.Min(totalConfidence, 99), // Reserve 100 for exact matches
            MatchType = matchType,
            MatchedName = targetName,
            LevenshteinDistance = levenshteinDistance,
            SoundexMatch = soundexMatch
        };
    }

    /// <summary>
    /// Normalizes name for comparison (uppercase, trim, remove extra spaces)
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Convert to uppercase, trim, and collapse multiple spaces
        var normalized = name.Trim().ToUpperInvariant();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");

        return normalized;
    }

    /// <summary>
    /// Calculates Levenshtein distance between two strings
    /// Measures minimum number of single-character edits required
    /// </summary>
    private static int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return target?.Length ?? 0;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;

        // Create distance matrix
        var distance = new int[sourceLength + 1, targetLength + 1];

        // Initialize first column and row
        for (var i = 0; i <= sourceLength; i++)
            distance[i, 0] = i;

        for (var j = 0; j <= targetLength; j++)
            distance[0, j] = j;

        // Calculate distances
        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;

                distance[i, j] = Math.Min(
                    Math.Min(
                        distance[i - 1, j] + 1,     // Deletion
                        distance[i, j - 1] + 1),    // Insertion
                    distance[i - 1, j - 1] + cost); // Substitution
            }
        }

        return distance[sourceLength, targetLength];
    }

    /// <summary>
    /// Calculates Soundex code for phonetic matching
    /// American Soundex algorithm (4-character code)
    /// </summary>
    private static string CalculateSoundex(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "0000";

        // Remove non-alphabetic characters and convert to uppercase
        var normalized = new string(name.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        
        if (string.IsNullOrEmpty(normalized))
            return "0000";

        var soundex = new StringBuilder();
        soundex.Append(normalized[0]); // Keep first letter

        // Soundex digit mapping
        var lastCode = GetSoundexCode(normalized[0]);

        for (var i = 1; i < normalized.Length && soundex.Length < 4; i++)
        {
            var code = GetSoundexCode(normalized[i]);

            // Skip vowels and letters with code 0
            if (code == '0')
                continue;

            // Skip duplicate consecutive codes
            if (code != lastCode)
            {
                soundex.Append(code);
                lastCode = code;
            }
        }

        // Pad with zeros if needed
        while (soundex.Length < 4)
            soundex.Append('0');

        return soundex.ToString();
    }

    /// <summary>
    /// Gets Soundex code for a character
    /// </summary>
    private static char GetSoundexCode(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            'B' or 'F' or 'P' or 'V' => '1',
            'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => '2',
            'D' or 'T' => '3',
            'L' => '4',
            'M' or 'N' => '5',
            'R' => '6',
            _ => '0' // Vowels (A, E, I, O, U) and H, W, Y
        };
    }
}

/// <summary>
/// Result of fuzzy name matching
/// </summary>
public class MatchResult
{
    /// <summary>
    /// Confidence score (0-100)
    /// 100 = Exact match
    /// 80-99 = High confidence match
    /// 60-79 = Medium confidence match
    /// 0-59 = Low confidence match
    /// </summary>
    public int Confidence { get; set; }

    /// <summary>
    /// Type of match detected
    /// </summary>
    public string MatchType { get; set; } = "NoMatch";

    /// <summary>
    /// Name that matched (original or alias)
    /// </summary>
    public string MatchedName { get; set; } = string.Empty;

    /// <summary>
    /// Levenshtein distance (edit distance)
    /// </summary>
    public int LevenshteinDistance { get; set; }

    /// <summary>
    /// Whether Soundex codes match (phonetic match)
    /// </summary>
    public bool SoundexMatch { get; set; }

    /// <summary>
    /// Whether this is considered a match (confidence >= 60)
    /// </summary>
    public bool IsMatch => Confidence >= 60;
}
