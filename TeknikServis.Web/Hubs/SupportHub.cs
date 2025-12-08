using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Interfaces;
using TeknikServis.Core.Entities;
using System.Threading.Tasks;
using System;

namespace TeknikServis.Web.Hubs
{
    [Authorize]
    public class SupportHub : Hub
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public SupportHub(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // KULLANICI -> ADMİNE MESAJ
        public async Task SendMessageToAdmins(string message)
        {
            var senderId = Context.UserIdentifier;
            var user = await _userManager.Users.Include(u => u.Branch).FirstOrDefaultAsync(u => u.Id.ToString() == senderId);

            string displayName = "Kullanıcı";
            if (user != null)
            {
                string branchInfo = user.Branch != null ? $" ({user.Branch.BranchName})" : "";
                displayName = $"{user.FullName}{branchInfo}";
            }

            var chatMsg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SenderId = senderId,
                SenderName = displayName,
                ReceiverId = "Admin",
                Message = message,
                Timestamp = DateTime.Now,
                IsRead = false
            };

            await _unitOfWork.Repository<ChatMessage>().AddAsync(chatMsg);
            await _unitOfWork.CommitAsync();

            // GÜNCELLENDİ: chatMsg.Id parametresi eklendi
            await Clients.Group("Admins").SendAsync("ReceiveMessage", senderId, displayName, message, chatMsg.Id);
        }

        // ADMİN -> KULLANICIYA CEVAP
        public async Task SendMessageToUser(string userId, string message)
        {
            var senderId = Context.UserIdentifier;
            var user = await _userManager.FindByIdAsync(senderId);
            var senderName = user != null ? user.FullName : "Destek Ekibi";

            var chatMsg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SenderId = senderId,
                SenderName = senderName,
                ReceiverId = userId,
                Message = message,
                Timestamp = DateTime.Now,
                IsRead = false
            };

            await _unitOfWork.Repository<ChatMessage>().AddAsync(chatMsg);
            await _unitOfWork.CommitAsync();

            // GÜNCELLENDİ: chatMsg.Id parametresi eklendi
            await Clients.User(userId).SendAsync("ReceiveSupportMessage", message, senderName, chatMsg.Id);
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User.IsInRole("Admin"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }
            await base.OnConnectedAsync();
        }
    }
}