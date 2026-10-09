using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;

namespace Application.Tasks.Queries;

public record TaskDto(Guid Id, string Title, bool IsCompleted);
public record GetTasksQuery(long UserId) : IRequest<List<TaskDto>>;

public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, List<TaskDto>>
{
    private readonly AppDbContext _context;

    public GetTasksQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TaskDto>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.UserId == request.UserId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TaskDto(t.Id, t.Title, t.IsCompleted))
            .ToListAsync(cancellationToken);
    }
}