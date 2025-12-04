using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        // İstediğimiz entity'nin repository'sini çağırmak için generic metod
        IGenericRepository<T> Repository<T>() where T : BaseEntity;

        // Değişiklikleri kaydetmek için
        Task<int> CommitAsync();
    }
}
