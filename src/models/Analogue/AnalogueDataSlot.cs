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

    public string? md5 { get; set; }

    private BitArray? getBits()
    {
        if(parameters == null) {
            return null;
        }
        int p = 0;
        if(parameters.StartsWith("0x")) {
            p = Convert.ToInt32(parameters, 16);
        } else {
            p = Int32.Parse(parameters);
        }

        byte[] bytes = System.BitConverter.GetBytes(p);
        BitArray bits = new BitArray(bytes);

        return bits;
    }

    public bool isCoreSpecific()
    {
        var bits = getBits();

        if(bits == null) {
            return false;
        }

        return bits[1];
    }

    public int getPlatformIdIndex()
    {
        var bits = getBits();

        if(bits == null) {
            return 0;
        }

        var temp = new BitArray(2);
        temp[1] = bits[25];
        temp[0] = bits[24];

        int[] index = new int[1];
        temp.CopyTo(index, 0);

        return index[0];
    }
}