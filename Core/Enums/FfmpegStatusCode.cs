namespace Core.Enums;

public enum FfmpegStatusCode
{
    Success = 0,
    InvalidDataFound = -1094995529
}

public static class FfmpegStatusCodeExtensions
{
    public static bool IsSuccess(this FfmpegStatusCode code)
    {
        return code == FfmpegStatusCode.Success;
    }

    public static string GetShortErrorMessage(this FfmpegStatusCode code)
    {
        return code switch
        {
            FfmpegStatusCode.Success => "Success",
            FfmpegStatusCode.InvalidDataFound => "Invalid data found when processing input",
            _ => $"Unknown error: {code}"
        };
    }
}