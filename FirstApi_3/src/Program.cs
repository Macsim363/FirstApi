using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.UseUrls("http://localhost:5000");


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });

    c.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
    {
        Name = "MyApp.AuthCookie",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Description = "Cookie-based authentication"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "cookieAuth" }
            },
            new string[] { }
        }
    });
});


builder.Services.AddDbContext<TodoDb>(opt => opt.UseInMemoryDatabase("TodoList"));


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MyApp.AuthCookie";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; 
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

var authGroup = app.MapGroup("/auth");
var todoGroup = app.MapGroup("/todoitems").RequireAuthorization();

#region Auth Endpoints


authGroup.MapPost("/register", (UserRegisterRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { message = "Username and password required" });

    if (UserRepository.Find(req.Username) != null)
        return Results.Conflict(new { message = "User already exists" });

    var user = new User
    {
        Username = req.Username,
        Password = req.Password,
        Role = "User"
    };

    UserRepository.Add(user);

    return Results.Created($"/auth/{user.Id}", new UserResponse(user));
})
.WithName("RegisterUser")
.WithTags("auth")
.Produces<UserResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict);


authGroup.MapPost("/login", async (UserLoginRequest req, HttpContext httpContext) =>
{
    var user = UserRepository.Find(req.Username);
    if (user == null || user.Password != req.Password)
        return Results.Unauthorized();

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Username!),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Role ?? "User")
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    return Results.Ok(new UserResponse(user));
})
.WithName("LoginUser")
.WithTags("auth")
.Produces<UserResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);


authGroup.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Logged out" });
})
.WithName("LogoutUser")
.WithTags("auth")
.Produces(StatusCodes.Status200OK);

#endregion

#region Todo Endpoints


todoGroup.MapGet("/", async (TodoDb db) =>
{
    var todos = await db.Todos.ToListAsync();
    var responses = todos.Select(t => new TodoResponse(t)).ToList();
    return Results.Ok(responses);
})
.WithName("GetTodos")
.WithTags("todoitems")
.Produces<List<TodoResponse>>(StatusCodes.Status200OK);


todoGroup.MapGet("/{id:int}", async (int id, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    return Results.Ok(new TodoResponse(todo));
})
.WithName("GetTodoById")
.WithTags("todoitems")
.Produces<TodoResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);


todoGroup.MapPost("/", async (TodoCreateRequest req, TodoDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest(new { message = "Name is required" });

    var todo = new Todo { Name = req.Name, IsComplete = req.IsComplete };
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todoitems/{todo.Id}", new TodoResponse(todo));
})
.WithName("CreateTodo")
.WithTags("todoitems")
.Produces<TodoResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);


todoGroup.MapPut("/{id:int}", async (int id, TodoUpdateRequest req, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    todo.Name = req.Name;
    todo.IsComplete = req.IsComplete;
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("UpdateTodo")
.WithTags("todoitems")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);


todoGroup.MapDelete("/{id:int}", async (int id, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("DeleteTodo")
.WithTags("todoitems")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

#endregion


app.MapGet("/", () => "Hello World!").WithTags("root");

app.Run();

#region Модели и сущности

public record UserRegisterRequest(string Username, string Password);
public record UserLoginRequest(string Username, string Password);
public record UserResponse(int Id, string Username, string? Role)
{
    public UserResponse(User user) : this(user.Id, user.Username!, user.Role) { }
}

public record TodoCreateRequest(string Name, bool IsComplete);
public record TodoUpdateRequest(string Name, bool IsComplete);
public record TodoResponse(int Id, string Name, bool IsComplete)
{
    public TodoResponse(Todo todo) : this(todo.Id, todo.Name!, todo.IsComplete) { }
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

public class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options) : base(options) { }
    public DbSet<Todo> Todos => Set<Todo>();
}

public static class UserRepository
{
    private static readonly List<User> users = new();
    public static User? Find(string username) =>
        users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
    public static void Add(User user)
    {
        user.Id = users.Count + 1;
        users.Add(user);
    }
}

#endregion
