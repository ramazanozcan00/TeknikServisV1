using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Core.Interfaces
{
    public interface IGenericRepository<T> where T : BaseEntity
    {
        Task<T> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();

        // ESKİSİ: Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        // YENİSİ: Hem filtreleme hem de Include yapabilen süper metod
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);

        // Sadece ID ile değil, ilişkileriyle beraber tek kayıt getirmek için (Details sayfası için lazım)
        Task<T> GetByIdWithIncludesAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);

        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
    }
}
