//geng shit?

using Common;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
namespace Server
{
    public class Program
    {
        static void Main(string[] args)
        {
            Socket sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint destPoint = new IPEndPoint(IPAddress.Any, 1024);
            sendSocket.Bind(destPoint);
            Random R = new Random();
            Console.WriteLine("Pokretanje SCADA servera...");




            byte[] buffer = new byte[4096];

            EndPoint ep = new IPEndPoint(IPAddress.Any, 0); // used for storing the sender's endpoint

            int received = sendSocket.ReceiveFrom(buffer, ref ep);

            Uredjaj uredjaj = null;

            using (MemoryStream ms = new MemoryStream(buffer, 0, received))
            {
                BinaryFormatter bf = new BinaryFormatter();
                uredjaj = (Uredjaj)bf.Deserialize(ms);
            }


            Console.WriteLine($"Dobijanje ID uredjaja: {uredjaj.ID_uredjaja}");
            Console.WriteLine($"Type: {uredjaj.tip_uredjaja}");


            byte[] response;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, uredjaj);
                response = ms.ToArray();
            }

            sendSocket.SendTo(response, ep);

            Console.WriteLine("Poslana potvrda inicijalne konfiguracije.");

            byte[] ack = new byte[256];
            sendSocket.ReceiveFrom(ack, ref ep);
            Console.WriteLine("Server je primio potvrdu od klijenta.");




            //primanje poruke
            List<string> listaStatusa = new List<string>();
            while (true)
            {
                string komanda = (R.Next(100) < 50) ? "WRITE" : "READ";
                byte[] komandaBytes = Encoding.UTF8.GetBytes(komanda);
                sendSocket.SendTo(komandaBytes, ep);

                Console.WriteLine("Konfiguracija primljena");

                List<Socket> readSockets = new List<Socket> { sendSocket };


                Socket.Select(readSockets, null, null, 5000000);

                if (readSockets.Count > 0)
                {
                    Console.Clear();
                    byte[] buffer2 = new byte[256];
                    sendSocket.ReceiveFrom(buffer2, ref ep);
                    Console.WriteLine("Primljena poruka: \n" + Encoding.UTF8.GetString(buffer2).TrimEnd('\0'));
                    
                    
                }
                System.Threading.Thread.Sleep(2000);

            }
           

        }

    }
}


     
