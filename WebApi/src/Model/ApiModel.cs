using Microsoft.EntityFrameworkCore;

namespace WebApi.Models
{
    public record UserRegisterRequest(string Username, string Password);
    public record UserLoginRequest(string Username, string Password);

    public record UserResponse(int Id, string Username, string? Role)
    {
        public UserResponse(User user) : this(user.Id, user.Username!, user.Role) { }
    }

    public class User
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
    }

    public class Todo
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsComplete { get; set; }
    }

    public record TodoResponse(int Id, string Name, bool IsComplete)
    {
        public TodoResponse(Todo todo) : this(todo.Id, todo.Name!, todo.IsComplete) { }
    }

    public record TodoCreateRequest(string Name, bool IsComplete);
    public record TodoUpdateRequest(string Name, bool IsComplete);

    public class TodoDb : DbContext
    {
        public TodoDb(DbContextOptions<TodoDb> options) : base(options) { }
        public DbSet<Todo> Todos => Set<Todo>();
    }
}