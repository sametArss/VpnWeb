using DataAccessLayer.Concrete.Repository;
using DataAcsessLayer.Abstract;
using DataAcsessLayer.Concrete.Context;
using EntityLayer.Concrete;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DataAcsessLayer.EntityFramework
{
    public class EFUserVpnDal : GenericRepositoryDal<UserVpn>, IUserVpnDal
    {
        private readonly VpnDbContext _context;

        public EFUserVpnDal(VpnDbContext context) : base(context)
        {
            _context = context; // Bu satır var mıydı?
        }

        public async Task<int> CountAsync(Expression<Func<UserVpn, bool>> filter)
        {
            Console.WriteLine("CountAsync çağrıldı!"); // Debug için
            var count = await _context.UserVpns.CountAsync(filter);
            Console.WriteLine($"Count sonucu: {count}");
            return count;
        }
    }
}
