using Core.Exceptions;
using Core.ExtensionMethods;
using Serilog;

namespace Core.FileDownloading;

public static class M3U8Downloader
{
    public static async Task DownloadObfuscatedM3U8(string url, string savePath, string outputName,
                                                    string? referer = null, bool isIndex = false)
    {
        var path = Path.GetFullPath(savePath);
        var temp = Path.Combine(path, "temp");
        Directory.CreateDirectory(temp);
        
        var client = new HttpClient();
        if (referer is not null)
        {
            client.DefaultRequestHeaders.Add("Referer", referer);
            var origin = referer.EndsWith('/') ? referer[..^1] : referer;
            client.DefaultRequestHeaders.Add("Origin", origin);
        }

        var shortBaseUrl = url.Split("/").Take(3).Join("/");
        var qs = url.IndexOf('?');
        var queryStart = qs == -1 ? url.Length - 1 : qs;
        Log.Debug("QueryStart: {QueryStart}, Length: {Length}", queryStart, url.Length);
        var longBaseUrl = url[..(url.LastIndexOf('/', queryStart) + 1)];
        HttpResponseMessage response;
        string content;
        string? segment;
        if (!isIndex)
        {
            Log.Debug("Downloading M3U8 playlist from: {Url}", url);
            response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            content = await response.Content.ReadAsStringAsync();
            segment = GetHighestQualitySegment(content);
            if (segment == null)
            {
                throw new RipperException("No segment found");
            }
            
            if (!segment.StartsWith("http"))
            {

                segment = longBaseUrl + segment;
            }
        }
        else
        {
            segment = url;
        }

        Log.Debug("Highest Quality Segment: {Segment}", segment);
        response = await client.GetAsync(segment);
        response.EnsureSuccessStatusCode();
        content = await response.Content.ReadAsStringAsync();
        var segmentsDest = Path.Combine(temp, "segments");
        Directory.CreateDirectory(segmentsDest);
        var lines = content.Split('\n');
        var segCount = 0;
        var pull = false;
        foreach (var line in lines)
        {
            Log.Debug("Processing line: {Line}", line);
            if (pull)
            {
                pull = false;
                var segmentUrl = line.Trim();
                if (!segmentUrl.StartsWith("http"))
                {
                    qs = segmentUrl.IndexOf('?');
                    queryStart = qs == -1 ? segmentUrl.Length - 1 : qs;
                    if (segmentUrl.IndexOf('/', 0, queryStart) != -1)
                    {
                        segmentUrl = shortBaseUrl + segmentUrl;
                        Log.Debug("Using short base URL for segment: {SegmentUrl}", segmentUrl);
                    }
                    else
                    {
                        segmentUrl = longBaseUrl + segmentUrl;
                        Log.Debug("Using long base URL for segment: {SegmentUrl}", segmentUrl);
                    }
                }
                Log.Debug("Downloading segment: {SegmentUrl}", segmentUrl);
                var res = await client.GetAsync(segmentUrl);
                if (!res.IsSuccessStatusCode)
                {
                    Log.Warning("Failed to download segment: {SegmentUrl} with status code {StatusCode}", segmentUrl, res.StatusCode);
                    continue;
                }
                
                var data = await res.Content.ReadAsByteArrayAsync();
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
                var segmentPath = Path.Combine(segmentsDest, $"segment_{segCount:D5}.ts");
                await File.WriteAllBytesAsync(segmentPath, segmentData);
                segCount++;
            }
            else if (line.StartsWith("#EXTINF"))
            {
                pull = true;
            }
        }

        Log.Debug("Downloaded {SegCount} segments", segCount);
        var pathEntries = new List<string>(segCount);
        for(var i = 0; i < segCount; i++)
        {
            var segmentPath = Path.Combine(segmentsDest, $"segment_{i:D5}.ts");
            pathEntries.Add($"file '{segmentPath.Replace("'", "'\\''")}'");
        }
        
        var listPath = Path.Combine(temp, "segments.txt");
        await File.WriteAllLinesAsync(listPath, pathEntries);
        var outputPath = Path.Combine(path, outputName);
        var cmd = new[] 
        {
            "-f", "concat", 
            "-safe", "0", 
            "-i", $"\"{listPath}\"", 
            "-c", "copy",
            $"\"{outputPath}\""
            
        };
        await ImageRipper.RunFfmpeg(cmd, startMessage: "Starting ffmpeg concatenation", endMessage: "Finished ffmpeg concatenation");

        Log.Debug("Output saved to: {OutputPath}", outputPath);
        
        Directory.Delete(temp, true);
        Log.Debug("Temporary files cleaned up.");
    }

    private static string? GetHighestQualitySegment(string playlist)
    {
        var lines = playlist.Split('\n');
        string? highestQuality = null;
        var highestResolution = 0;
        var store = false;
        foreach (var line in lines)
        {
            if (!line.StartsWith("#EXT-X-STREAM-INF"))
            {
                if (store)
                {
                    highestQuality = line;
                    store = false;
                }
                
                continue;
            }
            
            var parts = line.Split(',');
            foreach (var part in parts)
            {
                if (!part.StartsWith("RESOLUTION="))
                {
                    continue;
                }
                
                var resolutionPart = part.TrimStartMatches("RESOLUTION=");
                var resolution = resolutionPart.Split('x')[0];
                var resolutionValue = int.TryParse(resolution, out var res) ? res : 0;
                if (resolutionValue > highestResolution)
                {
                    highestResolution = resolutionValue;
                    store = true;
                }
            }
        }
        
        return highestQuality;
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

            var tempSlice = new Memory<byte>(segment, index+1, segment.Length - index - 1);
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