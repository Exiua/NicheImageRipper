using System.Diagnostics;

namespace Core;

public static class MegaApi
{
    public static bool Login(string email, string password)
    {
        string[] cmd = ["mega-login", email, $"\"{password}\""];
        
        using var process = RunSubprocess(cmd);

        var stderr = process.StandardError.ReadToEnd();
        return string.IsNullOrEmpty(stderr);
    }
    
    public static void Logout()
    {
        string[] cmd = ["mega-logout"];
        
        using var process = RunSubprocess(cmd);
    }
    
    public static void Download(string url, string dest)
    {
        string[] cmd = ["mega-get", url, dest];
        
        using var process = RunSubprocess(cmd);
    }
    
    public static string WhoAmI()
    {
        string[] cmd = ["mega-whoami"];

        using var process = RunSubprocess(cmd);

        var stdout = process.StandardOutput.ReadToEnd();
        return stdout.Split(' ')[^1].Trim();
    }

    private static Process RunSubprocess(IEnumerable<string> cmd)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {string.Join(" ", cmd)}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        process.WaitForExit();

        return process;
    }


    /*
     * def mega_login(email: str, password: str) -> bool:
    cmd = ["mega-login", email, f'"{password}"']
    out = subprocess.run(cmd, shell=True, capture_output=True)
    return out.stderr.decode() == ""


def mega_logout():
    cmd = ["mega-logout"]
    subprocess.run(cmd, shell=True)


def mega_download(url: str, dest: str):
    cmd = ["mega-get", url, dest]
    subprocess.run(cmd, shell=True)


def mega_whoami() -> str:
    cmd = ["mega-whoami"]
    out = subprocess.run(cmd, shell=True, capture_output=True)
    return out.stdout.decode().split(" ")[-1]
     */
}