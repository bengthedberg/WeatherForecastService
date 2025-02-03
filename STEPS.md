# Steps

Following are the steps to recreate this solution:

## Create Solution

```shell
 mkdir WeatherForecastService
 cd .\WeatherForecastService\
 dotnet new gitignore
 dotnet new editorconfig
 git init --initial-branch=main
 dotnet new sln
```

## Add Shared Contract Library

```shell
dotnet new classlib -n WeatherForecast.Contracts
dotnet add .\WeatherForecast.Service\ reference .\WeatherForecast.Contracts\
dotnet add .\WeatherForecast.API\ reference .\WeatherForecast.Contracts\
dotnet sln add (ls -r **/*.csproj)
rm .\WeatherForecast.Contracts\Class1.cs
```

Create the file `.\WeatherForecast.Contracts\WeatherForecastCreatedEvent.cs`:

```csharp
# .\WeatherForecast.Contracts\WeatherForecastCreatedEvent.cs
namespace WeatherForecast.Contracts;

public class WeatherForecastCreatedEvent
{
	public string City { get; set; } = string.Empty;
	public DateTime Date { get; set; } = DateTime.Today;
	public int TemperatureC { get; set; }
	public string? Summary { get; set; }
}
```

## Add Lambda API

```shell
dotnet new webapi -n WeatherForecast.API
dotnet add .\WeatherForecast.API\ package AWSSDK.Extensions.NETCore.Setup
dotnet add .\WeatherForecast.API\ package AWSSDK.DynamoDBv2
dotnet add .\WeatherForecast.API\ package AWSSDK.SQS
dotnet add .\WeatherForecast.API\ package Amazon.Lambda.AspNetCoreServer.Hosting
dotnet add .\WeatherForecast.API\ reference .\WeatherForecast.Contracts\
```

Update the file `.\WeatherForecast.API\Properties\launchSettings.json`:

```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "http://localhost:7001",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Update the file `.\WeatherForecast.API\appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AWS": {
    "Profile": "localstack",
    "Region": "ap-southeast-2",
    "ServiceURL": "http://localhost:4566",
    "AuthenticationRegion": "ap-southeast-2"
  }
}
```

Update the file `.\WeatherForecast.API\appsettings.LocalStack.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AWS": {
    "Profile": "localstack",
    "Region": "ap-southeast-2",
    "ServiceURL": "http://host.docker.internal:4566",
    "AuthenticationRegion": "ap-southeast-2"
  }
}
```

Update the file `.\WeatherForecast.API\appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AWS": {
    "Profile": "default",
    "Region": "ap-southeast-2"
  }
}
```

Update the file `.\WeatherForecast.API\Program.cs`:

```csharp
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
```

Update the file `.\WeatherForecast.API\WeatherForecast.API.http`:

```http
@WeatherForecast.API_HostAddress = http://localhost:7001

POST {{WeatherForecast.API_HostAddress}}/weatherforecast/
Content-Type: application/json

{
  "city": "Brisbane",
  "date": "2025-01-01T00:00:00.000Z",
  "temperatureC": 32,
  "summary": "Hot and sunny"
}

###

POST http://cqedii08zo404nrukzl8zh1zf1lf1g0w.lambda-url.ap-southeast-2.localhost.localstack.cloud:4566/weatherforecast
Content-Type: application/json

{
  "city": "Brisbane",
  "date": "2025-01-02T00:00:00.000Z",
  "temperatureC": 30,
  "summary": "Hot and sunny"
}
```

Update the file `.\WeatherForecast.API\WeatherForecastData.cs`:

```csharp
namespace WeatherForecast.API;

