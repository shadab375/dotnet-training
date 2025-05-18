using Models;

namespace Repositories {
    public class TodoRepositoryEF : ITodoRepository
    {
        private readonly AppDbContext _context;

        public TodoRepositoryEF(AppDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Todo> GetAllByUserId(string userId)
        {
            return _context.Todos.Where(t => t.UserId == userId).ToList();
        }

        public Todo? GetById(string id)
        {
            return _context.Todos.FirstOrDefault(t => t.Id == id);
        }

        public void Add(Todo todo)
        {
            _context.Todos.Add(todo);
            _context.SaveChanges();
        }

        public void Update(Todo todo)
        {
            var existingTodo = _context.Todos.Find(todo.Id);
            if (existingTodo != null)
            {
                existingTodo.Title = todo.Title;
                existingTodo.Description = todo.Description;
                existingTodo.Completed = todo.Completed;
                existingTodo.Deadline = todo.Deadline;
                existingTodo.Priority = todo.Priority;
                existingTodo.UserId = todo.UserId;
                
                _context.SaveChanges();
            }
        }

        public void Delete(string id)
        {
            var todo = _context.Todos.Find(id);
            if (todo != null)
            {
                _context.Todos.Remove(todo);
                _context.SaveChanges();
            }
        }
    }
} 