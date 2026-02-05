using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static ProjekatMreze.Enumeratori;



namespace Common
{

    [Serializable]
    public class Uredjaj
    {

        public int ID_uredjaja { get; set; }


        public TIP_UREDJAJA tip_uredjaja { get; set; }
        public FIZICKA_VELICINA fizicka_velicina    { get; set; }

        public double min_vrednost { get; set; }
        public double max_vrednost { get; set; }

        public IO ulaz_izlaz { get; set; }  




        public string ip_adresa { get; set; }
        public int port { get; set; }




       




    }
}
