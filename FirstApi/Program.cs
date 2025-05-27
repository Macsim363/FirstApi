using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });

    
    c.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
    {
        Name = "Cookie",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Description = "Cookie-based authentication"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference 
                { 
                    Type = ReferenceType.SecurityScheme, 
                    Id = "cookieAuth" 
                }
            },
            new string[] { }
        }
    });
});

builder.Services.AddDbContext<TodoDb>(opt => opt.UseInMemoryDatabase("TodoList"));
builder.Services.AddScoped<AuthFilter>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
   .AddCookie(options => 
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = "MyApp.AuthCookie";
    
    
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();


var FirstTaskGroup = app.MapGroup("/todoitems")
    .RequireAuthorization()
    .AddEndpointFilter<AuthFilter>();

var SecondTaskGroup = app.MapGroup("/tasks");


SecondTaskGroup.MapPost("/register", (User user) =>
{
    if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password))
        return Results.BadRequest(new { message = "Username and password are required" });

    if (UserRepository.Find(user.Username) != null)
        return Results.Conflict(new { message = "User already exists" });

    UserRepository.Add(user);
    user.Password = string.Empty;
    return Results.Created($"/tasks/{user.Id}", user);
}).WithTags("tasks");

SecondTaskGroup.MapPost("/login", async (User loginModel, HttpContext httpContext) =>
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
}).WithTags("tasks");

SecondTaskGroup.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Logged out" });
}).WithTags("tasks");

// Todo items endpoints (require authorization)
FirstTaskGroup.MapGet("/", async (TodoDb db) =>
    await db.Todos.ToListAsync()).WithTags("todoitems");

FirstTaskGroup.MapGet("/{id}", async (int id, TodoDb db) =>
    await db.Todos.FindAsync(id) is Todo todo ? Results.Ok(todo) : Results.NotFound()).WithTags("todoitems");

FirstTaskGroup.MapPost("/", async (Todo todo, TodoDb db) =>
{
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/todoitems/{todo.Id}", todo);
}).WithTags("todoitems");

FirstTaskGroup.MapPut("/{id}", async (int id, Todo inputTodo, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    todo.Name = inputTodo.Name;
    todo.IsComplete = inputTodo.IsComplete;
    await db.SaveChangesAsync();

    return Results.NoContent();
}).WithTags("todoitems");

FirstTaskGroup.MapDelete("/{id}", async (int id, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();

    return Results.NoContent();
}).WithTags("todoitems");

// Root endpoint
app.MapGet("/", () => "Hello World!").WithTags("todoitems");

app.Run();


// Models and supporting classes

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