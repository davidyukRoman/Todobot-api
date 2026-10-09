using MediatR;
using Infrastructure.Persistence;

namespace Application.Tasks.Commands;

public record ToggleTaskCommand(Guid Id) : IRequest;

public class ToggleTaskCommandHandler : IRequestHandler<ToggleTaskCommand>
{
    private readonly AppDbContext _context;
    public ToggleTaskCommandHandler(AppDbContext context) => _context = context;

    public async Task Handle(ToggleTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks.FindAsync([request.Id], cancellationToken);
        if (task != null)
        {
            task.IsCompleted = !task.IsCompleted;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public record DeleteTaskCommand(Guid Id) : IRequest;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand>
{
    private readonly AppDbContext _context;
    public DeleteTaskCommandHandler(AppDbContext context) => _context = context;

    public async Task Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _context.Tasks.FindAsync([request.Id], cancellationToken);
        if (task != null)
        {
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}