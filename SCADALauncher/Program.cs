using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SCADALauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           SCADA SYSTEM - AUTO LAUNCHER                        ║");
            Console.WriteLine("║        Automatsko pokretanje servera i uredjaja              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[INFO] Priprema za pokretanje SCADA sistema...");
            Console.WriteLine("[INFO] Server + 4 Device klijenta");
            Console.ResetColor();
            Console.WriteLine();

            // Proverava da li postoje kompajlirani fajlovi
            string serverPath = FindExecutable("SCADAServer.exe");
            string devicePath = FindExecutable("Device.exe");

            if (string.IsNullOrEmpty(serverPath) || string.IsNullOrEmpty(devicePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Nisu pronadjeni kompajlirani fajlovi!");
                Console.WriteLine("[ERROR] Molimo kompajlirajte projekat pre pokretanja.");
                Console.ResetColor();
                Console.WriteLine("\nPritisnite bilo koji taster za izlaz...");
                Console.ReadKey();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[FOUND] Server: {serverPath}");
            Console.WriteLine($"[FOUND] Device: {devicePath}");
            Console.ResetColor();
            Console.WriteLine();

            PokreniKlijente(serverPath, devicePath, 4);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║             SVI PROCESI POKRENUTI                             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Pritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }

        static string FindExecutable(string fileName)
        {
            // Proveri u trenutnom direktorijumu
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            // Proveri u bin/Debug ili bin/Release
            string[] searchPaths = new string[]
            {
                Path.Combine("bin", "Debug", "net8.0", fileName),
                Path.Combine("bin", "Debug", "net7.0", fileName),
                Path.Combine("bin", "Debug", "net6.0", fileName),
                Path.Combine("bin", "Release", "net8.0", fileName),
                Path.Combine("bin", "Release", "net7.0", fileName),
                Path.Combine("bin", "Release", "net6.0", fileName),
                Path.Combine("..", "SCADAServer", "bin", "Debug", "net8.0", fileName),
                Path.Combine("..", "Device", "bin", "Debug", "net8.0", fileName),
            };

            foreach (string path in searchPaths)
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }

            return string.Empty;
        }

        static void PokreniKlijente(string serverPath, string devicePath, int brojKlijenata)
        {
            // 1. Pokreni Server
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[LAUNCH] Pokretanje SCADA Servera...");
            Console.ResetColor();

            Process serverProces = new Process();
            serverProces.StartInfo.FileName = serverPath;
            serverProces.StartInfo.UseShellExecute = true;
            serverProces.StartInfo.CreateNoWindow = false;
            serverProces.Start();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Server pokrenut (PID: {serverProces.Id})");
            Console.ResetColor();

            // Sacekaj da se server inicijalizuje
            Thread.Sleep(2000);

            // 2. Pokreni Device klijente
            for (int i = 0; i < brojKlijenata; i++)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[LAUNCH] Pokretanje Device #{i + 1}...");
                Console.ResetColor();

                Process klijentProces = new Process();
                klijentProces.StartInfo.FileName = devicePath;

                // Argument - broj klijenta (ID ce biti i + 1)
                klijentProces.StartInfo.Arguments = $"{i + 1}";
                klijentProces.StartInfo.UseShellExecute = true;
                klijentProces.StartInfo.CreateNoWindow = false;
                klijentProces.Start();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCCESS] Device #{i + 1} pokrenut (PID: {klijentProces.Id})");
                Console.ResetColor();

                // Sacekaj malo izmedju pokretanja
                Thread.Sleep(1000);
            }
        }
    }
}