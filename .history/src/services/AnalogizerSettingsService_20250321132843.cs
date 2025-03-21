
namespace Pannella.Services;

using Pannella.Helpers;

//snac_game_cont_type = analogizer_config_s[4:0];
//snac_cont_assignment = analogizer_config_s[9:6];
//analogizer_video_type = analogizer_config_s[13:10];
//analogizer_ena = analogizer_config_s[5];
//pocket_blank_screen = analogizer_config_s[14];
//analogizer_osd_out = analogizer_config_s[15];
class AnalogizerSettingsService
{
    static string NL = Environment.NewLine; // shortcut
    static string NORMAL = Console.IsOutputRedirected ? "" : "\x1b[39m";
    static string RED = Console.IsOutputRedirected ? "" : "\x1b[91m";
    static string GREEN = Console.IsOutputRedirected ? "" : "\x1b[92m";
    static string YELLOW = Console.IsOutputRedirected ? "" : "\x1b[93m";
    static string BLUE = Console.IsOutputRedirected ? "" : "\x1b[94m";
    static string MAGENTA = Console.IsOutputRedirected ? "" : "\x1b[95m";
    static string CYAN = Console.IsOutputRedirected ? "" : "\x1b[96m";
    static string GREY = Console.IsOutputRedirected ? "" : "\x1b[97m";
    static string BOLD = Console.IsOutputRedirected ? "" : "\x1b[1m";
    static string NOBOLD = Console.IsOutputRedirected ? "" : "\x1b[22m";
    static string UNDERLINE = Console.IsOutputRedirected ? "" : "\x1b[4m";
    static string NOUNDERLINE = Console.IsOutputRedirected ? "" : "\x1b[24m";
    static string REVERSE = Console.IsOutputRedirected ? "" : "\x1b[7m";
    static string NOREVERSE = Console.IsOutputRedirected ? "" : "\x1b[27m";

    static int videoSelection = -1;
    static int snacAssigmentSelection = -1;
    static int snacSelection = -1;
    static readonly int analogizerEnaSelection = 1;
    static int pocketBlankScreenSelection =-1;
    static int analogizerOsdOutSelection = -1;
    static int analogizerRegionalSettings = -1;
    static string AnalogizerHeader = @"   ###   #     #   ###   #         ####   #######    #### ####### ####### #### 
  #...#  ##    #. #...#  #.       #....#  #.          #..  ....#. #...... #...#
 #.    # #.#   #.#.    # #.      #.     #.#.          #.      #.  #.      #.  #
 #.    #.#. #  #.#.    #.#.      #.     #.#.  ####    #.     #.   ####### ####.
 #######.#.  # #.#######.#.      #.     #.#.   ...#   #.    #.    #...... #.#. 
 #.....#.#.   ##.#.....#.#.      #.     #.#.      #.  #.   #.     #.      #. #.
 #.    #.#.    #.#.    #.#.       #.   #. #.      #.  #.  #.      #.      #.  #
 #.    #.#.    #.#.    #.#######   ####.   #######.  #### ####### ####### #.  #
===================== C O N F I G U R A T O R   V 0.4 =========================";

    static readonly Dictionary<int, string> VideoOutputOptions = new Dictionary<int, string>
    {
        {0, "RGBS"},
        {1, "RGsB"},
        {2, "YPbPr"},
        {3, "Y/C NTSC"},
        {4, "Y/C PAL"},
        {5, "SC 0% RGBHV"},
        {6, "SC 25% RGBHV"},
        {7, "SC 50% RGBHV"},
        {8, "SC 75% RGBHV"},
        {9, "SC HQ2x RGBHV"}
    };

    //static Dictionary<int, string> AnalogizerEnableOptions = new Dictionary<int, string>
    //{
    //    {1, "On"},
    //    {0, "Off"}
    //};

