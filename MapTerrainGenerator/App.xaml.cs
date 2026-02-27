using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace MapTerrainGeneratorWPF
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                // Attach to the command prompt that executed the app
                AttachConsole(ATTACH_PARENT_PROCESS);
                Console.WriteLine("\n--- Radiant Terrain Generator CLI ---");
                RunCLI(e.Args);
                Environment.Exit(0);
            }
            else
            {
                // No arguments: launch standard GUI
                base.OnStartup(e);
                MainWindow window = new MainWindow();
                window.Show();
            }
        }

        private void RunCLI(string[] args)
        {
            // Default Parameters
            string mode = "hint"; string file = ""; string output = "";
            double width = 2048, length = 2048, height = 256;
            double subX = 64, subY = 64;
            string texture = "common/caulk";
            int shapeType = 0; double shapeHeight = 256; double terrace = 0;
            int noiseType = 0; double variance = 32; double frequency = 0.005;
            bool overrideMap = false;

            // Parse Arguments
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToLower())
                    {
                        case "--mode": mode = args[++i].ToLower(); break;
                        case "--file": file = args[++i]; break;
                        case "--width": width = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--length": length = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--height": height = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--subx": subX = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--suby": subY = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--texture": texture = args[++i]; break;
                        case "--shape":
                            string s = args[++i].ToLower();
                            shapeType = s == "hill" ? 1 : s == "crater" ? 2 : s == "ridge" ? 3 : s == "slope" ? 4 : s == "volcano" ? 5 : s == "valley" ? 6 : 0;
                            break;
                        case "--shapeheight": shapeHeight = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--terrace": terrace = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--noise":
                            string n = args[++i].ToLower();
                            noiseType = n == "simplex" ? 1 : n == "random" ? 2 : 0;
                            break;
                        case "--variance": variance = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--frequency": frequency = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                        case "--out": output = args[++i]; break;
                        case "--override": overrideMap = true; break;
                        case "--help":
                            PrintHelp();
                            return;
                    }
                }

                Action<string> logger = msg => Console.WriteLine(msg);

                // Run Engine
                TerrainEngine.GenerateAndExport(
                    mode, file, width, length, height, subX, subY, texture,
                    shapeType, shapeHeight, terrace, noiseType, variance, frequency,
                    output, overrideMap, logger);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] Failed to parse arguments: {ex.Message}");
                Console.WriteLine("Use --help to see parameter formatting.");
            }
        }

        private void PrintHelp()
        {
            Console.WriteLine("Usage: MapTerrainGeneratorWPF.exe [arguments]");
            Console.WriteLine("  --mode <hint|manual>     : Target mode (default: hint)");
            Console.WriteLine("  --file <path>            : Path to target .map file");
            Console.WriteLine("  --width <num>            : Manual width (X)");
            Console.WriteLine("  --length <num>           : Manual length (Y)");
            Console.WriteLine("  --height <num>           : Manual max height (Z)");
            Console.WriteLine("  --subX <num>             : Sub-square size X (e.g. 64)");
            Console.WriteLine("  --subY <num>             : Sub-square size Y (e.g. 64)");
            Console.WriteLine("  --texture <name>         : Terrain face texture");
            Console.WriteLine("  --shape <type>           : flat, hill, crater, ridge, slope, volcano, valley");
            Console.WriteLine("  --shapeHeight <num>      : Peak/Depth height offset");
            Console.WriteLine("  --terrace <num>          : Z-height stair stepping (0 for smooth)");
            Console.WriteLine("  --noise <type>           : perlin, simplex, random");
            Console.WriteLine("  --variance <num>         : Max noise offset");
            Console.WriteLine("  --frequency <num>        : Noise scaling factor (e.g. 0.005)");
            Console.WriteLine("  --out <name>             : Output map name (if not overriding)");
            Console.WriteLine("  --override               : Flag to overwrite target .map in hint mode");
        }
    }
}