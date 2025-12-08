using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System; // Guid için gerekli

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

        // ... GetActiveChats ve CheckUnread metodları aynen kalacak ...

        // LİSTELEME: Admin için aktif sohbetleri getirir
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetActiveChats()
        {
            // Mevcut kodunuz aynen kalabilir...
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
                if (string.IsNullOrEmpty(displayName) || displayName == "Kullanıcı" || !displayName.Contains("("))
                {
                    var user = await _userManager.Users.Include(u => u.Branch).FirstOrDefaultAsync(u => u.Id.ToString() == chat.UserId);
                    if (user != null)
                    {
                        string branchInfo = user.Branch != null ? $" ({user.Branch.BranchName})" : "";
                        displayName = $"{user.FullName}{branchInfo}";
                    }
                    else { displayName = "Bilinmeyen Kullanıcı"; }
                }

                resultList.Add(new
                {
                    chat.UserId,
                    LastMessage = chat.LastMessageObj.Message,
                    UserName = displayName,
                    chat.UnreadCount,
                    LastDate = chat.LastMessageObj.Timestamp
                });
            }
            return Json(resultList);
        }

        // KONTROL: Okunmamış mesaj sayısı
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

        // --- GÜNCELLENEN METOT: GEÇMİŞİ GETİR (ID EKLENDİ) ---
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
                    id = m.Id, // <-- BURASI EKLENDİ: Mesajı silmek için ID lazım
                    sender = m.SenderId == targetId ? "User" : "Admin",
                    message = m.Message,
                    time = m.Timestamp.ToString("HH:mm"),
                    senderName = m.SenderName
                })
                .ToList();

            // Okundu İşaretleme
            List<ChatMessage> unreadMessages = new List<ChatMessage>();
            if (isAdmin)
                unreadMessages = allMessages.Where(m => m.SenderId == targetId && m.ReceiverId == "Admin" && !m.IsRead).ToList();
            else
                unreadMessages = allMessages.Where(m => m.ReceiverId == currentUserId && !m.IsRead).ToList();

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

        // --- YENİ EKLENEN METOT: MESAJ SİLME ---
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMessage(Guid id)
        {
            var msg = await _unitOfWork.Repository<ChatMessage>().GetByIdAsync(id);
            if (msg == null) return Json(new { success = false, message = "Mesaj bulunamadı." });

            _unitOfWork.Repository<ChatMessage>().Remove(msg);
            await _unitOfWork.CommitAsync();

            return Json(new { success = true });
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteChat(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return Json(new { success = false, message = "ID hatalı." });

            // Kullanıcıya ait (gönderdiği veya aldığı) tüm mesajları bul
            var messages = await _unitOfWork.Repository<ChatMessage>()
                .FindAsync(m => m.SenderId == userId || m.ReceiverId == userId);

            if (messages.Any())
            {
                foreach (var msg in messages)
                {
                    _unitOfWork.Repository<ChatMessage>().Remove(msg);
                }
                await _unitOfWork.CommitAsync();
            }

            return Json(new { success = true });
        }




    }
}