public class WeatherForecastData
{
    public string City { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }

}
```

## Add Lambda Worker

```shell
 dotnet new lambda.SQS -n WeatherForecast.Service
 rm -R .\WeatherForecast.Service\test
 mv .\WeatherForecast.Service\src\WeatherForecast.Service .\x
 rm -R .\WeatherForecast.Service
 mv .\x .\WeatherForecast.Service
 dotnet add .\WeatherForecast.Service\ reference .\WeatherForecast.Contracts\

 dotnet sln add (ls -r **/*.csproj)
```

Update the file `.\WeatherForecast.Service\aws-lambda-tools-defaults.json`:

```json
{
  "Information": [
    "This file provides default values for the deployment wizard inside Visual Studio and the AWS Lambda commands added to the .NET Core CLI.",
    "To learn more about the Lambda commands with the .NET Core CLI execute the following command at the command line in the project root directory.",
    "dotnet lambda help",
    "All the command line options for the Lambda command can be specified in this file."
  ],
  "profile": "default",
  "region": "ap-southeast-2",
  "configuration": "Release",
  "function-runtime": "dotnet8",
  "function-memory-size": 512,
  "function-timeout": 30,
  "function-handler": "WeatherForecast.Service::WeatherForecast.Service.Function::FunctionHandler"
}
```

Update the file `.\WeatherForecast.Service\Function.cs`:

```csharp
using System.Text.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

using WeatherForecast.Contracts;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WeatherForecast.Service;

public class Function
{
    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {

    }


    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        foreach (var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        var weatherForecastCreatedEvent = JsonSerializer.Deserialize<WeatherForecastCreatedEvent>(message.Body);
        if (weatherForecastCreatedEvent is not null)
        {
            context.Logger.LogInformation(
                $"Processed weather forecast event for {weatherForecastCreatedEvent.City} at {weatherForecastCreatedEvent.Date.ToString("yyyy-MM-dd")} : {weatherForecastCreatedEvent.TemperatureC} degrees, {weatherForecastCreatedEvent.Summary}");
        }
        await Task.CompletedTask;
    }
}
```

## Add CDK Project

```shell
 mkdir WeatherForecast.CDK
 cd WeatherForecast.CDK
 cdk init --language csharp
 cd ..

 mv WeatherForecast.CDK/.gitignore . -force
 mv WeatherForecast.CDK/cdk.json .
 mv ./WeatherForecast.CDK/src/WeatherForecastCdk/*.* ./WeatherForecast.CDK/
 rm -R ./WeatherForecast.CDK/src
 ren ./WeatherForecast.CDK/WeatherForecastCdk.csproj WeatherForecast.CDK.csproj
 ren ./WeatherForecast.CDK/WeatherForecastCdkStack.cs ApplicationStack.cs
 Get-ChildItem -Recurse -include cdk.json | ForEach-Object { if (!($_.PSIsContainer)) { (Get-Content $_.PSPath | ForEach {$_ -creplace "WeatherForecastCdk", "WeatherForecast.CDK"}) | Set-Content $_.PSPath }}
 Get-ChildItem -Recurse -include *.cs | ForEach-Object { if (!($_.PSIsContainer)) { (Get-Content $_.PSPath | ForEach {$_ -creplace "WeatherForecastCdkStack", "ApplicationStack"}) | Set-Content $_.PSPath }}
 Get-ChildItem -Recurse -include *.cs | ForEach-Object { if (!($_.PSIsContainer)) { (Get-Content $_.PSPath | ForEach {$_ -creplace "namespace WeatherForecastCdk", "namespace WeatherForecast.CDK"}) | Set-Content $_.PSPath }}
 dotnet sln add (ls -r **/*.csproj)
```

Update the file `.\WeatherForecast.CDK\ApplicationStack.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;

using Constructs;

