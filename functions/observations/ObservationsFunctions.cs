using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SideSpins.Api.Observations;
using SideSpins.Api.Services;
using SidesSpins.Functions;

namespace SideSpins.Api;

public class ObservationsFunctions
{
    private readonly ILogger<ObservationsFunctions> _logger;
    private readonly ObservationsService _observationsService;
    private readonly BlobService _blobService;

    public ObservationsFunctions(
        ILogger<ObservationsFunctions> logger,
        ObservationsService observationsService,
        BlobService blobService
    )
    {
        _logger = logger;
        _observationsService = observationsService;
        _blobService = blobService;
    }

    [Function("GetObservations")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetObservations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var observations = await _observationsService.GetObservationsAsync();
            return new OkObjectResult(observations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting observations");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetObservation")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetObservation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "observations/{id}")]
            HttpRequest req,
        FunctionContext context,
        string id
    )
    {
        try
        {
            var observation = await _observationsService.GetObservationByIdAsync(id);
            if (observation == null)
            {
                return new NotFoundObjectResult(new { error = "Observation not found" });
            }
            return new OkObjectResult(observation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting observation {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("CreateObservation")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> CreateObservation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var observation = JsonConvert.DeserializeObject<Observation>(requestBody);

            if (observation == null)
            {
                return new BadRequestObjectResult(new { error = "Invalid observation data" });
            }

            // Validate required fields
            if (string.IsNullOrEmpty(observation.Label))
            {
                return new BadRequestObjectResult(
                    new { error = "Label is required (practice or match)" }
                );
            }

            if (observation.Label != "practice" && observation.Label != "match")
            {
                return new BadRequestObjectResult(
                    new { error = "Label must be 'practice' or 'match'" }
                );
            }

            var createdObservation = await _observationsService.CreateObservationAsync(observation);
            return new OkObjectResult(createdObservation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating observation");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateObservation")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> UpdateObservation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "observations/{id}")]
            HttpRequest req,
        FunctionContext context,
        string id
    )
    {
        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var observation = JsonConvert.DeserializeObject<Observation>(requestBody);

            if (observation == null)
            {
                return new BadRequestObjectResult(new { error = "Invalid observation data" });
            }

            var updatedObservation = await _observationsService.UpdateObservationAsync(
                id,
                observation
            );
            if (updatedObservation == null)
            {
                return new NotFoundObjectResult(new { error = "Observation not found" });
            }

            return new OkObjectResult(updatedObservation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating observation {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteObservation")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> DeleteObservation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "observations/{id}")]
            HttpRequest req,
        FunctionContext context,
        string id
    )
    {
        try
        {
            var deleted = await _observationsService.DeleteObservationAsync(id);
            if (!deleted)
            {
                return new NotFoundObjectResult(new { error = "Observation not found" });
            }

            return new OkObjectResult(new { message = "Observation deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting observation {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("GetVideoPlaybackUrl")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> GetVideoPlaybackUrl(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "observations/{id}/video-url")]
            HttpRequest req,
        FunctionContext context,
        string id
    )
    {
        try
        {
            var observation = await _observationsService.GetObservationByIdAsync(id);
            if (observation == null)
            {
                return new NotFoundObjectResult(new { error = "Observation not found" });
            }

            if (observation.RecordingParts == null || observation.RecordingParts.Count == 0)
            {
                return new BadRequestObjectResult(
                    new { error = "No recording parts attached to this observation" }
                );
            }

            // Return URLs for all parts
            var videoParts = observation
                .RecordingParts.Select(part => new
                {
                    partNumber = part.PartNumber,
                    videoUrl = _blobService.GetPublicBlobUrl(part),
                    startOffsetSeconds = part.StartOffsetSeconds,
                    durationSeconds = part.DurationSeconds,
                })
                .OrderBy(p => p.startOffsetSeconds)
                .ToList();

            return new OkObjectResult(new { videoParts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video URL for observation {Id}", id);
            return new StatusCodeResult(500);
        }
    }

    [Function("ListBlobs")]
    [RequireAuthentication("player")]
    public async Task<IActionResult> ListBlobs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "blobs/list")]
            HttpRequest req,
        FunctionContext context
    )
    {
        try
        {
            // Get container name from query parameter, default to configured container
            var containerName = req.Query["container"].FirstOrDefault() 
                ?? Environment.GetEnvironmentVariable("BLOB_CONTAINER_NAME") 
                ?? "videos";

            var blobs = await _blobService.ListBlobsAsync(containerName);

            return new OkObjectResult(new { blobs, container = containerName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing blobs in container");
            return new StatusCodeResult(500);
        }
    }}