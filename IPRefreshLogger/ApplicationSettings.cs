namespace IPRefreshLogger;
public class ApplicationSettings
{
    public string FilePath { get; set; } = "IP.txt";

    public bool IgnoreCertificateValidation { get; set; } = false;

    public required string SMBServerName { get; set; }

    public required string SMBShare { get; set; }

    public required string SMBLogin { get; set; }

    public required string SMBPassword { get; set; }

    public required int UpdateInterval { get; set; } = 10; //min
}
