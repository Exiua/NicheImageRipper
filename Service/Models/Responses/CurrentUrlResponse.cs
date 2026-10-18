namespace NicheImageRipper.Service.Models.Responses;

/// <param name="Url">URL currently being ripped, or null when idle</param>
public record CurrentUrlResponse(string? Url);
