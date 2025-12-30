using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknikServis.Core.DTOs
{
    // Backend tarafı (C#) - TicketApiController içindeki DTO
    public class ServiceTicketDto
    {
        public Guid CustomerId { get; set; }
        public string DeviceModel { get; set; }
        public string SerialNo { get; set; }
        public string Problem { get; set; }

        // Bunları ekleyin:
        public string Accessories { get; set; }
        public string PhysicalDamage { get; set; }
        public bool IsWarranty { get; set; }
    }
}
