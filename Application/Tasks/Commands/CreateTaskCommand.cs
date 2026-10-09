using Domain;
using Infrastructure.Persistence;
using MediatR;

namespace Application.Tasks.Commands;

public record CreateTaskCommand(long UserId, string Title) : IRequest<string>;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, string>
{
    private readonly AppDbContext _context;

    public CreateTaskCommandHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = new TodoTask
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        return $"✅ Завдання \"{request.Title}\" успішно додано!";
    }
}