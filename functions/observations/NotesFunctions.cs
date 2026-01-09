using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SideSpins.Api.Observations;
using SideSpins.Api.Services;
using SidesSpins.Functions;

namespace SideSpins.Api;

public class NotesFunctions
{
    private readonly ILogger<NotesFunctions> _logger;
    private readonly ObservationsService _observationsService;

    public NotesFunctions(ILogger<NotesFunctions> logger, ObservationsService observationsService)
    {
        _logger = logger;
        _observationsService = observationsService;
    }

    [Function("GetNotes")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetNotes(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "observations/{observationId}/notes"
        )]
            HttpRequest req,
        FunctionContext context,
        string observationId
    )
    {
        try
        {
            // Verify observation exists
            var observation = await _observationsService.GetObservationByIdAsync(observationId);
            if (observation == null)
            {
                return new NotFoundObjectResult(new { error = "Observation not found" });
            }

            var notes = await _observationsService.GetNotesByObservationIdAsync(observationId);
            return new OkObjectResult(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting notes for observation {ObservationId}",
                observationId
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("GetNote")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetNote(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "observations/{observationId}/notes/{id}"
        )]
            HttpRequest req,
        FunctionContext context,
        string observationId,
        string id
    )
    {
        try
        {
            var note = await _observationsService.GetNoteByIdAsync(id, observationId);
            if (note == null)
            {
                return new NotFoundObjectResult(new { error = "Note not found" });
            }
            return new OkObjectResult(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting note {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateNote")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> CreateNote(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "observations/{observationId}/notes"
        )]
            HttpRequest req,
        FunctionContext context,
        string observationId
    )
    {
        try
        {
            // Verify observation exists
            var observation = await _observationsService.GetObservationByIdAsync(observationId);
            if (observation == null)
            {
                return new NotFoundObjectResult(new { error = "Observation not found" });
            }

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var note = JsonConvert.DeserializeObject<Note>(requestBody);

            if (note == null)
            {
                return new BadRequestObjectResult(new { error = "Invalid note data" });
            }

            // Validate required fields
            if (string.IsNullOrEmpty(note.Text))
            {
                return new BadRequestObjectResult(new { error = "Text is required" });
            }

            // Ensure observationId matches route
            note.ObservationId = observationId;

            var createdNote = await _observationsService.CreateNoteAsync(note);
            return new OkObjectResult(createdNote);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating note for observation {ObservationId}",
                observationId
            );
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateNote")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> UpdateNote(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "patch",
            Route = "observations/{observationId}/notes/{id}"
        )]
            HttpRequest req,
        FunctionContext context,
        string observationId,
        string id
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var note = JsonConvert.DeserializeObject<Note>(requestBody);

            if (note == null)
            {
                return new BadRequestObjectResult(new { error = "Invalid note data" });
            }

            // Ensure observationId matches route
            note.ObservationId = observationId;

            var updatedNote = await _observationsService.UpdateNoteAsync(id, observationId, note);
            if (updatedNote == null)
            {
                return new NotFoundObjectResult(new { error = "Note not found" });
            }

            return new OkObjectResult(updatedNote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating note {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteNote")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> DeleteNote(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "delete",
            Route = "observations/{observationId}/notes/{id}"
        )]
            HttpRequest req,
        FunctionContext context,
        string observationId,
        string id
    )
    {
        try
        {
            var deleted = await _observationsService.DeleteNoteAsync(id, observationId);
            if (!deleted)
            {
                return new NotFoundObjectResult(new { error = "Note not found" });
            }

            return new OkObjectResult(new { message = "Note deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting note {Id}", id);
            return new StatusCodeResult(500);
        }
    }
}
