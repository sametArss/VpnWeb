using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DataAcsessLayer.Abstract
{
    public interface IUserVpnDal:IRepositoriesDal<EntityLayer.Concrete.UserVpn>
    {
        Task<int> CountAsync(Expression<Func<EntityLayer.Concrete.UserVpn, bool>> filter);
    }
}
