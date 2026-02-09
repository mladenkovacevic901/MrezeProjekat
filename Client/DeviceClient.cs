using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Common;

namespace Device
{
    class DeviceSimulator
    {
        // Konfiguracija uređaja
        private static DeviceConfiguration? config;
        private static double currentValue;
        private static Random random = new Random();

        // Mrežne komponente
        private static Socket? udpSocket;
        private static Socket? tcpSocket;
        private static bool isConnected = false;

        // Parametri
        private const string SERVER_IP = "127.0.0.1";
        private const int UDP_SERVER_PORT = 50000;
        private const int TCP_SERVER_PORT = 50001;
        private const int POLL_TIMEOUT_MS = 500;
        private const int MAX_POLL_ATTEMPTS = 20;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            DEVICE SIMULATOR - POKRETANJE                      ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // Automatska konfiguracija ako je prosleđen argument
            if (args.Length > 0 && int.TryParse(args[0], out int deviceId))
            {
                AutoConfigureDevice(deviceId);
            }
            else
            {
                ConfigureDevice();
            }

            try
            {
                InitializeViaUDP();
                ConnectViaTCP();
                RunDevicePolling();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[KRITICNA GRESKA] {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                Cleanup();
            }

            Console.WriteLine("\n\nPritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }

        static void AutoConfigureDevice(int deviceId)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("           AUTOMATSKA KONFIGURACIJA UREDJAJA");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();

            DeviceType type;
            double minValue, maxValue;
            PhysicalQuantity quantity;
            bool isInput;
            string typeName;

            // Automatski dodeljuje tip na osnovu ID-a
            switch (deviceId % 4)
            {
                case 1: // Solarni Panel
                    type = DeviceType.SolarniPanel;
                    minValue = 0;
                    maxValue = 5000;
                    quantity = PhysicalQuantity.W;
                    isInput = true;
                    typeName = "Solarni Panel";
                    break;

                case 2: // Vetrogenerator
                    type = DeviceType.Vetrogenerator;
                    minValue = 0;
                    maxValue = 3000;
                    quantity = PhysicalQuantity.W;
                    isInput = true;
                    typeName = "Vetrogenerator";
                    break;

                case 3: // Baterija
                    type = DeviceType.Baterija;
                    minValue = 0;
                    maxValue = 100;
                    quantity = PhysicalQuantity.P;
                    isInput = false;
                    typeName = "Baterija";
                    break;

                default: // Potrošač (case 0)
                    type = DeviceType.Potrosac;
                    minValue = 0;
                    maxValue = 2000;
                    quantity = PhysicalQuantity.W;
                    isInput = false;
                    typeName = "Potrosac";
                    break;
            }

            string ipAddress = "127.0.0.1";
            int port = 50001 + deviceId;

            config = new DeviceConfiguration(deviceId, type, minValue, maxValue,
                quantity, isInput, ipAddress, port);

            currentValue = GetInitialAlarmProneValue();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Uredjaj automatski konfigurisan!");
            Console.ResetColor();
            Console.WriteLine($"  |- ID: {config.DeviceID}");
            Console.WriteLine($"  |- Tip: {typeName}");
            Console.WriteLine($"  |- Opseg: [{config.MinValue} - {config.MaxValue}] {config.Quantity}");
            Console.WriteLine($"  |- Smer: {(config.IsInput ? "Ulaz (Input)" : "Izlaz (Output)")}");
            Console.WriteLine($"  '- Pocetna vrednost: {currentValue:F2} {quantity}");
            Console.WriteLine();
        }

        static void ConfigureDevice()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("              KONFIGURACIJA UREDJAJA");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();

            Console.Write("Unesite ID uredjaja: ");
            int deviceId;
            while (!int.TryParse(Console.ReadLine(), out deviceId) || deviceId < 1)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Nevazeci ID! Unesite pozitivan broj: ");
                Console.ResetColor();
            }

            Console.WriteLine("\nIzbor tipa uredjaja:");
            Console.WriteLine("  1 - Solarni Panel");
            Console.WriteLine("  2 - Vetrogenerator");
            Console.WriteLine("  3 - Baterija");
            Console.WriteLine("  4 - Potrosac");
            Console.Write("\nVas izbor (1-4): ");

