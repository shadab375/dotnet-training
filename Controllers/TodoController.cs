using Microsoft.AspNetCore.Mvc;
using Models;
using Repositories;
using System.Security.Claims;

namespace Controllers {
    [ApiController]
    [Route("api/todos")]
    public class TodoController : ControllerBase
    {
        private readonly ITodoRepository _repository;

        public TodoController(ITodoRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public IActionResult GetAllTodos()
        {
            var userId = GetUserIdFromToken();
            if (userId == null) return Unauthorized();
            return Ok(_repository.GetAllByUserId(userId));
        }

        [HttpGet("{id}")]
        public IActionResult GetTodo(string id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null) return Unauthorized();
            var todo = _repository.GetById(id);
            if (todo == null) return NotFound();
            if (todo.UserId != userId) return Forbid();
            return Ok(todo);
        }

        [HttpPost]
        public IActionResult CreateTodo([FromBody] Todo todo)
        {
            var userId = GetUserIdFromToken();
            if (userId == null) return Unauthorized();
            todo.UserId = userId;
            todo.Id = Guid.NewGuid().ToString();
            _repository.Add(todo);
            return Created($"/api/todos/{todo.Id}", todo);
        }

        [HttpPut("{id}")]
        public IActionResult UpdateTodo(string id, [FromBody] Todo todo)
        {
            var userId = GetUserIdFromToken();
            if (userId == null) return Unauthorized();
            var existingTodo = _repository.GetById(id);
            if (existingTodo == null) return NotFound();
            if (existingTodo.UserId != userId) return Forbid();
            todo.Id = id;
            todo.UserId = userId;
            _repository.Update(todo);
            return Ok(todo);
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteTodo(string id)
        {
            var userId = GetUserIdFromToken();
            if (userId == null) return Unauthorized();
            var todo = _repository.GetById(id);
            if (todo == null) return NotFound();
            if (todo.UserId != userId) return Forbid();
            _repository.Delete(id);
            return NoContent();
        }

        private string? GetUserIdFromToken()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
} 