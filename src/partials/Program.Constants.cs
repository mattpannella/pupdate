// ReSharper disable UseRawString

using System.Reflection;

namespace Pannella;

internal static partial class Program
{
    private static readonly string VERSION = Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);

    private static readonly string SYSTEM_OS_PLATFORM = GetSystemPlatform();

    private const string USER = "mattpannella";

    private const string REPOSITORY = "pupdate";

    private const string RELEASE_URL = "https://github.com/mattpannella/pupdate/releases/download/{0}/pupdate_{1}.zip";

    private static readonly string[] WELCOME_MESSAGES =
    {
        @"                                                                                
 _____ _                                          _ ___               _____       _ 
| __  | |___ _____ ___    _ _ ___ _ _ ___ ___ ___| |  _|   ___ ___   |   __|___ _| |
| __ -| | .'|     | -_|  | | | . | | |  _|_ -| -_| |  _|  | . |  _|  |  |  | . | . |
|_____|_|__,|_|_|_|___|  |_  |___|___|_| |___|___|_|_|    |___|_|    |_____|___|___|
                         |___|
                                                                                    ",
        @"                                                                                       
 _ _ _     _                      _          _____ _                 _                 
| | | |___| |___ ___ _____ ___   | |_ ___   |   __| |___ _ _ ___ ___| |_ ___ _ _ _ ___ 
| | | | -_| |  _| . |     | -_|  |  _| . |  |   __| | .'| | | . |  _|  _| . | | | |   |
|_____|___|_|___|___|_|_|_|___|  |_| |___|  |__|  |_|__,|\_/|___|_| |_| |___|_____|_|_|
                                                                                       ",
        @"                                                                                               
     _          ___ _       _                                       _            _             
 ___| |_ ___   |  _|_|___ _| |___    _ _ ___ _ _    ___ ___ _ _ ___| |_ _ _    _| |___ _ _ ___ 
|_ -|   | -_|  |  _| |   | . |_ -|  | | | . | | |  |  _|  _| | |_ -|  _| | |  | . | .'| | | -_|
|___|_|_|___|  |_| |_|_|_|___|___|  |_  |___|___|  |___|_| |___|___|_| |_  |  |___|__,|\_/|___|
                                    |___|                              |___|                   ",
        @"                                                                                                   
 _____ _   _        _               ___                                _                       _   
|_   _| |_|_|___   |_|___    ___   |  _|___ ___ ___ _ _    ___ ___ ___| |_ ___ _ _ ___ ___ ___| |_ 
  | | |   | |_ -|  | |_ -|  | .'|  |  _| .'|   |  _| | |  |  _| -_|_ -|  _| .'| | |  _| .'|   |  _|
  |_| |_|_|_|___|  |_|___|  |__,|  |_| |__,|_|_|___|_  |  |_| |___|___|_| |__,|___|_| |__,|_|_|_|  
                                                   |___|                                           ",
        @"                                                                                                             __ 
 _ _ _     _                      _          _   _          _____ _         _      _____         _       _  |  |
| | | |___| |___ ___ _____ ___   | |_ ___   | |_| |_ ___   | __  | |___ ___| |_   |     |___ ___| |_ ___| |_|  |
| | | | -_| |  _| . |     | -_|  |  _| . |  |  _|   | -_|  | __ -| | .'|  _| '_|  | | | | .'|  _| '_| -_|  _|__|
|_____|___|_|___|___|_|_|_|___|  |_| |___|  |_| |_|_|___|  |_____|_|__,|___|_,_|  |_|_|_|__,|_| |_,_|___|_| |__|
                                                                                                                ",
        @"                                                    
 _____ _       _            _____         _         
|     | |_ ___| |_ _ _     |_   _|___ ___| |_ _ _   
|-   -|  _|  _|   | | |_     | | | .'|_ -|  _| | |_ 
|_____|_| |___|_|_|_  |_|    |_| |__,|___|_| |_  |_|
                  |___|                      |___|
                                                    ",
        @"                   _                                       _____ 
 _ _ _ _       _  | |                      _           _  |___  |
| | | | |_ ___| |_|_|___ ___    _ _ ___   | |_ _ _ _ _|_|___|  _|
| | | |   | .'|  _| |  _| -_|  | | | .'|  | . | | | | | |   |_|  
|_____|_|_|__,|_|   |_| |___|  |_  |__,|  |___|___|_  |_|_|_|_|  
                               |___|              |___|
                                                                 ",
        @"                                                  _____ 
 _ _ _ _       _      _                          |___  |
| | | | |_ ___| |_   |_|___    ___    _____ ___ ___|  _|
| | | |   | .'|  _|  | |_ -|  | .'|  |     | .'|   |_|  
|_____|_|_|__,|_|    |_|___|  |__,|  |_|_|_|__,|_|_|_|  
                                                        ",
        @"                                                    
 _____ _         _            _____     _ _       _ 
|   __|_|___ ___|_|___ ___   |     |___|_| |___ _| |
|   __| |_ -|_ -| | . |   |  | | | | .'| | | -_| . |
|__|  |_|___|___|_|___|_|_|  |_|_|_|__,|_|_|___|___|
                                                    ",
        @"                             
 _____       _               
|  |  |_____| |_ ___ ___ ___ 
|  |  |     | . | .'|_ -| .'|
|_____|_|_|_|___|__,|___|__,|
                             ",
        @"               _                              
 __        _  | |                             
|  |   ___| |_|_|___    _____ ___ ___ ___ _ _ 
|  |__| -_|  _| |_ -|  |     | . |_ -| -_| | |
|_____|___|_|   |___|  |_|_|_|___|___|___|_  |
                                         |___|
                                              ",
        @"       _=,_
    o_/6 /#\
    \__ |##/
     ='|--\
       /   #'-.
       \#|_   _'-. /
        |/ \_( # |'' 
       C/ ,--___/
                    "
    };
}
