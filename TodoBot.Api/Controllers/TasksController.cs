using Application.Tasks.Commands;
using Application.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TodoBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetTasks([FromQuery] long userId)
    {
        var tasks = await _mediator.Send(new GetTasksQuery(userId));
        return Ok(tasks);
    }
    
    [HttpPut("{id:guid}/toggle")]
    public async Task<IActionResult> ToggleTask(Guid id)
    {
        await _mediator.Send(new ToggleTaskCommand(id));
        return NoContent();
    }
    
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        await _mediator.Send(new DeleteTaskCommand(id));
        return NoContent();
    }
}