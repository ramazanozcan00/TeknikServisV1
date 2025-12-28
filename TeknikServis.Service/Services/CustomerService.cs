using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Service.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // --- YENİ MÜŞTERİ OLUŞTURMA ---
        public async Task CreateCustomerAsync(Customer customer)
        {
            customer.Id = Guid.NewGuid();
            customer.CreatedDate = DateTime.Now;
            customer.IsDeleted = false;

            await _unitOfWork.Repository<Customer>().AddAsync(customer);
            await _unitOfWork.CommitAsync();
        }

        // --- MÜŞTERİ GÜNCELLEME (DÜZELTİLDİ) ---
        public async Task UpdateCustomerAsync(Customer customer)
        {
            var existingCustomer = await _unitOfWork.Repository<Customer>().GetByIdAsync(customer.Id);

            if (existingCustomer != null)
            {
                // 1. Temel Bilgiler
                existingCustomer.FirstName = customer.FirstName;
                existingCustomer.LastName = customer.LastName;
                existingCustomer.Email = customer.Email;
                existingCustomer.Phone = customer.Phone;

                // 2. Yeni Eklenen Alanlar
                existingCustomer.CompanyName = customer.CompanyName;
                existingCustomer.Phone2 = customer.Phone2;
                existingCustomer.TCNo = customer.TCNo;
                existingCustomer.CustomerType = customer.CustomerType;

                // 3. Adres ve Vergi Bilgileri
                existingCustomer.Address = customer.Address;
                existingCustomer.City = customer.City;
                existingCustomer.District = customer.District;
                existingCustomer.TaxOffice = customer.TaxOffice;
                existingCustomer.TaxNumber = customer.TaxNumber;

                // Güncelleme Tarihi
                existingCustomer.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<Customer>().Update(existingCustomer);
                await _unitOfWork.CommitAsync();
            }
        }

        // --- MÜŞTERİ LİSTELEME (SAYFALAMALI) ---
        public async Task<(IEnumerable<Customer> customers, int totalCount)> GetCustomersByBranchAsync(Guid branchId, int page, int pageSize, string search = null)
        {
            // 1. Şubeye ait (silinmemiş) müşterileri çek
            var allCustomers = await _unitOfWork.Repository<Customer>()
                .FindAsync(x => x.BranchId == branchId && !x.IsDeleted);

            // 2. Arama Filtresi
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                allCustomers = allCustomers.Where(x =>
                    x.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (x.Phone != null && x.Phone.Contains(search)) ||
                    (x.Email != null && x.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.CompanyName != null && x.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            // 3. Toplam Sayı
            int totalCount = allCustomers.Count();

            // 4. Sayfalama (En yeniden eskiye)
            var pagedCustomers = allCustomers
                .OrderByDescending(x => x.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedCustomers, totalCount);
        }

        // --- MÜŞTERİ LİSTELEME (SAYFALAMASIZ - DROPDOWN İÇİN) ---
        public async Task<IEnumerable<Customer>> GetCustomersByBranchAsync(Guid branchId)
        {
            var customers = await _unitOfWork.Repository<Customer>()
                .FindAsync(x => x.BranchId == branchId && !x.IsDeleted);

            return customers.OrderByDescending(c => c.CreatedDate);
        }

        // --- ID İLE GETİRME ---
        public async Task<Customer> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.Repository<Customer>().GetByIdAsync(id);
        }

        // --- DETAY GETİRME (İLİŞKİLİ TABLOLARLA) ---
        public async Task<Customer> GetCustomerDetailsAsync(Guid id)
        {
            return await _unitOfWork.Repository<Customer>()
                .GetByIdWithIncludesAsync(x => x.Id == id, x => x.ServiceTickets);
        }

        // --- SİLME (SOFT DELETE) ---
        public async Task DeleteCustomerAsync(Guid id)
        {
            var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(id);
            if (customer != null)
            {
                customer.IsDeleted = true;
                customer.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<Customer>().Update(customer);
                await _unitOfWork.CommitAsync();
            }
        }


        public async Task<Customer> GetByPhoneAsync(string phoneNumber)
        {
            // Telefon numarasına göre (silinmemiş) ilk müşteriyi bul
            // Not: Telefon formatı veritabanında nasıl tutuluyorsa ona dikkat etmelisiniz.
            // Contains yerine tam eşleşme veya 'EndsWith' daha güvenli olabilir.
            var customers = await _unitOfWork.Repository<Customer>()
                .FindAsync(x => x.Phone.Contains(phoneNumber) && !x.IsDeleted);

            return customers.FirstOrDefault();
        }
    }
}