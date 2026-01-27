using DataAcsessLayer.Abstract;
using DataAcsessLayer.Concrete.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DataAccessLayer.Concrete.Repository
{
    public class GenericRepositoryDal<T> : IRepositoriesDal<T> where T : class
    {
        protected readonly VpnDbContext _context;

        public GenericRepositoryDal(VpnDbContext context)
        {
            _context = context;
        }

        // --- EKLEME ---
        public async Task InsertAsync(T entity)
        {
            await _context.Set<T>().AddAsync(entity);
            await _context.SaveChangesAsync(); // <-- EKLENDİ: DB'ye yazar.
        }

        // --- GÜNCELLEME ---
        public async Task UpdateAsync(T entity)
        {
            _context.Set<T>().Update(entity);
            await _context.SaveChangesAsync(); // <-- EKLENDİ: Değişikliği DB'ye yazar.
        }

        // --- SİLME ---
        public async Task DeleteAsync(T entity)
        {
            _context.Set<T>().Remove(entity);
            await _context.SaveChangesAsync(); // <-- EKLENDİ: Silme işlemini onaylar.
        }

        // --- ID İLE GETİRME ---
        public async Task<T> GetByIdAsync(object id)
        {
            return await _context.Set<T>().FindAsync(id);
        }

        // --- HEPSİNİ GETİRME ---
        public async Task<List<T>> GetAllAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        // --- FİLTRELİ GETİRME ---
        public async Task<List<T>> GetAllFilterAsync(Expression<Func<T, bool>> filter)
        {
            return await _context.Set<T>().Where(filter).ToListAsync();
        }

        // --- TEK KAYIT + INCLUDE (İLİŞKİLİ TABLO) ---
        // Örn: Kullanıcıyı getirirken, Satın aldığı paketi de getir.
        public async Task<T> GetByFilterAsync(Expression<Func<T, bool>> filter, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();

            if (includes != null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            return await query.FirstOrDefaultAsync(filter);
        }

        // --- ÇOKLU KAYIT + INCLUDE ---
        // Örn: Tüm Sunucuları getir, ama hangi ülkede olduklarını da (Location tablosu) getir.
        public async Task<List<T>> GetAllFilterIncludeAsync(Expression<Func<T, bool>> filter, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();

            if (includes != null)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }

            return await query.Where(filter).ToListAsync();
        }
    }
}