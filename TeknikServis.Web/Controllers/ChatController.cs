using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public ChatController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // ADMIN İÇİN: Aktif Sohbetleri Getir (Şubeli İsim Dahil)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetActiveChats()
        {
            var allMessages = await _unitOfWork.Repository<ChatMessage>().GetAllAsync();

            var groupedChats = allMessages
                .Where(m => m.ReceiverId == "Admin" || m.SenderId == _userManager.GetUserId(User) || User.IsInRole("Admin"))
                .GroupBy(m => m.ReceiverId == "Admin" ? m.SenderId : m.ReceiverId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastMessageObj = g.OrderByDescending(m => m.Timestamp).FirstOrDefault(),
                    UnreadCount = g.Count(m => !m.IsRead && m.ReceiverId == "Admin")
                })
                .Where(x => x.UserId != "Admin" && x.UserId != null)
                .OrderByDescending(x => x.LastMessageObj.Timestamp)
                .ToList();

            var resultList = new List<object>();

            foreach (var chat in groupedChats)
            {
                string displayName = chat.LastMessageObj.SenderName;

                // Eğer isimde şube bilgisi (parantez) yoksa veya "Kullanıcı" ise veritabanından taze çek
                if (string.IsNullOrEmpty(displayName) || displayName == "Kullanıcı" || !displayName.Contains("("))
                {
                    var user = await _userManager.Users
                                    .Include(u => u.Branch)
                                    .FirstOrDefaultAsync(u => u.Id.ToString() == chat.UserId);

                    if (user != null)
                    {
                        string branchInfo = user.Branch != null ? $" ({user.Branch.BranchName})" : "";
                        displayName = $"{user.FullName}{branchInfo}";
                    }
                    else
                    {
                        displayName = "Bilinmeyen Kullanıcı";
                    }
                }

                resultList.Add(new
                {
                    chat.UserId,
                    LastMessage = chat.LastMessageObj.Message,
                    UserName = displayName, // "Ahmet Yılmaz (Kadıköy Şube)" formatında
                    chat.UnreadCount,
                    LastDate = chat.LastMessageObj.Timestamp
                });
            }

            return Json(resultList);
        }

        // KULLANICI İÇİN: Okunmamış Mesaj Sayısını Kontrol Et (Layout Badge İçin)
        [HttpGet]
        public async Task<IActionResult> CheckUnread()
        {
            var userId = _userManager.GetUserId(User);

            if (!User.IsInRole("Admin"))
            {
                var allMessages = await _unitOfWork.Repository<ChatMessage>().GetAllAsync();
                var count = allMessages.Count(m => m.ReceiverId == userId && !m.IsRead);
                return Json(new { count });
            }
            return Json(new { count = 0 });
        }

        // GEÇMİŞİ GETİR VE OKUNDU İŞARETLE
        [HttpGet]
        public async Task<IActionResult> GetHistory(string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");
            string targetId = (isAdmin && !string.IsNullOrEmpty(userId)) ? userId : currentUserId;

            if (userId == "me") targetId = currentUserId;

            var allMessages = await _unitOfWork.Repository<ChatMessage>().GetAllAsync();

            var history = allMessages
                .Where(m => (m.SenderId == targetId && m.ReceiverId == "Admin") || (m.ReceiverId == targetId))
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    sender = m.SenderId == targetId ? "User" : "Admin",
                    message = m.Message,
                    time = m.Timestamp.ToString("HH:mm"),
                    senderName = m.SenderName
                })
                .ToList();

            // Okundu İşaretleme
            List<ChatMessage> unreadMessages = new List<ChatMessage>();

            if (isAdmin)
            {
                // Admin bakıyorsa: Kullanıcıdan gelenleri okundu yap
                unreadMessages = allMessages.Where(m => m.SenderId == targetId && m.ReceiverId == "Admin" && !m.IsRead).ToList();
            }
            else
            {
                // Kullanıcı bakıyorsa: Kendisine (Adminden) gelenleri okundu yap
                unreadMessages = allMessages.Where(m => m.ReceiverId == currentUserId && !m.IsRead).ToList();
            }

            if (unreadMessages.Any())
            {
                foreach (var item in unreadMessages)
                {
                    item.IsRead = true;
                    _unitOfWork.Repository<ChatMessage>().Update(item);
                }
                await _unitOfWork.CommitAsync();
            }

            return Json(history);
        }
    }
}