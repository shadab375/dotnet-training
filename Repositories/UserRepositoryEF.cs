using Models;

namespace Repositories {
    public class UserRepositoryEF : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepositoryEF(AppDbContext context)
        {
            _context = context;
        }

        public User? GetById(string id)
        {
            return _context.Users.FirstOrDefault(u => u.Id == id);
        }

        public User? GetByEmail(string email)
        {
            return _context.Users.FirstOrDefault(u => u.Email == email);
        }

        public void Add(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
        }
    }
} 