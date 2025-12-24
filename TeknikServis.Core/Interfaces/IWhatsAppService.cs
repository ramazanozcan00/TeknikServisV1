using System.Threading.Tasks;

namespace TeknikServis.Core.Interfaces
{
    public interface IWhatsAppService
    {

        Task<bool> SendMessageAsync(string phoneNumber, string message, Guid branchId);

    }
}