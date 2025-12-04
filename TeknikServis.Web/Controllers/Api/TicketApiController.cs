using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions; // AddBusinessDays için
using System.Threading.Tasks;
using System.Linq;
using System;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public TicketApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet("CheckStatus")]
        public async Task<IActionResult> CheckStatus(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("Kod girilmedi.");

            q = q.Trim();

            // Fiş No veya Seri No ile ara
            var ticket = (await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.FisNo == q || x.SerialNumber == q,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType)).FirstOrDefault();

            if (ticket == null) return NotFound("Kayıt bulunamadı.");

            // Yasal Süre Hesaplama
            var yasalBitis = ticket.CreatedDate.AddBusinessDays(20);

            // Dışarıya dönecek veriyi hazırla (Güvenlik için tüm veriyi açmıyoruz)
            var response = new
            {
                FisNo = ticket.FisNo,
                Cihaz = $"{(ticket.DeviceBrand?.Name ?? "-")} {ticket.DeviceModel}",
                SeriNo = ticket.SerialNumber,
                Durum = ticket.Status,
                Ariza = ticket.ProblemDescription,
                GirisTarihi = ticket.CreatedDate.ToString("dd.MM.yyyy"),
                TahminiTeslim = yasalBitis.ToString("dd.MM.yyyy"),
                Ucret = ticket.TotalPrice.HasValue ? ticket.TotalPrice.Value.ToString("C2") : "-"
            };

            return Ok(response);
        }
    }
}