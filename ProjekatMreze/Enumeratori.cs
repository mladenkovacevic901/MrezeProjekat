using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjekatMreze
{
     public class Enumeratori
    {

       public enum TIP_UREDJAJA
        {
            VETROGENERATOR,
            SOLARNI_PANEL,
            BATERIJA,
            POTROSAC

        }

      public   enum FIZICKA_VELICINA
        {
            P,
            V,
            C,
            T   // pitati
        }

        public enum IO
        {
            ULAZ,
            IZLAZ
        }
    }
}
