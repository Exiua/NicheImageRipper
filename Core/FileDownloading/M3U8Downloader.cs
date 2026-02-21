using Core.Configuration;
using Core.Exceptions;
using Core.ExtensionMethods;
using Serilog;

namespace Core.FileDownloading;

public static class M3U8Downloader
{
    private static GeneralConfig Config => Configuration.Config.Instance;

    public static async Task DownloadM3U8(string url, string savePath, string outputName,
                                          string? referer = null, bool isIndex = false)
    {
        Log.Debug("Url: {Url}, SavePath: {SavePath}, OutputName: {OutputName}, Referer: {Referer}, IsIndex: {IsIndex}",
            url, savePath, outputName, referer, isIndex);
        string? tempPath = null;
        #if DEBUG
        var success = true;
        #endif
        try
        {
            var path = Path.GetFullPath(savePath);
            tempPath = Path.Combine(path, "temp");
            Directory.CreateDirectory(tempPath);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
            Log.Debug("HttpClient initialized with User-Agent: {UserAgent}", Config.UserAgent);
            if (referer is not null)
            {
                client.DefaultRequestHeaders.Add("Referer", referer);
                var origin = referer.EndsWith('/') ? referer[..^1] : referer;
                client.DefaultRequestHeaders.Add("Origin", origin);
            }

            var playlistUrl = await GetPlaylistUrl(url, client);
            var segments = await DownloadPlaylist(playlistUrl, client);
            var segmentPaths = await DownloadSegments(segments, tempPath, client);
            var outputPath = Path.Combine(savePath, outputName);
            await ConcatenateSegments(segmentPaths, tempPath, outputPath);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error downloading M3U8 segments");
            #if DEBUG
            success = false;
            #endif
            throw;
        }
        finally
        {
            if (tempPath is not null)
            {
                #if DEBUG // Keep temp files on failure for debugging
                if (success)
                {
                    Directory.Delete(tempPath, true);
                }
                #else
                Directory.Delete(tempPath, true);
                #endif
            }

            Log.Debug("Temporary files cleaned up.");
        }
    }

    private static async Task ConcatenateSegments(List<string> segmentPaths, string tempPath, string outputPath)
    {
        var pathEntries = new List<string>(segmentPaths.Count);
        pathEntries.AddRange(segmentPaths.Select(path => $"file '{path.Replace("'", "'\\''")}'"));

        var listPath = Path.Combine(tempPath, "segments.txt");
        await File.WriteAllLinesAsync(listPath, pathEntries);
        var cmd = new[]
        {
            "-f", "concat",
            "-safe", "0",
            "-i", $"\"{listPath}\"",
            "-c", "copy",
            $"\"{outputPath}\""
        };
        await ImageRipper.RunFfmpeg(cmd, startMessage: "Starting ffmpeg concatenation",
            endMessage: "Finished ffmpeg concatenation");

        Log.Debug("Output saved to: {OutputPath}", outputPath);
    }

    private static async Task<List<string>> DownloadSegments(List<string> segments, string savePath,
                                                             HttpClient client)
    {
        var segmentPaths = new List<string>();
        var segmentCount = 0;
        foreach (var segment in segments)
        {
            Log.Debug("Downloading segment URL: {SegmentUrl}", segment);
            var segmentResponse = await client.GetAsync(segment);
            segmentResponse.EnsureSuccessStatusCode();
            var data = await segmentResponse.Content.ReadAsByteArrayAsync();
            byte[] segmentData;
            try
            {
                segmentData = ExtractTs(data);
            }
            catch (RipperException e)
            {
                Log.Error(e, "Failed to extract segment data");
                continue;
            }

            var segmentPath = Path.Combine(savePath, $"segment_{segmentCount:D5}.ts");
            Log.Debug("Saving segment {SegmentPath}", segmentPath);
            segmentCount++;
            await File.WriteAllBytesAsync(segmentPath, segmentData);
            segmentPaths.Add(segmentPath);
        }

        return segmentPaths;
    }

    private static async Task<List<string>> DownloadPlaylist(string url, HttpClient client)
    {
        Log.Debug("Downloading playlist URL: {URL}", url);
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var lines = content.Split('\n');

        return (lines.Where(line => !line.StartsWith('#') && !string.IsNullOrWhiteSpace(line))
                     .Select(line => ResolveUrl(url, line.Trim()))).ToList();
    }

    private static async Task<string> GetPlaylistUrl(string url, HttpClient client)
    {
        Log.Debug("Downloading M3U8 playlist from: {Url}", url);
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var playlistUrl = GetHighestQualityVideoPlaylist(content);
        if (playlistUrl is null)
        {
            throw new InvalidOperationException("No video playlist found in M3U8 content");
        }

        var resolvedUrl = ResolveUrl(url, playlistUrl);
        Log.Debug("Resolved playlist URL: {ResolvedUrl}", resolvedUrl);
        return resolvedUrl;
    }

    private static string ResolveUrl(string baseUrl, string reference)
    {
        return new Uri(new Uri(baseUrl), reference).ToString();
    }

    private static string? GetHighestQualityVideoPlaylist(string playlist)
    {
        var lines = playlist
                   .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                   .Select(l => l.Trim())
                   .ToArray();

        string? bestUri = null;
        long bestScore = 0;

        for (var i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i];

            if (!line.StartsWith("#EXT-X-STREAM-INF"))
            {
                continue;
            }

            long bandwidth = 0;
            long pixels = 0;

            foreach (var part in line.Split(','))
            {
                if (part.StartsWith("BANDWIDTH="))
                {
                    if (long.TryParse(part["BANDWIDTH=".Length..], out var bw))
                    {
                        bandwidth = bw;
                    }
                }
                else if (part.StartsWith("RESOLUTION="))
                {
                    var r = part["RESOLUTION=".Length..].Split('x');
                    if (r.Length == 2 &&
                        long.TryParse(r[0], out var w) &&
                        long.TryParse(r[1], out var h))
                    {
                        pixels = w * h;
                    }
                }
            }

            // prefer resolution; fallback to bandwidth
            var score = pixels > 0 ? pixels : bandwidth;

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;

            // next NON-tag line is URI
            for (var j = i + 1; j < lines.Length; j++)
            {
                if (lines[j].StartsWith('#'))
                {
                    continue;
                }

                bestUri = lines[j];
                break;
            }
        }

        return bestUri;
    }

    private static byte[] ExtractTs(byte[] segment)
    {
        var index = Array.IndexOf(segment, (byte)0x47);
        if (index == -1)
        {
            throw new RipperException("No TS packet found");
        }

        var slice = new Memory<byte>(segment, index, segment.Length - index);
        while (slice.Length >= 189)
        {
            if (slice.Chunks(188).All(c => c.Span[0] == 0x47))
            {
                break;
            }

            var tempSlice = new Memory<byte>(segment, index + 1, segment.Length - index - 1);
            var tempIndex = tempSlice.IndexOf((byte)0x47);
            if (tempIndex == -1)
            {
                throw new RipperException("No TS packet found");
            }

            index += tempIndex + 1;
            slice = tempSlice[index..];
        }

        return slice.ToArray();
    }
}