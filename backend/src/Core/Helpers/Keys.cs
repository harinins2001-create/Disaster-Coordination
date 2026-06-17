namespace Core.Helpers;

public class Keys
{
    public (string Pk, string Sk) GenerateDisasterKeys(string slug)
    {
        var key = $"DISASTER#{slug}";
        return (key, key);
    }

    public (string Pk, string Sk) GenerateResourceKeys(string disasterSlug, string itemType)
    {
        return ($"DISASTER#{disasterSlug}", $"ITEM#{itemType}");
    }

    public string ResourcePartitionKey(string disasterSlug) => $"DISASTER#{disasterSlug}";

    public (string Pk, string Sk) GenerateUserKeys(string sub)
    {
        var key = $"USER#{sub}";
        return (key, key);
    }

    public (string Pk, string Sk) GenerateAssignmentKeys(string disasterSlug, string userSub)
    {
        return ($"DISASTER#{disasterSlug}", $"VOL#{userSub}");
    }

    public string AssignmentPartitionKey(string disasterSlug) => $"DISASTER#{disasterSlug}";

    public (string Pk, string Sk) GenerateDonationKeys(string disasterSlug, string donationId)
    {
        return ($"DISASTER#{disasterSlug}", $"DON#{donationId}");
    }

    public string DonationPartitionKey(string disasterSlug) => $"DISASTER#{disasterSlug}";

    public string NameToSlug(string name)
    {
        var normalized = string.Concat((name ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-'));
        while (normalized.Contains("--")) normalized = normalized.Replace("--", "-");
        return normalized.Trim('-');
    }
}
