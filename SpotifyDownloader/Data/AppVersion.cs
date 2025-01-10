using System.Diagnostics.CodeAnalysis;

namespace SpotifyDownloader.Data;

public class AppVersion
{
    public required int Major { get; set; }
    public required int Minor { get; set; }
    public required int Bugfix { get; set; }

    public AppVersion() { }
    [SetsRequiredMembers]
    public AppVersion(int major, int minor, int bugfix)
    {
        Major = major;
        Minor = minor;
        Bugfix = bugfix;
    }

    public override string ToString()
        => $"v{Major}.{Minor}.{Bugfix}";

    public static bool operator <(AppVersion v1, AppVersion v2)
    {
        if (v1.Major != v2.Major)
        {
            return v1.Major < v2.Major;
        }
        if (v1.Minor != v2.Minor)
        {
            return v1.Minor < v2.Minor;
        }
        return v1.Bugfix < v2.Bugfix;
    }

    public static bool operator>(AppVersion v1, AppVersion v2)
    {
        if (v1.Major != v2.Major)
        {
            return v1.Major > v2.Major;
        }
        if (v1.Minor != v2.Minor)
        {
            return v1.Minor > v2.Minor;
        }
        return v1.Bugfix > v2.Bugfix;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not AppVersion other)
            return false;

        return Major == other.Major && Minor == other.Minor && Bugfix == other.Bugfix;
    }
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Bugfix);

    public static bool operator ==(AppVersion v1, AppVersion v2) => v1.Equals(v2);
    public static bool operator !=(AppVersion v1, AppVersion v2) => !(v1.Equals(v2));
    public static bool operator <=(AppVersion v1, AppVersion v2) => v1 < v2 || v1 == v2;
    public static bool operator >=(AppVersion v1, AppVersion v2) => v1 > v2 || v1 == v2;

}
