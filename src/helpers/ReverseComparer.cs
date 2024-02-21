using System.Collections;

namespace Pannella.Helpers;

public class ReverseComparer : IComparer
{
    // Calls CaseInsensitiveComparer.Compare with the parameters reversed.
    int IComparer.Compare(object x, object y)
    {
        return new CaseInsensitiveComparer().Compare(y, x);
    }
}
