using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.IO;
using Common;

namespace SCADAServer
{
    class SCADAServer
    {
        // Konstante
        private const int UDP_INIT_PORT = 50000;
        private const int TCP_PORT = 50001;
        private const int MAX_DEVICES = 20;
        private const int BUFFER_SIZE = 8192;
        private const int SELECT_TIMEOUT = 1000000; // 1 sekunda
        private const int QUERY_INTERVAL = 3000; // 3 sekunde

        // Utičnice
        private static Socket? udpSocket;
        private static Socket? tcpListenSocket;
        private static List<Socket> tcpClientSockets = new List<Socket>();

        // KRITIČNO: Mapiranje TCP soketa na Device ID
        private static Dictionary<Socket, int> socketToDeviceId = new Dictionary<Socket, int>();
        private static Dictionary<int, Socket> deviceIdToSocket = new Dictionary<int, Socket>();

        // Praćenje uređaja
        private static Dictionary<int, DeviceStatus> deviceStatuses = new Dictionary<int, DeviceStatus>();

        // Write operacije - logging
        private static List<string> writeLog = new List<string>();
        private static StreamWriter? logWriter;

        // Vremenske oznake
        private static Stopwatch queryTimer = new Stopwatch();
        private static int cycleCount = 0;
        private static int totalWritesSent = 0;
        private static int successfulWrites = 0;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║               SCADA SERVER - POKRETANJE                       ║");
            Console.WriteLine("║            sa naprednim Write funkcionalnostima               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // NOVO - Pitaj korisnika da li želi da automatski pokrene Device-e
            Console.Write("Da li želite automatski da pokrenete Device klijente? (d/n): ");
            string? odgovor = Console.ReadLine();

            if (odgovor?.ToLower() == "d" || odgovor?.ToLower() == "da")
            {
                Console.Write("\nKoliko Device instanci želite? (1-20): ");
                if (int.TryParse(Console.ReadLine(), out int broj) && broj >= 1 && broj <= 20)
                {
                    PokreniDeviceKlijente(broj);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Nevažeći broj! Pokrećem 4 Device-a...");
                    Console.ResetColor();
                    PokreniDeviceKlijente(4);
                }

                Console.WriteLine("\nČekam 3 sekunde da se Device-i pokrenu...");
                System.Threading.Thread.Sleep(3000);
            }

            Console.WriteLine();

            try
            {
                InitializeServer();
                InitializeLogging();
                RunServer();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[KRITIČNA GREŠKA] {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                Cleanup();
            }

            Console.WriteLine("\n\nPritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }

        static void InitializeLogging()
        {
            try
            {
                string logFileName = $"SCADA_WriteLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
                logWriter = new StreamWriter(logFileName, true);
                logWriter.AutoFlush = true;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[LOGGING] Write log fajl kreiran: {logFileName}\n");
                Console.ResetColor();

                LogWrite("========== SCADA SERVER WRITE LOG ==========");
                LogWrite($"Pokretanje: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogWrite("============================================");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING] Nije moguce kreirati log fajl: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        static void LogWrite(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";
            writeLog.Add(logEntry);
            logWriter?.WriteLine(logEntry);
        }

        static void InitializeServer()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           INICIJALIZACIJA SCADA SERVERA                       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // UDP utičnica za inicijalizaciju
            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_INIT_PORT);
            udpSocket.Bind(udpEndPoint);
            udpSocket.Blocking = false;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[UDP SOCKET] Pokrenuta inicijalizaciona uticnica");
            Console.ResetColor();
            Console.WriteLine($"             |- Adresa: {udpEndPoint}");
            Console.WriteLine($"             |- Funkcija: Prijem inicijalizacija uredjaja");
            Console.WriteLine($"             '- Rezim: Neblokirajuci\n");

            // TCP utičnica za komunikaciju
            tcpListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint tcpEndPoint = new IPEndPoint(IPAddress.Any, TCP_PORT);
            tcpListenSocket.Bind(tcpEndPoint);
            tcpListenSocket.Listen(MAX_DEVICES);
            tcpListenSocket.Blocking = false;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[TCP SOCKET] Pokrenuta listen uticnica");
            Console.ResetColor();
            Console.WriteLine($"             |- Adresa: {tcpEndPoint}");
            Console.WriteLine($"             |- Funkcija: Periodicna Read/Write komunikacija");
            Console.WriteLine($"             |- Max konekcija: {MAX_DEVICES}");
            Console.WriteLine($"             '- Rezim: Neblokirajuci\n");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  Server je spreman za prijem uredjaja!");
            Console.WriteLine("  Komande:");
            Console.WriteLine("    'q' - Query (upit o uredjaju)");
            Console.WriteLine("    'w' - Write (rucno slanje vrednosti)");
            Console.WriteLine("    'l' - Log (prikaz Write istorije)");
            Console.WriteLine("    's' - Statistics (statistika Write operacija)");
            Console.WriteLine("    ESC - Izlaz");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();

            queryTimer.Start();
        }

        static void RunServer()
        {
            byte[] buffer = new byte[BUFFER_SIZE];

            while (true)
            {
                cycleCount++;

                // Priprema za Select
                List<Socket> checkRead = new List<Socket>();
                List<Socket> checkError = new List<Socket>();

                if (udpSocket != null)
                {
                    checkRead.Add(udpSocket);
                    checkError.Add(udpSocket);
                }

                if (tcpListenSocket != null && tcpClientSockets.Count < MAX_DEVICES)
                {
                    checkRead.Add(tcpListenSocket);
                    checkError.Add(tcpListenSocket);
                }

                foreach (Socket clientSocket in tcpClientSockets)
                {
                    checkRead.Add(clientSocket);
                    checkError.Add(clientSocket);
                }

                // Select sa timeoutom
                Socket.Select(checkRead, null, checkError, SELECT_TIMEOUT);

                // Obrada grešaka
                if (checkError.Count > 0)
                {
                    HandleErrors(checkError);
                }

                // Obrada događaja čitanja
                if (checkRead.Count > 0)
                {
                    foreach (Socket socket in checkRead)
                    {
                        if (socket == udpSocket)
                        {
                            HandleUDPInitialization(buffer);
                        }
                        else if (socket == tcpListenSocket)
                        {
                            HandleTCPConnection();
                        }
                        else
                        {
                            HandleTCPData(socket, buffer);
                        }
                    }
                }

                // Periodično slanje upita uređajima (Read/Write)
                if (queryTimer.ElapsedMilliseconds >= QUERY_INTERVAL)
                {
                    if (tcpClientSockets.Count > 0)
                    {
                        QueryAllDevices();
                    }
                    queryTimer.Restart();
                }

                // Prikazivanje statusa svakih 10 ciklusa
                if (cycleCount % 10 == 0 && deviceStatuses.Count > 0)
                {
                    DisplayDeviceStatuses();
                }

                // Provera korisničkog unosa
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n[SISTEM] Server se gasi...");
                        Console.ResetColor();
                        break;
                    }
                    else if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        HandleUserQuery();
                    }
                    else if (key.KeyChar == 'w' || key.KeyChar == 'W')
                    {
                        HandleManualWrite();
                    }
                    else if (key.KeyChar == 'l' || key.KeyChar == 'L')
                    {
                        DisplayWriteLog();
                    }
                    else if (key.KeyChar == 's' || key.KeyChar == 'S')
                    {
                        DisplayWriteStatistics();
                    }
                }
            }
        }

