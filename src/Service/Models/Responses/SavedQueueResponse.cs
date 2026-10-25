namespace NicheImageRipper.Service.Models.Responses;

/// <param name="Path">File the queue was saved to (on the backend), or null if the queue was empty</param>
public record SavedQueueResponse(string? Path);
