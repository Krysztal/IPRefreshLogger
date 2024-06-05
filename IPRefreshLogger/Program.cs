using IPRefreshLogger;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetRequiredSection("Settings"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