        static void HandleUDPInitialization(byte[] buffer)
        {
            if (udpSocket == null) return;

            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                int bytesReceived = udpSocket.ReceiveFrom(buffer, ref senderEP);
                var config = SerializationHelper.Deserialize<DeviceConfiguration>(buffer, 0, bytesReceived);

                if (config != null)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║          UDP INICIJALIZACIJA NOVOG UREDJAJA                   ║");
                    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[UDP] Primljena inicijalizacija od: {senderEP}");
                    Console.WriteLine($"[UDP] Broj primljenih bajtova: {bytesReceived}");
                    Console.ResetColor();

                    Console.WriteLine("\n--- PRIMLJENI PODACI O UREDJAJU ---");
                    Console.WriteLine($"  • ID uredjaja:          {config.DeviceID}");
                    Console.WriteLine($"  • Tip uredjaja:         {config.Type}");
                    Console.WriteLine($"  • Minimalna vrednost:  {config.MinValue} {config.Quantity}");
                    Console.WriteLine($"  • Maksimalna vrednost: {config.MaxValue} {config.Quantity}");
                    Console.WriteLine($"  • Fizicka velicina:    {config.Quantity}");
                    Console.WriteLine($"  • Smer (Ulaz/Izlaz):   {(config.IsInput ? "ULAZ (Input - samo Read)" : "IZLAZ (Output - Read i Write)")}");
                    Console.WriteLine($"  • IP adresa uredjaja:   {config.IPAddress}");
                    Console.WriteLine($"  • Port za komunikaciju: {config.Port}");

                    // Dodaj uređaj u evidenciju
                    if (!deviceStatuses.ContainsKey(config.DeviceID))
                    {
                        deviceStatuses[config.DeviceID] = new DeviceStatus(config);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n[STATUS] Uredjaj uspesno dodat u sistem!");
                        Console.ResetColor();
                        Console.WriteLine($"[STATUS] -> Ukupno registrovanih uredjaja: {deviceStatuses.Count}/{MAX_DEVICES}");

                        LogWrite($"Novi uredjaj dodat: ID={config.DeviceID}, Tip={config.Type}, " +
                                $"IsInput={config.IsInput}, Range=[{config.MinValue}-{config.MaxValue}]");

                        SendUDPConfirmation(config, senderEP);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n[STATUS] UPOZORENJE: Uredjaj sa ovim ID-om vec postoji!");
                        Console.WriteLine($"[STATUS] -> Inicijalizacija odbijena - koristite drugi ID");
                        Console.ResetColor();
                    }

                    Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[UDP ERROR] Greska pri obradi inicijalizacije: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        static void SendUDPConfirmation(DeviceConfiguration config, EndPoint senderEP)
        {
            if (udpSocket == null) return;

            try
            {
                string confirmation = $"CONFIG_OK:{config.DeviceID}";
                byte[] confirmBytes = Encoding.UTF8.GetBytes(confirmation);

                udpSocket.SendTo(confirmBytes, senderEP);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[UDP] -> Slanje potvrde inicijalizacije uredjaju...");
                Console.ResetColor();
                Console.WriteLine($"[UDP]   |- Poruka: \"{confirmation}\"");
                Console.WriteLine($"[UDP]   '- Potvrda uspesno poslata na {senderEP}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[UDP ERROR] Greska pri slanju potvrde: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void HandleTCPConnection()
        {
            if (tcpListenSocket == null) return;

            try
            {
                Socket clientSocket = tcpListenSocket.Accept();
                clientSocket.Blocking = false;
                tcpClientSockets.Add(clientSocket);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[TCP] Nova TCP konekcija uspostavljena");
                Console.ResetColor();
                Console.WriteLine($"[TCP]   |- Remote adresa: {clientSocket.RemoteEndPoint}");
                Console.WriteLine($"[TCP]   |- Local adresa: {clientSocket.LocalEndPoint}");
                Console.WriteLine($"[TCP]   '- Ukupno povezanih: {tcpClientSockets.Count}\n");

                // VAŽNO: Sačekaj malo da uređaj pošalje identifikaciju
                System.Threading.Thread.Sleep(100);
                IdentifyConnectedDevice(clientSocket);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[TCP ERROR] Greska pri prihvatanju konekcije: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void IdentifyConnectedDevice(Socket clientSocket)
        {
            // NOVI PRISTUP: Koristi redosled konekcije
            // Prvi uredjaj koji se povezao = prvi registrovani nekonektovani uredjaj

            foreach (var kvp in deviceStatuses.OrderBy(x => x.Key))
            {
                int deviceId = kvp.Key;
                DeviceStatus status = kvp.Value;

                // Pronađi prvi nekonektovani uređaj
                if (!status.IsConnected)
                {
                    status.IsConnected = true;

                    // KRITIČNO: Mapiraj socket na device ID
                    socketToDeviceId[clientSocket] = deviceId;
                    deviceIdToSocket[deviceId] = clientSocket;

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[IDENTIFY] Uredjaj ID {deviceId} ({status.Configuration.Type}) uspesno identifikovan");
                    Console.WriteLine($"[IDENTIFY]   '- {(status.Configuration.IsInput ? "Read-Only (Input)" : "Read/Write (Output)")}\n");
                    Console.ResetColor();

                    LogWrite($"TCP konekcija mapirana: Socket -> DeviceID {deviceId}");
                    return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[IDENTIFY] WARNING: Konekcija bez registrovanog uredjaja");
            Console.ResetColor();
        }

        static void HandleTCPData(Socket clientSocket, byte[] buffer)
        {
            try
            {
                int bytesReceived = clientSocket.Receive(buffer);

                if (bytesReceived == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n[TCP] Uredjaj zatvorio konekciju: {clientSocket.RemoteEndPoint}");
                    Console.ResetColor();
                    DisconnectDevice(clientSocket);
                    return;
                }

                var response = SerializationHelper.Deserialize<DeviceResponse>(buffer, 0, bytesReceived);

                if (response != null)
                {
                    ProcessDeviceResponse(response, clientSocket);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
            {
               
                  
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[TCP ERROR] Greska pri prijemu podataka: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void ProcessDeviceResponse(DeviceResponse response, Socket clientSocket)
        {
            if (deviceStatuses.ContainsKey(response.DeviceID))
            {
                DeviceStatus status = deviceStatuses[response.DeviceID];
                double oldValue = status.LastValue;
                status.LastValue = response.CurrentValue;
                status.LastUpdate = DateTime.Now;
                status.IsConnected = true;

                // Provera alarmnog stanja
                if (response.IsAlarmActive || response.Status == ResponseStatus.AlarmActive)
                {
                    status.IsAlarmActive = true;
                    status.AlarmMessage = response.Message;

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║                    ALARM AKTIVIRAN                            ║");
                    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                    Console.ResetColor();
                    HandleAlarm(status);
                }
                else
                {
                    if (status.IsAlarmActive)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\n[INFO] Alarm deaktiviran za uredjaj ID {response.DeviceID}");
                        Console.ResetColor();
                        LogWrite($"Alarm deaktiviran: ID={response.DeviceID}");
                    }
                    status.IsAlarmActive = false;
                    status.AlarmMessage = string.Empty;
                }

                if (response.Status == ResponseStatus.Success)
                {
                    // Proveri da li je ovo odgovor na Write
                    if (Math.Abs(oldValue - response.CurrentValue) > 0.1)
                    {
                        successfulWrites++;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\n[WRITE SUCCESS] Uredjaj ID {response.DeviceID} promenio vrednost");
                        Console.ResetColor();
                        Console.WriteLine($"[WRITE]   |- Stara vrednost: {oldValue:F2} {status.Configuration.Quantity}");
                        Console.WriteLine($"[WRITE]   '- Nova vrednost: {response.CurrentValue:F2} {status.Configuration.Quantity}");

                        LogWrite($"Write uspesan: ID={response.DeviceID}, {oldValue:F2} -> {response.CurrentValue:F2} {status.Configuration.Quantity}");
                    }
                }
                else if (response.Status == ResponseStatus.Error)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR] Uredjaj ID {response.DeviceID}: {response.Message}");
                    Console.ResetColor();
                    LogWrite($"ERROR: ID={response.DeviceID}, Message={response.Message}");
                }
            }
        }

        static void HandleAlarm(DeviceStatus status)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ALARM] Uredjaj ID: {status.Configuration.DeviceID}");
            Console.WriteLine($"[ALARM] Tip: {status.Configuration.Type}");
            Console.WriteLine($"[ALARM] Trenutna vrednost: {status.LastValue:F2} {status.Configuration.Quantity}");
            Console.WriteLine($"[ALARM] Poruka: {status.AlarmMessage}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ALARM] Vreme: {DateTime.Now:HH:mm:ss}");
            Console.WriteLine($"[ALARM] Akcija: Notifikacija operatora poslata");
            Console.ResetColor();
            Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

            LogWrite($"ALARM: ID={status.Configuration.DeviceID}, Tip={status.Configuration.Type}, " +
                    $"Vrednost={status.LastValue:F2}, Poruka={status.AlarmMessage}");
        }

        static void QueryAllDevices()
        {
            // ISPRAVLJENO: Koristi mapu socketToDeviceId
            foreach (Socket clientSocket in tcpClientSockets.ToList())
            {
                try
                {
                    // Pronađi Device ID koristeći mapu
                    if (!socketToDeviceId.ContainsKey(clientSocket))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WARNING] Socket nije mapiran na Device ID");
                        Console.ResetColor();
                        continue;
                    }

                    int deviceId = socketToDeviceId[clientSocket];

                    if (!deviceStatuses.ContainsKey(deviceId))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[WARNING] Device ID {deviceId} ne postoji u statusima");
                        Console.ResetColor();
                        continue;
                    }

                    DeviceStatus deviceStatus = deviceStatuses[deviceId];
                    DeviceRequest request;

                    // AUTOMATSKA WRITE LOGIKA
                    if (deviceStatus.Configuration.IsInput)
                    {
                        // Input uređaji - SAMO ČITANJE
                        request = new DeviceRequest(RequestType.Read);
                    }
                    else
                    {
                        // Output uređaji - pametno upravljanje (Read ili Write)
                        if (ShouldWriteToDevice(deviceStatus))
                        {
                            double targetValue = CalculateTargetValue(deviceStatus);
                            request = new DeviceRequest(RequestType.Write, targetValue);
                            totalWritesSent++;

                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"\n[AUTO-WRITE] Automatsko slanje Write zahteva");
                            Console.WriteLine($"[AUTO-WRITE]   |- Uredjaj: ID {deviceStatus.Configuration.DeviceID} ({deviceStatus.Configuration.Type})");
                            Console.WriteLine($"[AUTO-WRITE]   |- Razlog: {GetWriteReason(deviceStatus)}");
                            Console.WriteLine($"[AUTO-WRITE]   |- Trenutno: {deviceStatus.LastValue:F2} {deviceStatus.Configuration.Quantity}");
                            Console.WriteLine($"[AUTO-WRITE]   '- Cilj: {targetValue:F2} {deviceStatus.Configuration.Quantity}");
                            Console.ResetColor();

                            LogWrite($"Auto-Write: ID={deviceStatus.Configuration.DeviceID}, " +
                                    $"Razlog={GetWriteReason(deviceStatus)}, " +
                                    $"{deviceStatus.LastValue:F2} -> {targetValue:F2}");
                        }
                        else
                        {
                            request = new DeviceRequest(RequestType.Read);
                        }
                    }

                    byte[] requestData = SerializationHelper.Serialize(request);
                    clientSocket.Send(requestData);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[QUERY ERROR] Greska pri slanju upita: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        static bool ShouldWriteToDevice(DeviceStatus status)
        {
            // NAPREDNA LOGIKA: Kada treba pisati?
            switch (status.Configuration.Type)
            {
                case DeviceType.Baterija:
                    // Piši ako je:
                    // 1. Baterija skoro puna (>90%) - smanji punjenje
                    // 2. Baterija skoro prazna (<15%) - povećaj punjenje
                    // 3. U opasnoj zoni (>95% ili <10%)
                    return status.LastValue > 90 || status.LastValue < 15;

                case DeviceType.Potrosac:
                    // Piši ako je:
                    // 1. Preopterećen (>75%) - smanji potrošnju
                    // 2. Neoptimalan (>80% ili <20%)
                    double utilization = status.LastValue / status.Configuration.MaxValue;
                    return utilization > 0.75 || utilization < 0.20;

                default:
                    return false;
            }
        }

        static double CalculateTargetValue(DeviceStatus status)
        {
            // INTELIGENTNO IZRAČUNAVANJE ciljne vrednosti
            switch (status.Configuration.Type)
            {
                case DeviceType.Baterija:
                    if (status.LastValue > 90)
                    {
                        // Puna - drži na 80%
                        return 80.0;
                    }
                    else if (status.LastValue < 15)
                    {
                        // Prazna - povećaj na 35%
                        return 35.0;
                    }
                    return 50.0; // Optimum

                case DeviceType.Potrosac:
                    double utilization = status.LastValue / status.Configuration.MaxValue;

                    if (utilization > 0.75)
                    {
                        // Preopterećen - smanji na 60%
                        return status.Configuration.MaxValue * 0.60;
                    }
                    else if (utilization < 0.20)
                    {
                        // Premalo - povećaj na 40%
                        return status.Configuration.MaxValue * 0.40;
                    }
                    return status.Configuration.MaxValue * 0.50; // Optimum 50%

                default:
                    return (status.Configuration.MinValue + status.Configuration.MaxValue) / 2;
            }
        }

        static string GetWriteReason(DeviceStatus status)
        {
            switch (status.Configuration.Type)
            {
                case DeviceType.Baterija:
                    if (status.LastValue > 90)
                        return "Baterija skoro puna - smanjujem punjenje";
                    else if (status.LastValue < 15)
                        return "Baterija skoro prazna - povecavam punjenje";
                    return "Optimizacija nivoa baterije";

                case DeviceType.Potrosac:
                    double util = status.LastValue / status.Configuration.MaxValue;
                    if (util > 0.75)
                        return "Preopterecenje - smanjujem potrosnju";
                    else if (util < 0.20)
                        return "Niska potrosnja - optimizujem";
                    return "Balansiranje potrosnje";

                default:
                    return "Automatska optimizacija";
            }
        }

        static void DisplayDeviceStatuses()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║          STATUS UREDJAJA - {DateTime.Now:HH:mm:ss} | Aktivnih:{deviceStatuses.Count}/{MAX_DEVICES}           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            foreach (var status in deviceStatuses.Values.OrderBy(s => s.Configuration.DeviceID))
            {
                string connectionIcon = status.IsConnected ? "[ON]" : "[OFF]";
                ConsoleColor connectionColor = status.IsConnected ? ConsoleColor.Green : ConsoleColor.Gray;

                Console.ForegroundColor = connectionColor;
                Console.Write($"{connectionIcon} ");
                Console.ResetColor();

                Console.Write($"ID:{status.Configuration.DeviceID,-3} ");
                Console.Write($"{status.Configuration.Type,-15} | ");
                Console.Write($"Vrednost: {status.LastValue,7:F2} {status.Configuration.Quantity,-2} | ");
                Console.Write($"Update: {status.LastUpdate:HH:mm:ss}");

                // Oznaka za R/W
                if (!status.Configuration.IsInput)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write(" | R/W");
                    Console.ResetColor();
                }

                if (status.IsAlarmActive)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($" | [ALARM]");
                    Console.ResetColor();
                }

                Console.WriteLine();

                if (status.IsAlarmActive)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    '-> {status.AlarmMessage}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
        }

        static void HandleUserQuery()
        {
            Console.Write("\n[UPIT] Unesite ID uredjaja: ");
            string? input = Console.ReadLine();

            if (int.TryParse(input, out int deviceId))
            {
                if (deviceStatuses.ContainsKey(deviceId))
                {
                    DeviceStatus status = deviceStatuses[deviceId];

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║                   DETALJI UREDJAJA                            ║");
                    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                    Console.ResetColor();

                    Console.WriteLine($"  ID: {status.Configuration.DeviceID}");
                    Console.WriteLine($"  Tip: {status.Configuration.Type}");
                    Console.WriteLine($"  Opseg: [{status.Configuration.MinValue} - {status.Configuration.MaxValue}] {status.Configuration.Quantity}");
                    Console.WriteLine($"  Smer: {(status.Configuration.IsInput ? "Ulaz (Read-Only)" : "Izlaz (Read/Write)")}");
                    Console.WriteLine($"  Trenutna vrednost: {status.LastValue:F2} {status.Configuration.Quantity}");
                    Console.WriteLine($"  Poslednji update: {status.LastUpdate:dd.MM.yyyy HH:mm:ss}");

                    if (status.IsConnected)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  Status konekcije: [POVEZAN]");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine($"  Status konekcije: [DISKONEKTOVAN]");
                        Console.ResetColor();
                    }

                    if (status.IsAlarmActive)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ALARM: {status.AlarmMessage}");
                        Console.ResetColor();
                    }

                    Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR] Uredjaj sa ID {deviceId} ne postoji!\n");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[ERROR] Nevazeci ID!\n");
                Console.ResetColor();
            }
        }

        static void HandleManualWrite()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              RUCNO SLANJE WRITE ZAHTEVA                       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            // Prikaži dostupne OUTPUT uređaje
            var outputDevices = deviceStatuses.Values.Where(d => !d.Configuration.IsInput && d.IsConnected).ToList();

            if (outputDevices.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[INFO] Nema dostupnih OUTPUT uredjaja za Write operaciju.");
                Console.WriteLine("[INFO] Write je moguc samo na: Baterija, Potrosac");
                Console.WriteLine("[INFO] Uredjaji moraju biti POVEZANI!\n");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("\nDostupni OUTPUT uredjaji:");
            foreach (var device in outputDevices)
            {
                string recommendation = "";
                if (ShouldWriteToDevice(device))
                {
                    recommendation = $"[Preporuceno: {CalculateTargetValue(device):F2}]";
                }

                Console.WriteLine($"  ID: {device.Configuration.DeviceID} | {device.Configuration.Type,-15} | " +
                                $"Trenutno: {device.LastValue,6:F2} {device.Configuration.Quantity} | " +
                                $"Opseg: [{device.Configuration.MinValue,4:F0}-{device.Configuration.MaxValue,4:F0}] {recommendation}");
            }

            Console.Write("\n[WRITE] Unesite ID uredjaja: ");
            string? idInput = Console.ReadLine();

            if (!int.TryParse(idInput, out int deviceId))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Nevazeci ID!\n");
                Console.ResetColor();
                return;
            }

            if (!deviceStatuses.ContainsKey(deviceId))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Uredjaj sa ID {deviceId} ne postoji!\n");
                Console.ResetColor();
                return;
            }

