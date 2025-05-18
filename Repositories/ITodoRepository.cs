namespace Repositories {
    public interface ITodoRepository
    {
        IEnumerable<Todo> GetAllByUserId(string userId);
        Todo? GetById(string id);
        void Add(Todo todo);
        void Update(Todo todo);
        void Delete(string id);
    }
} 