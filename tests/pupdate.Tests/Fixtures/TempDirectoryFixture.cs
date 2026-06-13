namespace Pannella.Tests.Fixtures;

public class TempDirectoryFixture : IDisposable
{
    public string Path { get; }

    public TempDirectoryFixture()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "pupdate-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Combine(params string[] segments)
    {
        var parts = new List<string> { Path };
        parts.AddRange(segments);
        return System.IO.Path.Combine(parts.ToArray());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // best effort
        }

        GC.SuppressFinalize(this);
    }
}
