using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using SideSpins.Api.Observations;

namespace SideSpins.Api.Services;

public class ObservationsService
{
    private readonly Container _observationsContainer;
    private readonly Container _notesContainer;
    private readonly ILogger<ObservationsService> _logger;
    private readonly string _storageAccountName;
    private readonly string _containerName;

    public ObservationsService(
        CosmosClient cosmosClient,
        string databaseName,
        string storageAccountName,
        string containerName,
        ILogger<ObservationsService> logger
    )
    {
        var database = cosmosClient.GetDatabase(databaseName);
        _observationsContainer = database.GetContainer("Observations");
        _notesContainer = database.GetContainer("Notes");
        _logger = logger;
        _storageAccountName = storageAccountName;
        _containerName = containerName;
    }

    #region Observation Operations

    public async Task<IEnumerable<Observation>> GetObservationsAsync()
    {
        var query = "SELECT * FROM c WHERE c.type = 'observation' ORDER BY c.startTime DESC";
        var queryDefinition = new QueryDefinition(query);
        var resultSet = _observationsContainer.GetItemQueryIterator<Observation>(queryDefinition);

        var observations = new List<Observation>();
        while (resultSet.HasMoreResults)
        {
            var response = await resultSet.ReadNextAsync();
            observations.AddRange(response.ToList());
        }

        return observations;
    }

    public async Task<Observation?> GetObservationByIdAsync(string id)
    {
        try
        {
            var response = await _observationsContainer.ReadItemAsync<Observation>(
                id,
                new PartitionKey(id)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Observation> CreateObservationAsync(Observation observation)
    {
        if (string.IsNullOrEmpty(observation.Id))
        {
            observation.Id = $"obs_{Guid.NewGuid():N}";
        }

        observation.Type = "observation";
        observation.CreatedAt = DateTime.UtcNow;
        observation.UpdatedAt = DateTime.UtcNow;

        // Default to active status if not specified
        if (string.IsNullOrEmpty(observation.Status))
        {
            observation.Status = "active";
        }

        // Populate recordingParts defaults if present
        if (observation.RecordingParts != null && observation.RecordingParts.Any())
        {
            foreach (var part in observation.RecordingParts)
            {
                if (string.IsNullOrEmpty(part.StorageAccount))
                {
                    part.StorageAccount = _storageAccountName;
                }
                if (string.IsNullOrEmpty(part.Container))
                {
                    part.Container = _containerName;
                }
            }
        }

        var response = await _observationsContainer.CreateItemAsync(
            observation,
            new PartitionKey(observation.Id)
        );
        return response.Resource;
    }

    public async Task<Observation?> UpdateObservationAsync(string id, Observation observation)
    {
        try
        {
            observation.Id = id;
            observation.Type = "observation";
            observation.UpdatedAt = DateTime.UtcNow;

            // Preserve createdAt if not set
            if (observation.CreatedAt == default)
            {
                var existing = await GetObservationByIdAsync(id);
                if (existing != null)
                {
                    observation.CreatedAt = existing.CreatedAt;
                }
                else
                {
                    observation.CreatedAt = DateTime.UtcNow;
                }
            }

            // Populate recordingParts defaults if present
            if (observation.RecordingParts != null && observation.RecordingParts.Any())
            {
                foreach (var part in observation.RecordingParts)
                {
                    if (string.IsNullOrEmpty(part.StorageAccount))
                    {
                        part.StorageAccount = _storageAccountName;
                    }
                    if (string.IsNullOrEmpty(part.Container))
                    {
                        part.Container = _containerName;
                    }
                }
            }

            var response = await _observationsContainer.ReplaceItemAsync(
                observation,
                id,
                new PartitionKey(id)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteObservationAsync(string id)
    {
        try
        {
            await _observationsContainer.DeleteItemAsync<Observation>(id, new PartitionKey(id));

            // Also delete all associated notes
            var notes = await GetNotesByObservationIdAsync(id);
            foreach (var note in notes)
            {
                await DeleteNoteAsync(note.Id, id);
            }

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    #endregion

    #region Note Operations

    public async Task<IEnumerable<Note>> GetNotesByObservationIdAsync(string observationId)
    {
        // Remove ORDER BY with nullable field - sort in memory instead to avoid indexing issues
        var query = "SELECT * FROM c WHERE c.type = 'note' AND c.observationId = @observationId";
        var queryDefinition = new QueryDefinition(query).WithParameter(
            "@observationId",
            observationId
        );

        var resultSet = _notesContainer.GetItemQueryIterator<Note>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(observationId),
            }
        );

        var notes = new List<Note>();
        while (resultSet.HasMoreResults)
        {
            var response = await resultSet.ReadNextAsync();
            notes.AddRange(response.ToList());
        }

        // Sort in memory: timestamped notes first (by offset), then general notes (by created time)
        return notes.OrderBy(n => n.OffsetSeconds ?? int.MaxValue).ThenBy(n => n.CreatedAt);
    }

    public async Task<Note?> GetNoteByIdAsync(string id, string observationId)
    {
        try
        {
            var response = await _notesContainer.ReadItemAsync<Note>(
                id,
                new PartitionKey(observationId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Note> CreateNoteAsync(
        Note note,
        string? createdByAuthUserId = null,
        string? createdByName = null
    )
    {
        if (string.IsNullOrEmpty(note.Id))
        {
            note.Id = $"note_{Guid.NewGuid():N}";
        }

        note.Type = "note";
        note.CreatedAt = DateTime.UtcNow;
        note.UpdatedAt = DateTime.UtcNow;
        note.CreatedByAuthUserId = createdByAuthUserId;
        note.CreatedByName = createdByName;

        var response = await _notesContainer.CreateItemAsync(
            note,
            new PartitionKey(note.ObservationId)
        );
        return response.Resource;
    }

    public async Task<Note?> UpdateNoteAsync(string id, string observationId, Note note)
    {
        try
        {
            note.Id = id;
            note.ObservationId = observationId;
            note.Type = "note";
            note.UpdatedAt = DateTime.UtcNow;

            // Preserve createdAt
            if (note.CreatedAt == default)
            {
                var existing = await GetNoteByIdAsync(id, observationId);
                if (existing != null)
                {
                    note.CreatedAt = existing.CreatedAt;
                }
                else
                {
                    note.CreatedAt = DateTime.UtcNow;
                }
            }

            var response = await _notesContainer.ReplaceItemAsync(
                note,
                id,
                new PartitionKey(observationId)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteNoteAsync(string id, string observationId)
    {
        try
        {
            await _notesContainer.DeleteItemAsync<Note>(id, new PartitionKey(observationId));
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    #endregion
}
