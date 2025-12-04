using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions; // GetBranchId için

namespace TeknikServis.Web.ViewComponents
{
    public class BranchSwitcherViewComponent : ViewComponent
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;

        public BranchSwitcherViewComponent(UserManager<AppUser> userManager, IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null) return Content("");

            // 1. Kullanıcının Ana Şubesi
            var mainBranch = await _unitOfWork.Repository<Branch>().GetByIdAsync(user.BranchId);

            // 2. Ek Şubeler
            var extraBranches = await _unitOfWork.Repository<UserBranch>()
                .FindAsync(x => x.UserId == user.Id, inc => inc.Branch);

            // 3. Listeyi Birleştir
            var allBranches = new List<Branch>();
            if (mainBranch != null) allBranches.Add(mainBranch);

            foreach (var item in extraBranches)
            {
                if (item.Branch != null) allBranches.Add(item.Branch);
            }

            // 4. Şu anki aktif şube (Claim'den gelen)
            ViewBag.CurrentBranchId = HttpContext.User.GetBranchId();

            return View(allBranches);
        }
    }
}