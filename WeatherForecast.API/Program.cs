using System.Globalization;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SQS;
using Amazon.SQS.Model;

using WeatherForecast.API;
using WeatherForecast.Contracts;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string weatherDataQueue = "weather-data";
//string localStackUrl = "http://localhost:4566";
//string localStackRegion = "ap-southeast-2";


// Override the default AWS Options by using localstack
// if (builder.Environment.IsDevelopment())
// {
//     var awsOptions = builder.Configuration.GetAWSOptions();
//     awsOptions.DefaultClientConfig.ServiceURL = localStackUrl;
//     awsOptions.DefaultClientConfig.AuthenticationRegion = localStackRegion;
//     builder.Services.AddDefaultAWSOptions(awsOptions); 
// }

/* Explicitly specify on each client
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
        new AmazonDynamoDBClient(new AmazonDynamoDBConfig()
        {
            ServiceURL = "http://localhost:4566",
            AuthenticationRegion = "ap-southeast-2"
        }));
}
else
{
    builder.Services.AddAWSService<IAmazonDynamoDB>();
}
*/

// Add AWS Lambda support. When application is run in Lambda Kestrel is swapped out as the web server with Amazon.Lambda.AspNetCoreServer. This
// package will act as the webserver translating request and responses between the Lambda event source and ASP.NET Core.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddSingleton<IDynamoDBContext, DynamoDBContext>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/weatherforecast",
        async (WeatherForecastData data, IDynamoDBContext dynamoDbContext, IAmazonSQS publisher) =>
        {
            await dynamoDbContext.SaveAsync(data);
            await publisher.SendMessageAsync(
                new SendMessageRequest(weatherDataQueue,
                    JsonSerializer.Serialize(
                        new WeatherForecastCreatedEvent()
                        {
                            City = data.City,
                            Date = data.Date,
                            TemperatureC = data.TemperatureC,
                            Summary = data.Summary
                        }
                        )));
            return Results.Created($"/weatherforecast/{data.City}/{data.Date.ToString("yyyyMMdd")}", data);
        })
    .WithName("PostWeatherForecast")
    .DisableAntiforgery()
    .WithOpenApi();

app.Run();