            int typeChoice;
            while (!int.TryParse(Console.ReadLine(), out typeChoice) || typeChoice < 1 || typeChoice > 4)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Nevazeci izbor! Unesite broj 1-4: ");
                Console.ResetColor();
            }

            DeviceType type = DeviceType.SolarniPanel;
            double minValue = 0, maxValue = 100;
            PhysicalQuantity quantity = PhysicalQuantity.W;
            bool isInput = true;

            switch (typeChoice)
            {
                case 1:
                    type = DeviceType.SolarniPanel;
                    minValue = 0;
                    maxValue = 5000;
                    quantity = PhysicalQuantity.W;
                    isInput = true;
                    break;

                case 2:
                    type = DeviceType.Vetrogenerator;
                    minValue = 0;
                    maxValue = 3000;
                    quantity = PhysicalQuantity.W;
                    isInput = true;
                    break;

                case 3:
                    type = DeviceType.Baterija;
                    minValue = 0;
                    maxValue = 100;
                    quantity = PhysicalQuantity.P;
                    isInput = false;
                    break;

                case 4:
                    type = DeviceType.Potrosac;
                    minValue = 0;
                    maxValue = 2000;
                    quantity = PhysicalQuantity.W;
                    isInput = false;
                    break;
            }

            string ipAddress = "127.0.0.1";
            int port = 50001 + deviceId;

            config = new DeviceConfiguration(deviceId, type, minValue, maxValue,
                quantity, isInput, ipAddress, port);

