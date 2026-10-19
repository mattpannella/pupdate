using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Pannella.TUI;

internal static class SessionLog
{
    private static readonly object SYNC = new();
    private static readonly List<string> lines = new();
    private static readonly StringBuilder partial = new();

    public static void Record(string message)
    {
        if (message == null)
        {
            return;
        }

        lock (SYNC)
        {
            FlushPartial();
            lines.Add(message);
        }
    }

    public static void RecordRaw(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (SYNC)
        {
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\n':
                        lines.Add(partial.ToString());
                        partial.Clear();
                        break;
                    case '\r':
                        partial.Clear();
                        break;
                    default:
                        partial.Append(c);
                        break;
                }
            }
        }
    }

    public static IReadOnlyList<string> Snapshot()
    {
        lock (SYNC)
        {
            var copy = new List<string>(lines);

            if (partial.Length > 0)
            {
                copy.Add(partial.ToString());
            }

            return copy;
        }
    }

    public static string Format(string version, string installPath, DateTime now)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"pupdate {version} - session log");
        sb.AppendLine($"Exported:  {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"OS:        {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture})");
        sb.AppendLine($"Install:   {installPath}");
        sb.AppendLine(new string('-', 40));

        foreach (string line in Snapshot())
        {
            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    public static string Export(string directory, string version, string installPath)
    {
        DateTime now = DateTime.Now;
        string path = Path.Combine(directory, $"pupdate_log_{now:yyyyMMdd_HHmmss}.txt");

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, Format(version, installPath, now));

        return path;
    }

    internal static void ResetForTests()
    {
        lock (SYNC)
        {
            lines.Clear();
            partial.Clear();
        }
    }

    private static void FlushPartial()
    {
        if (partial.Length > 0)
        {
            lines.Add(partial.ToString());
            partial.Clear();
        }
    }
}
