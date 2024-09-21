using Pannella.Helpers;

namespace Pannella.Services;

public class AnalogizerOptionType
{
    public string Options { get; set; }
    public Dictionary<string, int[]> Dict { get; set; }
    public Dictionary<string, string> Replace { get; set; }
    public bool Expect1 { get; set; }
    public string Skip { get; set; }

    public AnalogizerOptionType(bool exp1 = true, string[] skp = null)
    {
        Options = "";
        Dict = new Dictionary<string, int[]>();
        Replace = new Dictionary<string, string>();
        Expect1 = exp1;
        Skip = skp != null ? string.Join("", skp) : "u";
    }

    public string GetInput()
    {
        Console.WriteLine(Options);

        string input = Console.ReadLine()!.ToLower();
        string notFound = "";

        if (Expect1 && input.Length > 1)
        {
            Console.WriteLine($"\nExpected input length: 1\nInput length found: {input.Length}\nTry again.");

            return GetInput();
        }

        if (Replace != null)
        {
            foreach (var r in Replace)
            {
                input = input.Replace(r.Key, r.Value);
            }
        }

        foreach (char let in input.Distinct())
        {
            if (!Dict.ContainsKey(let.ToString()) || Skip.Contains(let))
            {
                notFound += let;
            }
            else
            {
                Dict[let.ToString()][0]++;
            }
        }

        if (!string.IsNullOrEmpty(notFound))
        {
            Console.WriteLine($"Sorry, could not find following options: '{notFound.ToUpper()}'. Inputs ignored\n");
        }

        return input;
    }
}

public static class UserOptions
{
    public static void GenerateUserOptions(AnalogizerOptionType[] records, int wLen = 32, string filename = "test.bin", string filename2 = null)
    {
        string finalNum = "";
        string[] files = { filename, filename2 };

        if (filename == filename2)
        {
            files[1] = null;
        }

        foreach (var sel in records)
        {
            string selInput = sel.GetInput();

            foreach (var item in sel.Dict)
            {
                finalNum += string.Join("", item.Value);
            }
        }

        if (finalNum.Length < wLen)
        {
            finalNum = finalNum.PadRight(wLen, '0');
        }

        string hexStr = Convert.ToInt64(finalNum, 2).ToString("X").PadLeft(wLen / 4, '0');

        foreach (var file in files)
        {
            if (file == null)
                continue;

            File.WriteAllBytes(file, Enumerable.Range(0, hexStr.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hexStr.Substring(x, 2), 16))
                .ToArray());
        }
    }
}

public class AnalogizerSettingsService
{
    public void RunAnalogizerSettings()
    {
        var crt = new AnalogizerOptionType();

        crt.Options = @"
Please, select your preferred Video Option.
For example: A for RGB video

Letter | Option
-------|--------------------------------------------|
A   | RBGS       (SCART)                         |
B   | RGsB                                       |
C   | YPbPr      (Component video)               |
D   | Y/C NTSC   (SVideo, Composite video)       |
E   | Y/C PAL    (SVideo, Composite video)       |
F   | Scandoubler RGBHV (SCANLINES  0%)          |
G   | Scandoubler RGBHV (SCANLINES 25%)          |
H   | Scandoubler RGBHV (SCANLINES 50%)          |
I   | Scandoubler RGBHV (SCANLINES 75%)          |
X   | Disable Analog Video                       |
----------------------------------------------------|

Your selection:    ";

        crt.Dict = new Dictionary<string, int[]>
        {
            { "a", new int[1] },
            { "b", new int[1] },
            { "c", new int[1] },
            { "k", new int[1] },
            { "l", new int[1] },
            { "u", new int[1] },
            { "d", new int[1] },
            { "i", new int[1] },
            { "j", new int[1] },
            { "h", new int[1] },
            { "g", new int[1] },
            { "f", new int[1] }
        };

        crt.Replace = new Dictionary<string, string>
        {
            { "f", "af" },
            { "h", "afh" },
            { "g", "afg" },
            { "i", "afgh" },
            { "e", "akl" },
            { "d", "ak" },
            { "c", "acj" },
            { "b", "abj" },
            { "x", "" }
        };

        var snac = new AnalogizerOptionType();

        snac.Options = @"
Please, select the option corresponding to your controller.

Letter | Option
-------|--------------------------------------------|
A   | None                                       |
B   | DB15 Normal                                |
C   | NES                                        |
D   | SNES                                       |
E   | PCE 2BTN/6BTN                              |
F   | PCE Multitap                               |
----------------------------------------------------|

Your selection:    ";

        snac.Dict = new Dictionary<string, int[]>
        {
            { "u", new int[3] },
            { "a", new int[1] },
            { "f", new int[1] },
            { "e", new int[1] },
            { "c", new int[1] },
            { "b", new int[1] }
        };

        snac.Replace = new Dictionary<string, string>
        {
            { "a", "" },
            { "f", "ec" },
            { "d", "cb" },
            { "e", "eb" }
        };

        const string filename = "crtcfg.bin";
        string filepath = Path.Combine(ServiceHelper.UpdateDirectory, filename);

        UserOptions.GenerateUserOptions(new[] { crt, snac }, filename: filepath, filename2: filepath);

        if (File.Exists(filepath))
        {
            string jtpatreon = Path.Combine(ServiceHelper.UpdateDirectory, "Assets", "jtpatreon", "common");

            if (!Directory.Exists(jtpatreon))
            {
                Directory.CreateDirectory(jtpatreon);
            }

            string destPath = Path.Combine(jtpatreon, filename);

            File.Move(filepath, destPath, true);
        }
    }
}
