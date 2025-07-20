using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using WebApi.Models; 
using WebApi.Repositories;
namespace WebApi.Endpoints
{
    public static class AuthEndpoints
    {
        public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var authGroup = app.MapGroup("/auth");

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

            return authGroup;
        }
    }
}
