namespace pannella.analoguepocket;

public class Factory
{
    public static HttpHelper GetHttpHelper()
    {
        return HttpHelper.Instance;
    }

    public static GlobalHelper GetGlobals()
    {
        return GlobalHelper.Instance;
    }
}