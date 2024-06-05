using SMBLibrary;
using SMBLibrary.Client;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace IPRefreshLogger;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;

        ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        string path = _configuration.GetRequiredSection("filePath").Value!;

        while (!stoppingToken.IsCancellationRequested)
        {
            //using (var httpClient = _httpClientFactory.CreateClient())
            using (var httpClient = new HttpClient(handler))
            {
                using (var memoryStream = new MemoryStream())
                {
                    var ip = await httpClient.GetStringAsync("https://api.ipify.org", stoppingToken);
                    byte[] bytes = Encoding.UTF8.GetBytes(ip);
                    memoryStream.Write(bytes, 0, bytes.Length);
                    memoryStream.Position = 0;

                    UploadFileToSMB("192.168.0.100", "nextnas", "ip.txt", bytes);
                }
            }

            

            await Task.Delay(10000, stoppingToken);
        }
    }

    void UploadFileToSMB(string serverName, string shareName, string remoteFilePath, byte[] ip)
    {
        SMB2Client client = new SMB2Client();
        bool isConnected = client.Connect(serverName, SMBTransportType.DirectTCPTransport);

        if (isConnected)
        {
            // Attempt anonymous login
            NTStatus loginStatus = client.Login(String.Empty, "krysztal", "1111");

            if (loginStatus == NTStatus.STATUS_SUCCESS)
            {
                NTStatus status;
                var fileStore = client.TreeConnect(shareName, out status);
                if (status == NTStatus.STATUS_SUCCESS)
                {
                    FileStatus fileStatus;
                    status = fileStore.CreateFile(out object fileHandle, out fileStatus, remoteFilePath, AccessMask.GENERIC_WRITE, SMBLibrary.FileAttributes.Normal, ShareAccess.Write, CreateDisposition.FILE_OVERWRITE_IF, CreateOptions.FILE_NON_DIRECTORY_FILE, null);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        status = fileStore.WriteFile(out int numberOfBytesWritten, fileHandle, 0, ip);

                        if (status == NTStatus.STATUS_SUCCESS)
                        {
                            Console.WriteLine("File uploaded successfully.");
                        }
                        else
                        {
                            Console.WriteLine("Failed to write to the file: " + status);
                        }

                        fileStore.CloseFile(fileHandle);
                    }
                    else
                    {
                        Console.WriteLine("Failed to create the file: " + status);
                    }

                    fileStore.Disconnect();
                }

                client.Logoff();
            }
            else
            {
                Console.WriteLine("Failed to login as anonymous: " + loginStatus);
            }

            client.Disconnect();
        }
        else
        {
            Console.WriteLine("Failed to connect to the server.");
        }
    }
}