    static readonly Dictionary<int, string> SNACassigmentsOptions = new Dictionary<int, string>
    {
        {0, "SNAC P1 -> Pocket P1"},        //0x00
        {1, "SNAC P1 -> Pocket P2"},        //0x40
        {2, "SNAC P1,P2 -> Pocket P1,P2"},  //0x80
        {3, "SNAC P1,P2 -> Pocket P2,P1"},  //0xC0
        {4, "SNAC P1,P2 -> Pocket P3,P4"},  //0x100
        {5, "SNAC P1-P4 -> Pocket P1-P4"},  //0x200
    };

    static readonly Dictionary<int, string> SNACSelectionOptions = new Dictionary<int, string>
    {
        {0, "None - No SNAC gamepad, use Pocket and/or Dock controls"},
        {1, "DB15 Normal - Neogeo/Arcade using DB15 connector (normal polling speed)"},
        {2, "NES - Nintendo Entertainment System gamepad"},
        {3, "SNES - Super Nintendo gamepad"},
        {4, "PCE 2btn - PC Engine gamepad with 2 buttons"},
        {5, "PCE 6btn - PC Engine gamepad with 6 buttons"},
        {6, "PCE Multitap - Multitap for PC Engine"},
        {0x9, "DB15 Fast - Neogeo/Arcade using DB15 connector (fast polling speed)"},
        {0xb, "SNES A,B<->X,Y - SNES with remapped buttons"},
        {0x11, "PSX (Digital PAD) - PlayStation 1/2 digital gamepad"},
        {0x13, "PSX (Analog PAD) - PlayStation 1/2 analog gamepad"}
    };


    static readonly Dictionary<int, string> PocketBlankScreenOptions = new Dictionary<int, string>
    {
        {0, "Video is show on the Pocket screen"},
        {1, "No video output on the Pocket screen"}
    };

    static readonly Dictionary<int, string> AnalogizerOSDOptions = new Dictionary<int, string>
    {
        {1, "OSD is show on Analogizer video output (when avalaible)"},
        {0, "OSD is show on Pocket screen (when avalaible)"}
    };

    static readonly Dictionary<int, string> AnalogizerRegionalSettingsOptions = new Dictionary<int, string>
    {
        {0, "Auto > NTSC    (auto detect and disambiguates to NTSC)"},
        {1, "Auto > PAL     (auto detect and disambiguates to PAL)"},
        {2, "Auto > Another (auto detect and disambiguates to Another,e.g. NES Dendy)"},
        {3, "Force NTSC     (Force mode to NTSC)"},
        {4, "Force PAL      (Force mode to PAL)"},
        {5, "Force Another  (Force mode to Another,e.g. NES Dendy)"},
    };

    static void FlushKeyboard()
    {
        while (Console.In.Peek() != -1)
            Console.In.Read();
    }

    private static void SnacOptions()
    {
        while (snacSelection == -1)
        {
            ShowHeader();
            //Opciones de selección de mando SNAC
            // Console.WriteLine($"\n\n{GREY}{REVERSE}=== SNAC Game Controller Selection:==={NOREVERSE}{NORMAL}");
            Console.WriteLine($"====== SNAC GAME CONTROLLER SELECTION OPTIONS ======");
            foreach (var option in SNACSelectionOptions)
            {
                Console.WriteLine("{0}: {1}", option.Key, option.Value);
                //Console.WriteLine("{0}{1}: {2}{3}", GREEN, option.Key, NORMAL, option.Value);
            }
            //Console.WriteLine("");
            Console.Write($"Select an option:");
            if (int.TryParse(Console.ReadLine(), out int input) && SNACSelectionOptions.ContainsKey(input))
            {
                snacSelection = input;
            }
            else
            {
                FlushKeyboard();
                Console.WriteLine($"Option not valid.Try again.");
                Console.ReadLine(); // Espera a que el usuario presione Enter
                snacSelection = -1; // Reinicia la selección de video para repetir el menú completo
            }
        }
    }
    private static void SnacAssigmentsOptions()
    {
        while (snacAssigmentSelection == -1)
        {
            ShowHeader();
            //Opciones asignación de mando SNAC
            Console.WriteLine($"====== SNAC GAME CONTROLLER ASSIGMENTS OPTIONS ======");
            foreach (var option in SNACassigmentsOptions)
            {
                Console.WriteLine("{0}: {1}", option.Key, option.Value);
            }
            //Console.WriteLine("");
            Console.Write($"Select an option:");
            if (int.TryParse(Console.ReadLine(), out int input) && SNACassigmentsOptions.ContainsKey(input))
            {
                snacAssigmentSelection = input;
            }
            else
            {
                FlushKeyboard();
                Console.WriteLine($"Option not valid.Try again.");
                Console.ReadLine(); // Espera a que el usuario presione Enter
                snacAssigmentSelection = -1; // Reinicia la selección de video para repetir el menú completo
            }
        }
    }

