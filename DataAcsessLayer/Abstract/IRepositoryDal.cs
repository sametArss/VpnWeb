using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DataAcsessLayer.Abstract
{
    public interface IRepositoriesDal<T> where T : class
    {
        Task InsertAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);

        Task<T> GetByIdAsync(object id);
        Task<List<T>> GetAllAsync();
        Task<List<T>> GetAllFilterAsync(Expression<Func<T, bool>> filter);

        Task<T> GetByFilterAsync(
            Expression<Func<T, bool>> filter,
            params Expression<Func<T, object>>[] includes
        );

        Task<List<T>> GetAllFilterIncludeAsync(
            Expression<Func<T, bool>> filter,
            params Expression<Func<T, object>>[] includes
        );
    }

}
