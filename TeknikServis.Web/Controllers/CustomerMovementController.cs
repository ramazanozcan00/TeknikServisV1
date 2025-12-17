using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TeknikServis.Web.Controllers
{
    public class CustomerMovementController : Controller
    {
        // 1. ADIM: Nesne örneğini tutacak olan değişkeni tanımlayın
        private readonly IUnitOfWork _unitOfWork;

        // 2. ADIM: Constructor (Yapıcı Metod) ile sistemi bu değişkeni doldurmaya zorlayın
        // Program çalıştığında .NET buraya canlı bir UnitOfWork nesnesi teslim eder.
        public CustomerMovementController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [Authorize(Roles = "Admin,Personnel")]
        public async Task<IActionResult> Index()
        {
            var branchId = User.GetBranchId();

            // 3. ADIM: HATA BURADAYDI! 
            // "IUnitOfWork" (Büyük harf/Arayüz adı) yerine 
            // yukarıda tanımladığımız "_unitOfWork" (Küçük harf/Değişken adı) kullanmalısınız.
            var movements = await _unitOfWork.Repository<CustomerMovement>()
                .FindAsync(x => x.BranchId == branchId, inc => inc.Customer);

            return View(movements);
        }
    }
}