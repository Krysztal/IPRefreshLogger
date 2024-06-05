using Microsoft.Extensions.Options;
using SMBLibrary;
using SMBLibrary.Client;
using System.Text;

namespace IPRefreshLogger;

public class Worker : BackgroundService
{
    private const string GetIPUrl = "https://api.ipify.org";
    private readonly ILogger<Worker> _logger;
    private readonly ApplicationSettings _settings;

    public Worker(ILogger<Worker> logger, IOptions<ApplicationSettings> options)
    {
        _logger = logger;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var httpClient = new HttpClient(CreateHandler());
                using var memoryStream = new MemoryStream();

                var ip = await httpClient.GetStringAsync(GetIPUrl, stoppingToken);
                byte[] bytes = Encoding.UTF8.GetBytes(ip);
                memoryStream.Write(bytes, 0, bytes.Length);
                memoryStream.Position = 0;

                Console.WriteLine(ip);
                UploadFileToSMB(_settings.SMBServerName, _settings.SMBShare, _settings.FilePath, bytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString(), ex);
            }

            await Task.Delay(TimeSpan.FromMinutes(_settings.UpdateInterval), stoppingToken);
        }
    }

    SocketsHttpHandler CreateHandler()
    {
        if (_settings.IgnoreCertificateValidation)
        {
            return new SocketsHttpHandler();
        }
        else
        {
            return new SocketsHttpHandler
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                }
            };
        }
    }

    void UploadFileToSMB(string serverName, string shareName, string remoteFilePath, byte[] ip)
    {
        SMB2Client client = new SMB2Client();
        bool isConnected = client.Connect(serverName, SMBTransportType.DirectTCPTransport);

        if (isConnected)
        {
            NTStatus loginStatus = client.Login(string.Empty, _settings.SMBLogin, _settings.SMBPassword);

            if (loginStatus == NTStatus.STATUS_SUCCESS)
            {
                var fileStore = client.TreeConnect(shareName, out NTStatus status);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    status = fileStore.CreateFile(out object fileHandle, out _, remoteFilePath, AccessMask.GENERIC_WRITE, SMBLibrary.FileAttributes.Normal, ShareAccess.Write, CreateDisposition.FILE_OVERWRITE_IF, CreateOptions.FILE_NON_DIRECTORY_FILE, null);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        status = fileStore.WriteFile(out _, fileHandle, 0, ip);

                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            _logger.LogInformation("IP saved.");
                        }
                        else
                        {
                            _logger.LogError($"Failed to write to the file: {status}");
                        }

                        fileStore.CloseFile(fileHandle);
                    }
                    else
                    {
                        _logger.LogError($"Failed to create the file: {status}");
                    }

                    fileStore.Disconnect();
                }

                client.Logoff();
            }
            else
            {
                _logger.LogError($"Failed to login: {loginStatus}");
            }

            client.Disconnect();
        }
        else
        {
            _logger.LogError("Failed to connect to the server.");
        }
    }
}