            DeviceStatus status = deviceStatuses[deviceId];

            if (status.Configuration.IsInput)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Uredjaj ID {deviceId} je INPUT uredjaj ({status.Configuration.Type})");
                Console.WriteLine($"[ERROR] Write operacija nije dozvoljena na INPUT uredjajima!\n");
                Console.ResetColor();
                return;
            }

            if (!status.IsConnected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Uredjaj ID {deviceId} nije povezan!\n");
                Console.ResetColor();
                return;
            }

            // Preporuka
            if (ShouldWriteToDevice(status))
            {
                double recommended = CalculateTargetValue(status);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nPREPORUKA: {GetWriteReason(status)}");
                Console.WriteLine($"   Preporucena vrednost: {recommended:F2} {status.Configuration.Quantity}");
                Console.ResetColor();
            }

            Console.Write($"\n[WRITE] Unesite novu vrednost ({status.Configuration.MinValue}-{status.Configuration.MaxValue} {status.Configuration.Quantity}): ");
            string? valueInput = Console.ReadLine();

            if (!double.TryParse(valueInput, out double newValue))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Nevazeca vrednost!\n");
                Console.ResetColor();
                return;
            }

            if (newValue < status.Configuration.MinValue || newValue > status.Configuration.MaxValue)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Vrednost {newValue} van opsega [{status.Configuration.MinValue}-{status.Configuration.MaxValue}]!\n");
                Console.ResetColor();
                return;
            }

            // ISPRAVLJENO: Koristi deviceIdToSocket mapu
            if (!deviceIdToSocket.ContainsKey(deviceId))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Nije pronadjena TCP konekcija za uredjaj ID {deviceId}!\n");
                Console.ResetColor();
                return;
            }

            Socket targetSocket = deviceIdToSocket[deviceId];

            try
            {
                DeviceRequest request = new DeviceRequest(RequestType.Write, newValue);
                byte[] requestData = SerializationHelper.Serialize(request);
                targetSocket.Send(requestData);
                totalWritesSent++;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nWrite zahtev uspesno poslat!");
                Console.ResetColor();
                Console.WriteLine($"[WRITE]   |- Uredjaj: ID {deviceId} ({status.Configuration.Type})");
                Console.WriteLine($"[WRITE]   |- Stara vrednost: {status.LastValue:F2} {status.Configuration.Quantity}");
                Console.WriteLine($"[WRITE]   '- Nova vrednost: {newValue:F2} {status.Configuration.Quantity}");
                Console.WriteLine("\n[INFO] Cekam potvrdu od uredjaja...\n");

                LogWrite($"Manual-Write: ID={deviceId}, User request, {status.LastValue:F2} -> {newValue:F2}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] Greska pri slanju: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        static void DisplayWriteLog()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  WRITE OPERACIJE - LOG                        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            if (writeLog.Count == 0)
            {
                Console.WriteLine("\n  Nema zabelezenih Write operacija.\n");
                return;
            }

            int displayCount = Math.Min(20, writeLog.Count);
            Console.WriteLine($"\nPoslednjih {displayCount} dogadjaja:\n");

            for (int i = writeLog.Count - displayCount; i < writeLog.Count; i++)
            {
                Console.WriteLine($"  {writeLog[i]}");
            }

            Console.WriteLine($"\n═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"Ukupno dogadjaja: {writeLog.Count}\n");
        }

        static void DisplayWriteStatistics()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              WRITE OPERACIJE - STATISTIKA                     ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            int outputDevices = deviceStatuses.Values.Count(d => !d.Configuration.IsInput);
            int connectedOutput = deviceStatuses.Values.Count(d => !d.Configuration.IsInput && d.IsConnected);

            double successRate = totalWritesSent > 0 ? (successfulWrites * 100.0 / totalWritesSent) : 0;

            Console.WriteLine("\nOpsta statistika:");
            Console.WriteLine($"  • Ukupno Write zahteva poslato: {totalWritesSent}");
            Console.WriteLine($"  • Uspesno izvrsenih Write-ova: {successfulWrites}");
            Console.WriteLine($"  • Stopa uspeha: {successRate:F1}%");
            Console.WriteLine($"  • OUTPUT uredjaja (podrzavaju Write): {outputDevices}");
            Console.WriteLine($"  • Trenutno povezanih OUTPUT uredjaja: {connectedOutput}");

            Console.WriteLine("\nUredjaji sa Write podrskom:");
            foreach (var device in deviceStatuses.Values.Where(d => !d.Configuration.IsInput).OrderBy(d => d.Configuration.DeviceID))
            {
                string status = device.IsConnected ? "[ON]" : "[OFF]";
                ConsoleColor color = device.IsConnected ? ConsoleColor.Green : ConsoleColor.Gray;

                Console.ForegroundColor = color;
                Console.Write($"  {status} ");
                Console.ResetColor();
                Console.WriteLine($"ID:{device.Configuration.DeviceID} {device.Configuration.Type,-15} | " +
                                $"Trenutno: {device.LastValue,6:F2} {device.Configuration.Quantity}");
            }

            Console.WriteLine($"\n═══════════════════════════════════════════════════════════════\n");
        }

        static void DisconnectDevice(Socket clientSocket)
        {
            // ISPRAVLJENO: Koristi mapu
            if (socketToDeviceId.ContainsKey(clientSocket))
            {
                int deviceId = socketToDeviceId[clientSocket];

                if (deviceStatuses.ContainsKey(deviceId))
                {
                    deviceStatuses[deviceId].IsConnected = false;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[TCP] Uredjaj ID {deviceId} oznacen kao diskonektovan\n");
                    Console.ResetColor();

                    LogWrite($"Diskonektovan: ID={deviceId}");
                }

                // Ukloni iz mapa
                socketToDeviceId.Remove(clientSocket);
                deviceIdToSocket.Remove(deviceId);
            }

            clientSocket.Close();
            tcpClientSockets.Remove(clientSocket);
        }

        static void HandleErrors(List<Socket> errorSockets)
        {
            foreach (Socket socket in errorSockets)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] Greska na uticnici: {socket.LocalEndPoint}");
                Console.ResetColor();

                if (socket == udpSocket || socket == tcpListenSocket)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[CRITICAL] Greska na glavnoj uticnici!");
                    Console.ResetColor();
                }
                else if (tcpClientSockets.Contains(socket))
                {
                    DisconnectDevice(socket);
                }
            }
        }

        static void Cleanup()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[CLEANUP] Zatvaranje svih konekcija...");
            Console.ResetColor();

            foreach (Socket socket in tcpClientSockets)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                catch { }
            }

            try { tcpListenSocket?.Close(); } catch { }
            try { udpSocket?.Close(); } catch { }

            // Zatvori log
            if (logWriter != null)
            {
                LogWrite($"Server zaustavljen: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                LogWrite($"Ukupno Write operacija: {totalWritesSent}");
                LogWrite($"Uspesnih: {successfulWrites}");
                LogWrite("============================================");
                logWriter.Close();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[CLEANUP] Sve uticnice zatvorene");
            Console.WriteLine($"[CLEANUP] Write log sacuvan ({writeLog.Count} dogadjaja)");
            Console.ResetColor();
        }

        static void PokreniDeviceKlijente(int broj)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          AUTOMATSKO POKRETANJE DEVICE KLIJENATA               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            try
            {
                // Pronađi Device.exe
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;

                // Probaj različite putanje
                string[] moguceputanje = new string[]
                {
            Path.Combine(currentDir, "..", "..", "..", "..", "Client", "bin", "Debug", "net10.0", "Device.exe"),
            Path.Combine(currentDir, "..", "..", "..", "..", "Client", "bin", "Release", "net10.0", "Device.exe"),
            Path.Combine(currentDir, "Device.exe"), // Ako je u istom folderu
            Path.Combine(currentDir, "..", "Device.exe"),
                };

                string? devicePath = null;
                foreach (var putanja in moguceputanje)
                {
                    string fullPath = Path.GetFullPath(putanja);
                    if (File.Exists(fullPath))
                    {
                        devicePath = fullPath;
                        break;
                    }
                }

                if (devicePath == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[ERROR] Device.exe nije pronađen!");
                    Console.WriteLine("[INFO] Pokrenite Device-e ručno.");
                    Console.ResetColor();
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[OK] Device.exe pronađen: {devicePath}");
                Console.ResetColor();
                Console.WriteLine();

                // Pokreni Device instance
                for (int i = 0; i < broj; i++)
                {
                    try
                    {
                        System.Diagnostics.Process proces = new System.Diagnostics.Process();
                        proces.StartInfo.FileName = devicePath;
                        proces.StartInfo.WorkingDirectory = Path.GetDirectoryName(devicePath);
                        proces.StartInfo.UseShellExecute = true; // Otvara u novom prozoru
                        proces.Start();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[{i + 1}/{broj}] ✓ Device #{i + 1} pokrenut");
                        Console.ResetColor();

                        System.Threading.Thread.Sleep(800); // Pauza između pokretanja
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{i + 1}/{broj}] ✗ Greška: {ex.Message}");
                        Console.ResetColor();
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Uspešno pokrenuto {broj} Device instanci!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] Kritična greška pri pokretanju Device-a: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}