using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/CompanySetting")] 
    [ApiController]
    public class CompanySettingApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CompanySettingApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            // 1. MÜŞTERİLERDEN GELEN FİRMALAR (Önceden kaydettikleriniz)
            var customerCompanies = (await _unitOfWork.Repository<Customer>().GetAllAsync())
                                    .Select(c => c.CompanyName)
                                    .Where(x => !string.IsNullOrEmpty(x)) // Boş olanları at
                                    .ToList();

            // 2. AYARLARDAN GELEN FİRMALAR (Sabit tanımladıklarınız)
            var settingCompanies = (await _unitOfWork.Repository<CompanySetting>().GetAllAsync())
                                   .Select(c => c.CompanyName)
                                   .Where(x => !string.IsNullOrEmpty(x))
                                   .ToList();

            // 3. LİSTELERİ BİRLEŞTİR (Tekrarları kaldır)
            var allCompanies = customerCompanies
                               .Union(settingCompanies)
                               .Distinct()
                               .OrderBy(x => x)
                               .ToList();

            // 4. EĞER HİÇBİR YERDE FİRMA YOKSA TEST VERİSİ GÖNDER (Boş kalmasın)
            if (!allCompanies.Any())
            {
                allCompanies.Add("Test Firması A.Ş.");
                allCompanies.Add("Örnek Şirket Ltd.");
            }

            return Ok(allCompanies);
        }
    }
}