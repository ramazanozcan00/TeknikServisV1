using Microsoft.AspNetCore.Mvc;
using TeknikServis.Service.Services;
using System.Threading.Tasks;
using TeknikServis.Core.Interfaces;
using System;

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

            var ticket = await _serviceTicketService.GetTicketByFisNoAsync(q);

            if (ticket == null) return NotFound();

            string markaAdi = ticket.DeviceBrand != null ? ticket.DeviceBrand.Name : "";

            return Ok(new
            {
                FisNo = ticket.FisNo,
                Cihaz = $"{markaAdi} {ticket.DeviceModel}",
                SeriNo = ticket.SerialNumber ?? "-", // Seri No eklendi
                Durum = ticket.Status,
                Ariza = ticket.ProblemDescription,
                GirisTarihi = ticket.CreatedDate.ToString("dd.MM.yyyy"),
                Ucret = ticket.TotalPrice.HasValue ? ticket.TotalPrice.Value.ToString("N2") : "0"
            });
        }

        [HttpPost("UpdatePaymentStatus")]
        public async Task<IActionResult> UpdatePaymentStatus([FromBody] PaymentUpdateDto model)
        {
            if (string.IsNullOrEmpty(model.FisNo)) return BadRequest();

            var ticket = await _serviceTicketService.GetTicketByFisNoAsync(model.FisNo);

            if (ticket != null)
            {
                // --- KRİTİK DÜZELTME BURADA ---

                // ESKİ (HATALI): 
                // ticket.Status = "Ödeme Yapıldı";
                // await _serviceTicketService.UpdateTicketAsync(ticket);

                // YENİ (DOĞRU):
                // Durumu veritabanına işleyen özel metodu çağırıyoruz.
                await _serviceTicketService.UpdateTicketStatusAsync(ticket.Id, "Ödeme Yapıldı");

                return Ok(new { message = "Durum başarıyla güncellendi." });
            }

            return NotFound(new { message = "Kayıt bulunamadı." });
        }
    }

    public class PaymentUpdateDto
    {
        public string FisNo { get; set; }
    }
}