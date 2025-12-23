using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknikServis.Core.DTOs
{
    public class CurrencyDto
    {
        public string Code { get; set; }        // Örn: USD, EUR
        public string Name { get; set; }        // Örn: ABD DOLARI
        public decimal BanknoteBuying { get; set; } // Efektif Alış
        public decimal BanknoteSelling { get; set; } // Efektif Satış
        public decimal ForexBuying { get; set; }    // Döviz Alış
        public decimal ForexSelling { get; set; }   // Döviz Satış
    }
}