namespace WeatherForecast.CDK
{
    public class ApplicationStack : Stack
    {
        internal ApplicationStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // DynamoDB table
            var table = new Table(this, "WeatherForecastData", new TableProps
            {
                TableName = "WeatherForecastData",
                PartitionKey = new Attribute
                {
                    Name = "City",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                    Name = "Date",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            // SQS queue
            var queue = new Queue(this, "WeatherForecastQueue", new QueueProps
            {
                QueueName = "weather-data",
                VisibilityTimeout = Duration.Seconds(30)
            });

            // Lambda Function to Process SQS Messages
            var weatherLambdaFunction = new Function(this, "WeatherDataService", new FunctionProps
            {
                FunctionName = "weatherforecast-service",
                Runtime = Runtime.DOTNET_8,
                Handler = "WeatherForecast.Service::WeatherForecast.Service.Function::FunctionHandler",
                Code = Code.FromAsset("./artifacts/WeatherForecast.Service.zip"),
                Timeout = Duration.Seconds(30),
                MemorySize = 512,
                Environment = new Dictionary<string, string>
                {
                    {"DYNAMODB_TABLE_NAME", table.TableName},
                    {"SQS_QUEUE_URL", queue.QueueUrl}
                }
            });
            // Grant the Lambda Function read/write permissions to the DynamoDB table
            table.GrantReadWriteData(weatherLambdaFunction);
            // Add SQS Event Source to Lambda Function
            weatherLambdaFunction.AddEventSource(new SqsEventSource(queue));

            // Lambda Function to Host API
            var environmentVariables = new Dictionary<string, string>
                {
                    {"DYNAMODB_TABLE_NAME", table.TableName},
                    {"SQS_QUEUE_URL", queue.QueueUrl}
                };
            var environment = this.Node.TryGetContext("ASPNETCORE_ENVIRONMENT").ToString();
            if (!string.IsNullOrEmpty(environment))
            {
                Console.WriteLine($"Setting ASPNETCORE_ENVIRONMENT to {environment}");
                environmentVariables.Add("ASPNETCORE_ENVIRONMENT", environment);
            }
            var apiLambdaFunction = new Function(this, "WeatherDataApi", new FunctionProps
            {
                FunctionName = "weatherforecast-api",
                Runtime = Runtime.DOTNET_8,
                Handler = "WeatherForecast.API",
                Code = Code.FromAsset("./artifacts/WeatherForecast.API.zip"),
                Timeout = Duration.Seconds(30),
                MemorySize = 512,
                Environment = environmentVariables
            });
            // Grant the Lambda Function read/write permissions to the DynamoDB table
            table.GrantReadWriteData(apiLambdaFunction);
            // Grant the Lambda Function send message permissions to the SQS queue
            queue.GrantSendMessages(apiLambdaFunction);
            // Add a Function URL to the Lambda
            var functionUrl = apiLambdaFunction.AddFunctionUrl(new FunctionUrlOptions
            {
                AuthType = FunctionUrlAuthType.NONE
            });

            // Output the Function URL
            new CfnOutput(this, "WeatherDataApiUrl", new CfnOutputProps
            {
                Value = functionUrl.Url
            });
        }
    }
}
```

Create `bootstrap.ps1':

```ps1
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location
Set-Location (get-item $scriptRoot ).'\WeatherForecast.CDK'
cdklocal --profile localstack bootstrap aws://000000000000/ap-southeast-2
Pop-Location
```

Create `build.ps1':

```ps1
dotnet lambda package --project-location .\WeatherForecast.API\ `
    --configuration Release `
    --framework net8.0 `
    --output-package artifacts\WeatherForecast.API.zip

dotnet lambda package --project-location .\WeatherForecast.Service\ `
    --configuration Release `
    --framework net8.0 `
    --output-package artifacts\WeatherForecast.Service.zip
```

Create `deploy.ps1':

```ps1
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location
Set-Location (get-item $scriptRoot)
cdklocal --profile localstack deploy -c ASPNETCORE_ENVIRONMENT=LocalStack
Pop-Location
```

Bootstrap the LocalStack  
`.\bootstrap.ps1`

Build  
`.\build.ps1`

Deploy  
`.\deploy.ps1`
