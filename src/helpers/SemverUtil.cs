namespace pannella.analoguepocket;

using System.Text.RegularExpressions;

public class SemverUtil
{
    private const string SEMVER_FINDER = @"\D*(\d+(\.\d+)*\.\d+)\D*";

    public static string FindSemver(string input)
    {
        Regex r = new Regex(SEMVER_FINDER);
        Match matches = r.Match(input);
        if(matches == null || matches.Groups.Count <= 1) {
            return null;
        }
        var semver = matches.Groups[1].Value;
        //TODO throw some error if it doesn't find a semver in the tag
        semver = CompleteSemver(semver);

        return semver;
    }

    ///<summary>
    /////even though its technically not a valid semver, allow use of 2 part versions, and just add a .0 to complete the 3rd part
    ///</summary>
    public static string CompleteSemver(string version)
    {
        string[] parts = version.Split(".");

        if(parts.Length == 2) {
            version += ".0";
        }
        
        return version;
    }

    public static bool SemverCompare(string semverA, string semverB)
    {
        Version verA = Version.Parse(semverA);
        Version verB = Version.Parse(semverB);
        
        switch(verA.CompareTo(verB))
        {
            case 0:
            case -1:
                return false;
            case 1:
                return true;
            default:
                return true;
        }
    }

    public static bool IsActuallySemver(string potentiallySemver)
    {
        Version ver = null;
        return Version.TryParse(potentiallySemver, out ver);
    }
}