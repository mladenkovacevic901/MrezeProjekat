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

            ConfigureDevice();

            try
            {
                InitializeViaUDP();
                ConnectViaTCP();
                RunDevicePolling();
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

        static void ConfigureDevice()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("              KONFIGURACIJA UREĐAJA");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine();

            Console.Write("Unesite ID uređaja: ");
            int deviceId;
            while (!int.TryParse(Console.ReadLine(), out deviceId) || deviceId < 1)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Nevažeći ID! Unesite pozitivan broj: ");
                Console.ResetColor();
            }

            Console.WriteLine("\nIzbor tipa uređaja:");
            Console.WriteLine("  1 - Solarni Panel");
            Console.WriteLine("  2 - Vetrogenerator");
            Console.WriteLine("  3 - Baterija");
            Console.WriteLine("  4 - Potrošač");
            Console.Write("\nVaš izbor (1-4): ");
            
            int typeChoice;
            while (!int.TryParse(Console.ReadLine(), out typeChoice) || typeChoice < 1 || typeChoice > 4)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Nevažeći izbor! Unesite broj 1-4: ");
                Console.ResetColor();
            }

            DeviceType type = DeviceType.SolarniPanel;
            double minValue = 0, maxValue = 100;
            PhysicalQuantity quantity = PhysicalQuantity.W;
            bool isInput = true;

            switch (typeChoice)
            {
                case 1: // Solarni Panel
                    type = DeviceType.SolarniPanel;
                    minValue = 0;
                    maxValue = 5000; // 5kW max
                    quantity = PhysicalQuantity.W;
                    isInput = true;
                    break;

                case 2: // Vetrogenerator
                    type = DeviceType.Vetrogenerator;
                    minValue = 0;
                    maxValue = 3000; // 3kW max
                    quantity = PhysicalQuantity.W;
                    isInput = true;
                    break;

                case 3: // Baterija
                    type = DeviceType.Baterija;
                    minValue = 0;
                    maxValue = 100; // 0-100%
                    quantity = PhysicalQuantity.P;
                    isInput = false;
                    break;

                case 4: // Potrošač
                    type = DeviceType.Potrosac;
                    minValue = 0;
                    maxValue = 2000; // 2kW max
                    quantity = PhysicalQuantity.W;
                    isInput = false;
                    break;

                default:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Nevažeći izbor! Koristi se Solarni Panel kao default.");
                    Console.ResetColor();
                    type = DeviceType.SolarniPanel;
                    minValue = 0;
                    maxValue = 5000;
                    quantity = PhysicalQuantity.W;
                    isInput = true;
                    break;
            }

            string ipAddress = "127.0.0.1";
            int port = 50001 + deviceId;

            config = new DeviceConfiguration(deviceId, type, minValue, maxValue, 
                                             quantity, isInput, ipAddress, port);

            // Inicijalna vrednost - postavljena bliže alarmnom stanju
            currentValue = GetInitialAlarmProneValue();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Uređaj uspešno konfigurisan!");
            Console.ResetColor();
            Console.WriteLine($"  ├─ ID: {config.DeviceID}");
            Console.WriteLine($"  ├─ Tip: {config.Type}");
            Console.WriteLine($"  ├─ Opseg: [{config.MinValue} - {config.MaxValue}] {config.Quantity}");
            Console.WriteLine($"  ├─ Smer: {(config.IsInput ? "Ulaz" : "Izlaz")}");
            Console.WriteLine($"  └─ Početna vrednost: {currentValue:F2} {quantity}");
            Console.WriteLine();
        }

        static double GetInitialAlarmProneValue()
        {
            if (config == null) return 0;

            // Postavi početne vrednosti bliže alarmnim granicama
            switch (config.Type)
            {
                case DeviceType.SolarniPanel:
                    // Počinje nisko - između 50-200W (alarm je <300W)
                    return random.Next(50, 200
                        );

                case DeviceType.Vetrogenerator:
                    // Počinje nisko - između 50-200W
                    return random.Next(50, 200);

                case DeviceType.Baterija:
                    // Počinje sa 85% - brzo će dostići 90%
                    return 85.0;

                case DeviceType.Potrosac:
                    // Počinje na 75% kapaciteta - brzo će dostići 80%
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
                Console.WriteLine($"[UDP] ✓ Poslato {configData.Length} bajtova");
                Console.ResetColor();

                udpSocket.ReceiveTimeout = 5000;
                byte[] receiveBuffer = new byte[1024];
                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                
                Console.WriteLine("[UDP] Čekam potvrdu od servera...");
                int bytesReceived = udpSocket.ReceiveFrom(receiveBuffer, ref remoteEP);
                string confirmation = Encoding.UTF8.GetString(receiveBuffer, 0, bytesReceived);

                if (confirmation.StartsWith("CONFIG_OK"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[UDP] ✓ Potvrda primljena: {confirmation}");
                    Console.WriteLine("[UDP] ✓ Inicijalizacija uspešna!");
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
                Console.WriteLine($"[TCP] ✓ Konekcija uspostavljena");
                Console.ResetColor();
                Console.WriteLine($"[TCP]   ├─ Server: {serverEP}");
                Console.WriteLine($"[TCP]   └─ Lokalna adresa: {tcpSocket.LocalEndPoint}");
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
            Console.WriteLine("║          UREĐAJ U RADU - POLLING MODEL                        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Komande: ESC - Zaustavi uređaj\n");

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
                        if (pollAttempts % 5 == 0) // Prikazuj svakih 5 pokušaja
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine($"[POLL] Čekam zahtev... (pokušaj {pollAttempts}/{MAX_POLL_ATTEMPTS})");
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
                            Console.WriteLine("\n[DEVICE] Korisnik zatvara uređaj...");
                            Console.ResetColor();
                            isConnected = false;
                            break;
                        }
                    }
                }
                catch (SocketException ex)  
                {
                    if((ex.SocketErrorCode == SocketError.WouldBlock))
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
                Console.WriteLine($"\n[TIMEOUT] Prošlo {MAX_POLL_ATTEMPTS} pokušaja bez komunikacije. Zatvaram uređaj.");
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
                        Console.WriteLine($"[ALARM] !  ALARMNO STANJE DETEKTOVANO!");
                        Console.WriteLine($"[ALARM] Poruka: {GetAlarmMessage()}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[STATUS] ✓ Normalno stanje");
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
                        Console.WriteLine("[WRITE ERROR] Uređaj je ulazni - ne podržava pisanje!");
                        Console.ResetColor();
                        
                        response = new DeviceResponse(
                            config.DeviceID,
                            ResponseStatus.Error,
                            currentValue,
                            "Uređaj ne podržava Write operaciju"
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
                                Console.WriteLine($"[WRITE] ✓ Nova vrednost postavljena: {currentValue:F2} {config.Quantity}");
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
                                "Write vrednost nije prosleđena"
                            );
                        }
                    }
                }

                SendResponse(response);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Greška pri obradi zahteva: {ex.Message}");
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
                Console.WriteLine($"[ERROR] Greška pri slanju odgovora: {ex.Message}");
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
                    // POVEĆANE VARIJACIJE - brže dostizanje alarma
                    change = random.Next(-400, 500);
                    break;

                case DeviceType.Vetrogenerator:
                    // POVEĆANE VARIJACIJE
                    change = random.Next(-300, 400);
                    break;

                case DeviceType.Baterija:
                    // BRŽE PUNJENJE/PRAŽNJENJE
                    change = random.Next(-3, 4);
                    break;

                case DeviceType.Potrosac:
                    // VEĆE OSCILACIJE
                    change = random.Next(-100, 150);
                    break;
            }

            currentValue += change;

            // Ograniči vrednost
            if (currentValue < config.MinValue) currentValue = config.MinValue;
            if (currentValue > config.MaxValue) currentValue = config.MaxValue;
        }

        static bool CheckAlarmCondition()
        {
            if (config == null) return false;

            // POJAČANI ALARMI - češće aktiviranje
            switch (config.Type)
            {
                case DeviceType.SolarniPanel:
                    // ALARM ako je ispod 300W
                    return currentValue < 300;

                case DeviceType.Vetrogenerator:
                    // ALARM ako je ispod 300W
                    return currentValue < 300;

                case DeviceType.Baterija:
                    // ALARM na 90%+ ili ispod 10%
                    return currentValue >= config.MaxValue * 0.90 || currentValue <= config.MinValue + 10;

                case DeviceType.Potrosac:
                    // ALARM na 80%+
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
                    return $"Preopterećenje ({currentValue:F0}W od {config.MaxValue}W)! Redukovati potrošnju!";

                default:
                    return "Alarm aktivan!";
            }
        }

        static void Cleanup()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  ZATVARANJE UREĐAJA                           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            try
            {
                if (tcpSocket != null && tcpSocket.Connected)
                {
                    tcpSocket.Shutdown(SocketShutdown.Both);
                    tcpSocket.Close();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[TCP] ✓ TCP konekcija zatvorena");
                    Console.ResetColor();
                }
            }
            catch { }

            try
            {
                udpSocket?.Close();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[UDP] ✓ UDP utičnica zatvorena");
                Console.ResetColor();
            }
            catch { }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✓ Uređaj uspešno zaustavljen");
            Console.ResetColor();
        }
    }
}