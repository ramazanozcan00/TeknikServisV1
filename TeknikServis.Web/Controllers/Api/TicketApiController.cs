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
                SeriNo = ticket.SerialNumber ?? "-",
                Durum = ticket.Status,
                Ariza = ticket.ProblemDescription,
                GirisTarihi = ticket.CreatedDate.ToString("dd.MM.yyyy"),
                Ucret = ticket.TotalPrice.HasValue ? ticket.TotalPrice.Value.ToString("N2") : "0"
            });
        }

        [HttpPost("UpdatePaymentStatus")]
        public async Task<IActionResult> UpdatePaymentStatus([FromForm] string fisNo)
        {
            if (string.IsNullOrEmpty(fisNo))
                return BadRequest(new { message = "Fiş numarası (fisNo) boş olamaz." });

            // 1. Fiş numarasına göre kaydı bul
            var ticket = await _serviceTicketService.GetTicketByFisNoAsync(fisNo);

            if (ticket != null)
            {
                // 2. Durumu güncelle (Ödeme Yapıldı)
                await _serviceTicketService.UpdateTicketStatusAsync(ticket.Id, "Ödeme Yapıldı");

                return Ok(new { message = "Durum başarıyla güncellendi." });
            }

            return NotFound(new { message = "Kayıt bulunamadı." });
        }
    }
}