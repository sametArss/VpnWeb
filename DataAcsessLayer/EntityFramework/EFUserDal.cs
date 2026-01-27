using DataAccessLayer.Concrete.Repository;
using DataAcsessLayer.Abstract;
using DataAcsessLayer.Concrete.Context;
using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcsessLayer.EntityFramework
{
    public class EFUserDal : GenericRepositoryDal<User>, IUserDal
    {
        public EFUserDal(VpnDbContext context) : base(context)
        {
        }
    }
}