    private static void VideoOptions()
    {
        while (videoSelection == -1)
        {
            ShowHeader();
            //Opciones de selección de salida de video
            Console.WriteLine($"====== VIDEO OUTPUT OPTIONS ======");
            foreach (var option in VideoOutputOptions)
            {
                Console.WriteLine("{0}: {1}", option.Key, option.Value);
            }
            //Console.WriteLine("");
            Console.Write($"Select an option:");
            if (int.TryParse(Console.ReadLine(), out int input) && VideoOutputOptions.ContainsKey(input))
            {
                videoSelection = input;
            }
            else
            {
                Console.WriteLine($"Option not valid.Try again.");
                Console.ReadLine(); // Espera a que el usuario presione Enter
                videoSelection = -1; // Reinicia la selección de video para repetir el menú completo
            }
        }
    }

    private static void BlankScreenOptions() 
    {
        
        while (pocketBlankScreenSelection == -1)
        {
            ShowHeader();
            //Opciones de pocket Blank Screen
            Console.WriteLine($"====== POCKET BLANK SCREEN OPTIONS ======");
            foreach (var option in PocketBlankScreenOptions)
            {
                Console.WriteLine("{0}: {1}", option.Key, option.Value);
            }
            //Console.WriteLine("");
            Console.Write($"Select an option:");
            if (int.TryParse(Console.ReadLine(), out int input) && PocketBlankScreenOptions.ContainsKey(input))
            {
                pocketBlankScreenSelection = input;
            }
            else
            {
                Console.WriteLine($"Option not valid.Try again.");
                Console.ReadLine(); // Espera a que el usuario presione Enter
                pocketBlankScreenSelection = -1; // Reinicia la selección de video para repetir el menú completo
            }
        }
    }
    private static void AnalogizerOSDoutOptions()
    {
        while (analogizerOsdOutSelection == -1)
        {
            ShowHeader();
            //Opciones de selección de salida de video
            Console.WriteLine($"====== ANALOGIZER OSD OPTIONS ======");
            foreach (var option in AnalogizerOSDOptions)
            {
                Console.WriteLine("{0}: {1}", option.Key, option.Value);
            }
            //Console.WriteLine("");
            Console.Write($"Select an option:");
            if (int.TryParse(Console.ReadLine(), out int input) && AnalogizerOSDOptions.ContainsKey(input))
            {
                analogizerOsdOutSelection = input;
            }
            else
            {
                Console.WriteLine($"Option not valid.Try again.");
                Console.ReadLine(); // Espera a que el usuario presione Enter
                analogizerOsdOutSelection = -1; // Reinicia la selección de video para repetir el menú completo
            }
        }
    }

    private static void RegionalSettingsOptions()
    {
        while (analogizerRegionalSettings == -1)
        {
            ShowHeader();
            //Opciones de selección regionales
            // Console.WriteLine($"\n\n{GREY}{REVERSE}=== SNAC Game Controller Selection:==={NOREVERSE}{NORMAL}");
            Console.WriteLine($"====== REGIONAL SETTINGS OPTIONS ======");
            foreach (var option in AnalogizerRegionalSettingsOptions)
            {
                Console.WriteLine("{0}: {1}", option.Key, option.Value);
                //Console.WriteLine("{0}{1}: {2}{3}", GREEN, option.Key, NORMAL, option.Value);
            }
            //Console.WriteLine("");
            Console.Write($"Select an option:");
            if (int.TryParse(Console.ReadLine(), out int input) && AnalogizerRegionalSettingsOptions.ContainsKey(input))
            {
                analogizerRegionalSettings = input;
            }
            else
            {
                FlushKeyboard();
                Console.WriteLine($"Option not valid.Try again.");
                Console.ReadLine(); // Espera a que el usuario presione Enter
                analogizerRegionalSettings = -1; // Reinicia la selección regional para repetir el menú completo
            }
        }
    }

