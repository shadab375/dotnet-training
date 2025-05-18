namespace Repositories {
    public interface IUserRepository
    {
        User? GetById(string id);
        User? GetByEmail(string email);
        void Add(User user);
    }
} 