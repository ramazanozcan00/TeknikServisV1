using System.Collections.Generic;
using System.Threading.Tasks;
using TeknikServis.Core.DTOs;

namespace TeknikServis.Core.Interfaces
{
    public interface ICurrencyService
    {
        Task<List<CurrencyDto>> GetDailyRatesAsync();
    }
}