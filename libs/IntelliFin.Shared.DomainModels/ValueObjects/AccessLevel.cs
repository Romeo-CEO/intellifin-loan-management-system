namespace IntelliFin.Shared.DomainModels.ValueObjects;

public class AccessLevel : IEquatable<AccessLevel>
{
    public static readonly AccessLevel None = new(0, "None");
    public static readonly AccessLevel Read = new(1, "Read");
    public static readonly AccessLevel Write = new(2, "Write");
    public static readonly AccessLevel Delete = new(3, "Delete");
    public static readonly AccessLevel Admin = new(4, "Admin");
    public static readonly AccessLevel SuperAdmin = new(5, "SuperAdmin");

    private static readonly AccessLevel[] _predefinedLevels = { None, Read, Write, Delete, Admin, SuperAdmin };

    public int Value { get; }
    public string Name { get; }

    private AccessLevel(int value, string name)
    {
        Value = value;
        Name = name;
    }

    public static AccessLevel FromValue(int value)
    {
        var level = _predefinedLevels.FirstOrDefault(l => l.Value == value);
        return level ?? throw new ArgumentException($"Invalid access level value: {value}");
    }

    public static AccessLevel FromName(string name)
    {
        var level = _predefinedLevels.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return level ?? throw new ArgumentException($"Invalid access level name: {name}");
    }

    public bool CanAccess(AccessLevel requiredLevel)
    {
        return Value >= requiredLevel.Value;
    }

    public bool Equals(AccessLevel? other)
    {
        return other != null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AccessLevel);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Name;
    }

    public static bool operator ==(AccessLevel? left, AccessLevel? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    public static bool operator !=(AccessLevel? left, AccessLevel? right)
    {
        return !(left == right);
    }

    public static bool operator >(AccessLevel? left, AccessLevel? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return left.Value > right.Value;
    }

    public static bool operator <(AccessLevel? left, AccessLevel? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return left.Value < right.Value;
    }

    public static bool operator >=(AccessLevel? left, AccessLevel? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Value >= right?.Value;
    }

    public static bool operator <=(AccessLevel? left, AccessLevel? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Value <= right?.Value;
    }
}