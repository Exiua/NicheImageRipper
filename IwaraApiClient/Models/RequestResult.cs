namespace IwaraApiClient.Models;

public enum RequestResult
{
    Success,
    LoginFailure,
    VideoPrivate,
    VideoNotFound,
    JsonNull,
    FileUrlIsNull,
    ExpirationIsMissingFromUrl,
    SourceMetadataIsNull,
    FailedToDownload
}