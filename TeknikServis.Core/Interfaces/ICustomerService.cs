using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Core.Interfaces
{
    public interface ICustomerService
    {
        // GÜNCELLENEN KISIM:
        // Arama parametresi (search) opsiyonel olarak eklendi.
        // Böylece hem eski kodlar (sadece ID ile çalışanlar) hem yeni kodlar (arama yapanlar) çalışır.
        Task<(IEnumerable<Customer> customers, int totalCount)> GetCustomersByBranchAsync(Guid branchId, int page, int pageSize, string search = null);
        Task<IEnumerable<Customer>> GetCustomersByBranchAsync(Guid branchId);
        Task CreateCustomerAsync(Customer customer);

        Task<Customer> GetCustomerDetailsAsync(Guid id);

        Task<Customer> GetByIdAsync(Guid id);

        Task DeleteCustomerAsync(Guid id);

        Task UpdateCustomerAsync(Customer customer);


        Task<Customer> GetByPhoneAsync(string phoneNumber);
    }
}