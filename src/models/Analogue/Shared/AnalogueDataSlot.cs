using System.Collections;

namespace Pannella.Models.Analogue.Shared;

public class DataSlot
{
    public string id { get; set; }
    public string name { get; set; }
    public bool required { get; set; }
    public string parameters { get; set; }
    public string filename { get; set; }
    public string[] alternate_filenames { get; set; }
    public string md5 { get; set; }

    private BitArray GetBits()
    {
        if (parameters == null)
        {
            return null;
        }

        int p;

        if (parameters.StartsWith("0x"))
        {
            p = Convert.ToInt32(parameters, 16);
        }
        else
        {
            p = int.Parse(parameters);
        }

        byte[] bytes = BitConverter.GetBytes(p);
        BitArray bits = new BitArray(bytes);

        return bits;
    }

    public bool IsCoreSpecific()
    {
        var bits = this.GetBits();

        return bits != null && bits[1];
    }

    public int GetPlatformIdIndex()
    {
        var bits = this.GetBits();

        if (bits == null)
        {
            return 0;
        }

        var temp = new BitArray(2)
        {
            [1] = bits[25],
            [0] = bits[24]
        };

        int[] index = new int[1];

        temp.CopyTo(index, 0);

        return index[0];
    }
}
