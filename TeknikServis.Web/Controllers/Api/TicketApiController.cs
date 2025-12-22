using Microsoft.AspNetCore.Mvc;
using TeknikServis.Service.Services;
using System.Threading.Tasks;
using TeknikServis.Core.Interfaces;
using System; // Guid ve DateTime için gerekli

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketApiController : ControllerBase
    {
        private readonly IServiceTicketService _serviceTicketService;

        public TicketApiController(IServiceTicketService serviceTicketService)
        {
            _serviceTicketService = serviceTicketService;
        }

        [HttpGet("CheckStatus")]
        public async Task<IActionResult> CheckStatus(string q)
        {
            if (string.IsNullOrEmpty(q)) return BadRequest();

            // Düzeltme: Interface'deki doğru metot ismi 'GetTicketByFisNoAsync'
            var ticket = await _serviceTicketService.GetTicketByFisNoAsync(q);

            if (ticket == null) return NotFound();

            // Marka adı null gelirse patlamaması için kontrol
            string markaAdi = ticket.DeviceBrand != null ? ticket.DeviceBrand.Name : "";

            return Ok(new
            {
                FisNo = ticket.FisNo,
                // Düzeltme: Cihazın markası ve modeli birleştirildi
                Cihaz = $"{markaAdi} {ticket.DeviceModel}",
                // Düzeltme: Seri numarası eklendi
                SeriNo = ticket.SerialNumber,
                Durum = ticket.Status,
                Ariza = ticket.ProblemDescription,
                GirisTarihi = ticket.CreatedDate.ToString("dd.MM.yyyy"),
                // Düzeltme: Ücret null ise "0" gönder, yoksa formatlı gönder
                Ucret = ticket.TotalPrice.HasValue ? ticket.TotalPrice.Value.ToString("N2") : "0"
            });
        }

        [HttpPost("UpdatePaymentStatus")]
        public async Task<IActionResult> UpdatePaymentStatus([FromBody] PaymentUpdateDto model)
        {
            if (string.IsNullOrEmpty(model.FisNo)) return BadRequest();

            // 1. Fiş numarasına göre kaydı bul
            var ticket = await _serviceTicketService.GetTicketByFisNoAsync(model.FisNo);

            if (ticket != null)
            {
                // 2. Durumu güncelle
                ticket.Status = "Ödeme Yapıldı";

                // 3. Veritabanına kaydet (Interface'deki doğru metot: UpdateTicketAsync)
                await _serviceTicketService.UpdateTicketAsync(ticket);

                return Ok(new { message = "Durum güncellendi." });
            }

            return NotFound(new { message = "Kayıt bulunamadı." });
        }
    }

    public class PaymentUpdateDto
    {
        public string FisNo { get; set; }
    }
}