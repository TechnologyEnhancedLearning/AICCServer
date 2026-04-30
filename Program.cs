using AICCServer.Models;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.Configure<AICCSettings>(builder.Configuration.GetSection("AICCSettings"));

var allowedOrigin = builder.Configuration["AICCSettings:BaseUrl"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowContentServer", policy =>
    {
        policy.WithOrigins(allowedOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowContentServer");
app.UseAuthorization();

app.MapControllers();

var options = new DefaultFilesOptions();
options.DefaultFileNames.Clear();
options.DefaultFileNames.Add("hubrouter.html");
options.DefaultFileNames.Add("metarouter.htlm");
options.DefaultFileNames.Add("protocolrouter.html");

app.UseDefaultFiles(options);

app.UseStaticFiles();

app.Run();