    private static void ShowHeader()
    {
        Console.Clear();
        Console.WriteLine($"{AnalogizerHeader}");
        //Console.WriteLine($"======================= C U R R E N T   S E T T I N G S =======================");
        Console.WriteLine("SNAC Controller:     {0,-40}", snacSelection == -1 ? "-" : SNACSelectionOptions[snacSelection]);
        Console.WriteLine("SNAC Assigments:     {0,-40}", snacAssigmentSelection == -1 ? "-" : SNACassigmentsOptions[snacAssigmentSelection]);
        Console.WriteLine("Video output:        {0,-40}", videoSelection == -1 ? "-" : VideoOutputOptions[videoSelection]);
        Console.WriteLine("Pocket Blank Screen: {0,-40}", pocketBlankScreenSelection == -1 ? "-" : PocketBlankScreenOptions[pocketBlankScreenSelection]);
        Console.WriteLine("OSD output:          {0,-40}", analogizerOsdOutSelection == -1 ? "-" : AnalogizerOSDOptions[analogizerOsdOutSelection]);
        Console.WriteLine("Regional Settings:   {0,-40}", analogizerRegionalSettings == -1 ? "-" : AnalogizerRegionalSettingsOptions[analogizerRegionalSettings]);
        Console.WriteLine($"===============================================================================");
        //Console.WriteLine("");
    }
    public static void ShowWizard()
    {
        int menuDone = 1;

        while (menuDone != 6)
        {
            switch (menuDone)
            {
                case 1:
                {
                    //SNAC game controller options
                    SnacOptions();
                    menuDone++;
                    break;
                }
                case 2:
                {
                    if(snacSelection == 0) //If none SNAC controller is selected, bypass assigment
                        {
                            snacAssigmentSelection = 0; 
                        }
                    else
                        {
                            //SNAC game controller options
                            SnacAssigmentsOptions();
                        }
                    menuDone++;
                    break;
                }
                case 3:
                {
                    //Video output options
                    VideoOptions();
                    menuDone++;
                    break;
                }
                case 4:
                {
                    BlankScreenOptions();
                    menuDone++;
                    break;
                }
                case 5:
                {
                    //Miscellaneous options
                    AnalogizerOSDoutOptions();
                    menuDone++;
                    break;
                }
                default:
                    break;
            }
        }

        // Almacenar la selección en un archivo binario de 32 bits con big-endian
        uint data = (uint)((analogizerOsdOutSelection << 15) | (pocketBlankScreenSelection << 14) | (videoSelection << 10) | (snacAssigmentSelection << 6) | (analogizerEnaSelection << 5) | snacSelection); // Usamos uint para 32 bits
        byte[] buffer = BitConverter.GetBytes(data);
        //Array.Reverse(buffer); // Invertimos el arreglo para big-endian
        string filename = "analogizer.bin";
        string filepath = Path.Combine(ServiceHelper.UpdateDirectory, filename);

        //Assets/Analogizer/common
        File.WriteAllBytes(filepath, buffer);

        if (File.Exists(filepath))
        {
            string destination = Path.Combine(ServiceHelper.UpdateDirectory, "Assets", "analogizer", "common");

            if (!Directory.Exists(destination))
            {
                Directory.CreateDirectory(destination);
            }

            string destPath = Path.Combine(destination, filename);

            File.Move(filepath, destPath, true);
            Console.WriteLine($"Analogizer configuration saved to '{destPath}");
        }
    }
}
