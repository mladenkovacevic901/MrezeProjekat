
using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using static ProjekatMreze.Enumeratori;

namespace Klijent
{
    public class Program
    {

        static async Task Main(string[] args)
        {
            int id = 1;
            Random R = new Random();
            int RandomIO = R.Next(0, 2);
            Console.WriteLine("Pokretanje SCADA klijenta...");
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Uredjaj uredjaj = new Uredjaj
            {
                ID_uredjaja = 1,
                tip_uredjaja = ProjekatMreze.Enumeratori.TIP_UREDJAJA.POTROSAC,
                fizicka_velicina = ProjekatMreze.Enumeratori.FIZICKA_VELICINA.V,
                min_vrednost = 0,
                max_vrednost = 100,
                ulaz_izlaz = (RandomIO == 0) ? ProjekatMreze.Enumeratori.IO.ULAZ : ProjekatMreze.Enumeratori.IO.IZLAZ,
                ip_adresa = "127.0.0.1", // loopback adresa
                port = 1024 // isti port kao i kod servera da bi se povezali
            };

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, uredjaj);  // ovde se ovo sve pakuje u bajt niz
                data = ms.ToArray();
            }


            EndPoint ep = new IPEndPoint(IPAddress.Loopback, 1024);

            clientSocket.SendTo(data, ep);
            byte[] recvBuf = new byte[4096];
            clientSocket.ReceiveFrom(recvBuf, ref ep);
            Console.WriteLine("Konfiguracija primljena");



            clientSocket.SendTo(Encoding.UTF8.GetBytes("ACK"), ep);

            // Cekanje komande od servera
            while (true)
            {
                if (clientSocket.Poll(5000000, SelectMode.SelectRead))
                {
                    byte[] getRW = new byte[1024];
                    clientSocket.ReceiveFrom(getRW, ref ep);
                    string rw = Encoding.UTF8.GetString(getRW).Trim('\0');
                    Console.WriteLine($"Primljena komanda: {rw}\n");


                    if (rw == "READ")
                    {
                        string errMessage = "";

                        if (uredjaj.min_vrednost <= 20 && uredjaj.max_vrednost>=80)
                        {
                            errMessage = "Upozorenje: Niska minimalna vrednost i visoka maksimalna vrednost uredjaja!\n";
                        }
                        else if (uredjaj.max_vrednost >= 80)
                        {
                            errMessage = "Upozorenje: Visoka maksimalna vrednost uredjaja!\n";
                        }
                        else if(uredjaj.min_vrednost <= 20)
                        {
                            errMessage = "Upozorenje: Niska minimalna vrednost uredjaja!\n"; 
                        }
                        else
                        {
                            errMessage = "\n";
                        }
                        string message = $"ID uredjaja: {uredjaj.ID_uredjaja}\ntip uredjaja: {uredjaj.tip_uredjaja}\nfizicka velicina: {uredjaj.fizicka_velicina}\nminimalna vrednost: {uredjaj.min_vrednost}\nmaksimalna vrednost: {uredjaj.max_vrednost}\nulazni ili izlazni uredjaj: {uredjaj.ulaz_izlaz}\nprekoracenje: {errMessage}\n";
                        byte[] msg = Encoding.UTF8.GetBytes(message);
                        clientSocket.SendTo(msg, ep);
                        Console.WriteLine("Poslati podaci o uredjaju serveru.\n");
                    }
                    else if (rw == "WRITE")
                    {
                        
                        id++;
                        uredjaj.ID_uredjaja = id;
                        TIP_UREDJAJA stariTip = uredjaj.tip_uredjaja;
                        FIZICKA_VELICINA staraVelicina = uredjaj.fizicka_velicina;
                        IO stariIO = uredjaj.ulaz_izlaz;
                        double min_vrednost_stara = uredjaj.min_vrednost;
                        double max_vrednost_stara = uredjaj.max_vrednost;

                        do { uredjaj.tip_uredjaja = (TIP_UREDJAJA)R.Next(0, 4); } while (uredjaj.tip_uredjaja == stariTip);
                        do { uredjaj.fizicka_velicina = (FIZICKA_VELICINA)R.Next(0, 4); } while (uredjaj.fizicka_velicina == staraVelicina);
                        do { uredjaj.ulaz_izlaz = (IO)R.Next(0, 2); } while (uredjaj.ulaz_izlaz == stariIO);
                        do { uredjaj.min_vrednost = R.Next(0, 51); } while (uredjaj.min_vrednost == min_vrednost_stara);
                        do { uredjaj.max_vrednost = R.Next(51, 101); } while (uredjaj.max_vrednost == max_vrednost_stara);

                        string errMessage = "";


                        if (uredjaj.min_vrednost <= 20 && uredjaj.max_vrednost >= 80)
                        {
                            errMessage = "Upozorenje: Niska minimalna vrednost i visoka maksimalna vrednost uredjaja!\n";
                        }
                        else if (uredjaj.max_vrednost >= 80)
                        {
                            errMessage = "Upozorenje: Visoka maksimalna vrednost uredjaja!\n";
                        }
                        else if (uredjaj.min_vrednost <= 20)
                        {
                            errMessage = "Upozorenje: Niska minimalna vrednost uredjaja!\n";
                        }
                        else
                        {
                            errMessage = "\n";
                        }
                        Console.WriteLine("Poslati podaci o uredjaju serveru.\n");

                        Console.WriteLine("Podaci su uspesno izmenjeni i poslati.\n");
                        string message = $"ID uredjaja: {uredjaj.ID_uredjaja}\ntip uredjaja: {uredjaj.tip_uredjaja}\nfizicka velicina: {uredjaj.fizicka_velicina}\nminimalna vrednost: {uredjaj.min_vrednost}\nmaksimalna vrednost: {uredjaj.max_vrednost}\nulazni ili izlazni uredjaj: {uredjaj.ulaz_izlaz}\nprekoracenje: {errMessage}\n";
                        byte[] msg = Encoding.UTF8.GetBytes(message);
                        clientSocket.SendTo(msg, ep);

                    }
                    else
                    {
                        string message = "Doslo je do greske sa datom operacijom!\n";
                        byte[] msg = Encoding.UTF8.GetBytes(message);
                        clientSocket.SendTo(msg, ep);
                        Console.WriteLine("Poslati podaci o uredjaju serveru.\n");
                        
                    }
                    
                }

                await Task.Delay(1000);
            }
            
        }
    }
}

