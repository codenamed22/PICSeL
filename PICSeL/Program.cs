using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using PICSeL.Configs;
using PICSeL.Utils;
using System.Configuration;

var AllowAllOriginsPolicy = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var azureServiceTokenProvider = new DefaultAzureCredential();
SecretClient kvConnector = new SecretClient(builder.Configuration.GetValue<Uri>($"KeyVault"), new DefaultAzureCredential());

builder.Configuration["OpenAIConfig:ApiKey"] = kvConnector.GetSecretAsync($"OpenAIConfig--ApiKey").GetAwaiter().GetResult().Value.Value;
builder.Configuration["OpenAIConfig:ApiKeyBackup"] = kvConnector.GetSecretAsync($"OpenAIConfig--ApiKeyBackup").GetAwaiter().GetResult().Value.Value;
builder.Configuration["AzureAvatarConfig:SubscriptionKey"] = kvConnector.GetSecretAsync($"AzureAvatarConfig--SubscriptionKey").GetAwaiter().GetResult().Value.Value;

builder.Services.Configure<OpenAIConfig>(builder.Configuration.GetSection("OpenAIConfig"));
builder.Services.Configure<AzureAvatarConfig>(builder.Configuration.GetSection("AzureAvatarConfig"));

builder.Services.AddSingleton<AzureAIHelper>();
builder.Services.AddSingleton<AzureSpeechHelper>();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        name: AllowAllOriginsPolicy,
        builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.UseCors(AllowAllOriginsPolicy);

app.Run();