            currentValue = GetInitialAlarmProneValue();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Uredjaj uspesno konfigurisan!");
            Console.ResetColor();
            Console.WriteLine($"  |- ID: {config.DeviceID}");
            Console.WriteLine($"  |- Tip: {config.Type}");
            Console.WriteLine($"  |- Opseg: [{config.MinValue} - {config.MaxValue}] {config.Quantity}");
            Console.WriteLine($"  |- Smer: {(config.IsInput ? "Ulaz" : "Izlaz")}");
            Console.WriteLine($"  '- Pocetna vrednost: {currentValue:F2} {quantity}");
            Console.WriteLine();
        }

        static double GetInitialAlarmProneValue()
        {
            if (config == null) return 0;

            switch (config.Type)
            {
                case DeviceType.SolarniPanel:
                    return random.Next(50, 200);

                case DeviceType.Vetrogenerator:
                    return random.Next(50, 200);

                case DeviceType.Baterija:
                    return 85.0;

                case DeviceType.Potrosac:
                    return config.MaxValue * 0.75;

                default:
                    return (config.MinValue + config.MaxValue) / 2;
            }
        }

        static void InitializeViaUDP()
        {
            if (config == null) return;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              UDP INICIJALIZACIJA                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(SERVER_IP), UDP_SERVER_PORT);

            try
            {
                byte[] configData = SerializationHelper.Serialize(config);

                Console.WriteLine($"[UDP] Slanje inicijalizacije serveru {serverEP}...");
                udpSocket.SendTo(configData, serverEP);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[UDP] Poslato {configData.Length} bajtova");
                Console.ResetColor();

                udpSocket.ReceiveTimeout = 5000;
                byte[] receiveBuffer = new byte[1024];
                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                Console.WriteLine("[UDP] Cekam potvrdu od servera...");
                int bytesReceived = udpSocket.ReceiveFrom(receiveBuffer, ref remoteEP);
                string confirmation = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);

                if (confirmation.StartsWith("CONFIG_OK"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[UDP] Potvrda primljena: {confirmation}");
                    Console.WriteLine("[UDP] Inicijalizacija uspesna!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[UDP] Nepoznata potvrda: {confirmation}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[UDP ERROR] {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        static void ConnectViaTCP()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              TCP KONEKCIJA                                    ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse(SERVER_IP), TCP_SERVER_PORT);

            try
            {
                Console.WriteLine($"[TCP] Uspostavljanje konekcije sa serverom {serverEP}...");
                tcpSocket.Connect(serverEP);
                tcpSocket.Blocking = false;
                isConnected = true;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[TCP] Konekcija uspostavljena");
                Console.ResetColor();
                Console.WriteLine($"[TCP]   |- Server: {serverEP}");
                Console.WriteLine($"[TCP]   '- Lokalna adresa: {tcpSocket.LocalEndPoint}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[TCP ERROR] {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }

        static void RunDevicePolling()
        {
            if (tcpSocket == null) return;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          UREDJAJ U RADU - POLLING MODEL                       ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Komande: ESC - Zaustavi uredjaj\n");

            byte[] receiveBuffer = new byte[8192];
            int pollAttempts = 0;
            int requestCount = 0;

            while (isConnected && pollAttempts < MAX_POLL_ATTEMPTS)
            {
                try
                {
                    if (tcpSocket.Poll(POLL_TIMEOUT_MS * 1000, SelectMode.SelectRead))
                    {
                        int bytesReceived = tcpSocket.Receive(receiveBuffer);

                        if (bytesReceived == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n[TCP] Server je zatvorio konekciju");
                            Console.ResetColor();
                            isConnected = false;
                            break;
                        }

                        pollAttempts = 0;
                        requestCount++;

                        var request = SerializationHelper.Deserialize<DeviceRequest>(receiveBuffer, 0, bytesReceived);

                        if (request != null)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"\n[REQUEST #{requestCount}] Primljen zahtev od servera");
                            Console.ResetColor();
                            ProcessRequest(request);
                        }
                    }
                    else
                    {
                        pollAttempts++;
                        if (pollAttempts % 5 == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine($"[POLL] Cekam zahtev... (pokusaj {pollAttempts}/{MAX_POLL_ATTEMPTS})");
                            Console.ResetColor();
                        }

                        SimulateValueChange();
                    }

                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Escape)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("\n[DEVICE] Korisnik zatvara uredjaj...");
                            Console.ResetColor();
                            isConnected = false;
                            break;
                        }
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR] {ex.Message}");
                    Console.ResetColor();
                    isConnected = false;
                    break;
                }
            }

            if (pollAttempts >= MAX_POLL_ATTEMPTS)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[TIMEOUT] Proslo {MAX_POLL_ATTEMPTS} pokusaja bez komunikacije. Zatvaram uredjaj.");
                Console.ResetColor();
            }
        }

        static void ProcessRequest(DeviceRequest request)
        {
            if (config == null) return;

            DeviceResponse response;

            try
            {
                if (request.Type == RequestType.Read)
                {
                    Console.WriteLine($"[READ] Tip: {request.Type}");
                    Console.WriteLine($"[READ] Trenutna vrednost: {currentValue:F2} {config.Quantity}");

                    bool isAlarm = CheckAlarmCondition();

                    if (isAlarm)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[ALARM] ALARMNO STANJE DETEKTOVANO!");
                        Console.WriteLine($"[ALARM] Poruka: {GetAlarmMessage()}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[STATUS] Normalno stanje");
                        Console.ResetColor();
                    }

                    response = new DeviceResponse(
                        config.DeviceID,
                        isAlarm ? ResponseStatus.AlarmActive : ResponseStatus.Success,
                        currentValue,
                        isAlarm ? GetAlarmMessage() : "Read OK",
                        isAlarm
                    );
                }
                else // Write
                {
                    if (config.IsInput)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[WRITE ERROR] Uredjaj je ulazni - ne podrzava pisanje!");
                        Console.ResetColor();

                        response = new DeviceResponse(
                            config.DeviceID,
                            ResponseStatus.Error,
                            currentValue,
                            "Uredjaj ne podrzava Write operaciju"
                        );
                    }
                    else
                    {
                        if (request.WriteValue.HasValue)
                        {
                            double newValue = request.WriteValue.Value;

                            if (newValue >= config.MinValue && newValue <= config.MaxValue)
                            {
                                currentValue = newValue;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[WRITE] Nova vrednost postavljena: {currentValue:F2} {config.Quantity}");
                                Console.ResetColor();

                                response = new DeviceResponse(
                                    config.DeviceID,
                                    ResponseStatus.Success,
                                    currentValue,
                                    "Write OK"
                                );
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[WRITE ERROR] Vrednost {newValue} van opsega [{config.MinValue}-{config.MaxValue}]!");
                                Console.ResetColor();

                                response = new DeviceResponse(
                                    config.DeviceID,
                                    ResponseStatus.Error,
                                    currentValue,
                                    $"Vrednost mora biti u opsegu [{config.MinValue}-{config.MaxValue}]"
                                );
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[WRITE ERROR] Nedostaje vrednost za upis!");
                            Console.ResetColor();

                            response = new DeviceResponse(
                                config.DeviceID,
                                ResponseStatus.Error,
                                currentValue,
                                "Write vrednost nije prosledjena"
                            );
                        }
                    }
                }

                SendResponse(response);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Greska pri obradi zahteva: {ex.Message}");
                Console.ResetColor();

                response = new DeviceResponse(
                    config.DeviceID,
                    ResponseStatus.Error,
                    currentValue,
                    ex.Message
                );

                SendResponse(response);
            }
        }

        static void SendResponse(DeviceResponse response)
        {
            if (tcpSocket == null) return;

            try
            {
                byte[] responseData = SerializationHelper.Serialize(response);
                tcpSocket.Send(responseData);

                ConsoleColor statusColor = response.Status == ResponseStatus.Success ? ConsoleColor.Green :
                                          response.Status == ResponseStatus.AlarmActive ? ConsoleColor.Red :
                                          ConsoleColor.Yellow;

                Console.ForegroundColor = statusColor;
                Console.WriteLine($"[RESPONSE] Odgovor poslat serveru (Status: {response.Status})");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Greska pri slanju odgovora: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void SimulateValueChange()
        {
            if (config == null) return;

            double change = 0;

            switch (config.Type)
            {
                case DeviceType.SolarniPanel:
                    change = random.Next(-400, 500);
                    break;

                case DeviceType.Vetrogenerator:
                    change = random.Next(-300, 400);
                    break;

                case DeviceType.Baterija:
                    change = random.Next(-3, 4);
                    break;

                case DeviceType.Potrosac:
                    change = random.Next(-100, 150);
                    break;
            }

            currentValue += change;

            if (currentValue < config.MinValue) currentValue = config.MinValue;
            if (currentValue > config.MaxValue) currentValue = config.MaxValue;
        }

        static bool CheckAlarmCondition()
        {
            if (config == null) return false;

            switch (config.Type)
            {
                case DeviceType.SolarniPanel:
                    return currentValue < 300;

                case DeviceType.Vetrogenerator:
                    return currentValue < 300;

                case DeviceType.Baterija:
                    return currentValue >= config.MaxValue * 0.90 || currentValue <= config.MinValue + 10;

                case DeviceType.Potrosac:
                    return currentValue >= config.MaxValue * 0.80;

                default:
                    return false;
            }
        }

        static string GetAlarmMessage()
        {
            if (config == null) return "Alarm aktivan!";

            switch (config.Type)
            {
                case DeviceType.SolarniPanel:
                    return $"Niska proizvodnja solarne energije ({currentValue:F0}W)! Proveriti panele!";

                case DeviceType.Vetrogenerator:
                    return $"Niska proizvodnja vetro energije ({currentValue:F0}W)! Nedovoljno vetra!";

                case DeviceType.Baterija:
                    if (currentValue >= config.MaxValue * 0.90)
                        return $"Baterija skoro puna ({currentValue:F1}%)! Zaustaviti punjenje!";
                    else
                        return $"Baterija skoro prazna ({currentValue:F1}%)! Hitno punjenje!";

                case DeviceType.Potrosac:
                    return $"Preopterecenje ({currentValue:F0}W od {config.MaxValue}W)! Redukovati potrosnju!";

                default:
                    return "Alarm aktivan!";
            }
        }

        static void Cleanup()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  ZATVARANJE UREDJAJA                          ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            try
            {
                if (tcpSocket != null && tcpSocket.Connected)
                {
                    tcpSocket.Shutdown(SocketShutdown.Both);
                    tcpSocket.Close();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[TCP] TCP konekcija zatvorena");
                    Console.ResetColor();
                }
            }
            catch { }

            try
            {
                udpSocket?.Close();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[UDP] UDP uticnica zatvorena");
                Console.ResetColor();
            }
            catch { }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nUredjaj uspesno zaustavljen");
            Console.ResetColor();
        }
    }
}