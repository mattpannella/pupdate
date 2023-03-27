using System.IO;
using System.Net.Http;

namespace pannella.analoguepocket;

public class GlobalHelper
{
    private static GlobalHelper instance = null;
    private static object syncLock = new object();
    public archiveorg.Archive ArchiveFiles { get; set; }
    public SettingsManager? SettingsManager { get; set ;}
    public string UpdateDirectory { get; set; }
    public string SettingsPath { get; set; }
    public string[] Blacklist { get; set; }
    public List<Core>? Cores { get; set; }

    private GlobalHelper()
    {
        
    }

    public static GlobalHelper Instance
    {
        get
        {
            lock (syncLock)
            {
                if (GlobalHelper.instance == null) {
                    GlobalHelper.instance = new GlobalHelper();
                }

                return GlobalHelper.instance;
            }
        }
    }  
}
