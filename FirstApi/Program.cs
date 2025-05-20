using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<TodoDb>(opt => opt.UseInMemoryDatabase("TodoList"));
builder.Services.AddScoped<AuthFilter>();
builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "My API";
    config.Version = "v1";

    
    config.AddSecurity("cookieAuth", Enumerable.Empty<string>(), new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.ApiKey,
        Name = "Cookie",
        In = NSwag.OpenApiSecurityApiKeyLocation.Header,
        Description = "Cookie-based authentication"
    });

    config.OperationProcessors.Add(
        new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("cookieAuth"));
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/tasks/login"; 
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var FirstTaskGroup = app.MapGroup("/todoitems").RequireAuthorization().AddEndpointFilter<AuthFilter>();
var SeccondTaskGroup = app.MapGroup("/tasks");


SeccondTaskGroup.MapPost("/register", (User user) =>
{
    if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password))
        return Results.BadRequest(new { message = "Username and password are required" });

    if (UserRepository.Find(user.Username) != null)
        return Results.Conflict(new { message = "User already exists" });

    UserRepository.Add(user);
    user.Password = string.Empty;
    return Results.Created($"/tasks/{user.Id}", user);
});


SeccondTaskGroup.MapPost("/login", async (User loginModel, HttpContext httpContext) =>
{
    var user = UserRepository.Find(loginModel.Username);

    if (user == null || user.Password != loginModel.Password)
        return Results.Unauthorized();

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Username!),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Role ?? "User")
    };

    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

    user.Password = string.Empty;
    return Results.Ok(new { user });
});


SeccondTaskGroup.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Logged out" });
});


FirstTaskGroup.MapGet("/", async (TodoDb db) =>
    await db.Todos.ToListAsync());

FirstTaskGroup.MapGet("/{id}", async (int id, TodoDb db) =>
    await db.Todos.FindAsync(id) is Todo todo ? Results.Ok(todo) : Results.NotFound());

FirstTaskGroup.MapPost("/", async (Todo todo, TodoDb db) =>
{
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/todoitems/{todo.Id}", todo);
});

FirstTaskGroup.MapPut("/{id}", async (int id, Todo inputTodo, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    todo.Name = inputTodo.Name;
    todo.IsComplete = inputTodo.IsComplete;
    await db.SaveChangesAsync();

    return Results.NoContent();
});

FirstTaskGroup.MapDelete("/{id}", async (int id, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();

    return Results.NoContent();
});


app.UseOpenApi();
app.UseSwaggerUi();


app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

app.Run();



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
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}

public static class UserRepository
{
    private static readonly List<User> users = new();

    public static User? Find(string username) =>
        users.FirstOrDefault(u => u.Username?.ToLower() == username.ToLower());

    public static void Add(User user)
    {
        user.Id = users.Count + 1;
        users.Add(user);
    }
}

public class AuthFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            
            return Results.Unauthorized();
        }

        
        return await next(context);
    }
}