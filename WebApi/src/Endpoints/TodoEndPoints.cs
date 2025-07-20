using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;
namespace WebApi.Endpoints
{
    public static class TodoEndpoints
    {
        public static RouteGroupBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
        {
            var todoGroup = app.MapGroup("/todoitems").RequireAuthorization();

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

            return todoGroup;
        }
    }
}