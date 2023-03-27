namespace Analogue;

using System.Collections;

public class DataSlot
{
    public string id { get; set; }
    public string name { get; set; }
    public bool required { get; set; }
    public string parameters { get; set; }

    public string? filename { get; set; }

    public string[]? alternate_filenames{ get; set; }

    public bool isCoreSpecific()
    {
        if(parameters == null) {
            return false;
        }
        int p = 0;
        if(parameters.StartsWith("0x")) {
            p = Convert.ToInt32(parameters, 16);
        } else {
            p = Int32.Parse(parameters);
        }

        byte[] bytes = System.BitConverter.GetBytes(p);
        BitArray bits = new BitArray(bytes);

        return bits[1];
    }
}