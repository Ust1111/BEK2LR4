using ArticulationAPI.AssignmentApi.Contracts;
using ArticulationAPI.AssignmentApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ArticulationAPI.AssignmentApi.Controllers;

[ApiController]
[Route("assignments")]
public class AssignmentsController : ControllerBase
{
    private static readonly string[] SupportedLanguages = { "ru", "en", "de", "fr" };

    private readonly AssignmentsService _service;

    public AssignmentsController(AssignmentsService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<StartAssignmentResponse>> Start(StartAssignmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Student))
        {
            return BadRequest("Student is required");
        }

        if (request.Words is null || request.Words.Length == 0
            || request.Words.Any(w => string.IsNullOrWhiteSpace(w.Word) || string.IsNullOrWhiteSpace(w.Transcription)))
        {
            return BadRequest("At least one word with a transcription is required");
        }

        if (!SupportedLanguages.Contains(request.Language))
        {
            return BadRequest($"Language '{request.Language}' is not supported. Supported: {string.Join(", ", SupportedLanguages)}");
        }

        return Accepted(await _service.StartAsync(request));
    }

    [HttpGet("{assignmentId}")]
    public async Task<ActionResult<AssignmentStatusResponse>> GetStatus(string assignmentId)
    {
        var status = await _service.GetStatusAsync(assignmentId);

        if (status is null)
        {
            return NotFound("Assignment not found");
        }

        return Ok(status);
    }
    
    [HttpPost("{assignmentId}/review")]
    public async Task<IActionResult> Review(string assignmentId, ReviewRequest request)
    {
        if (request.Grade is < 0 or > 100)
        {
            return BadRequest("Grade must be between 0 and 100");
        }

        var sent = await _service.ReviewAsync(assignmentId, request);

        if (!sent)
        {
            return NotFound("Assignment not found or already finished");
        }

        return Accepted();
    }
}