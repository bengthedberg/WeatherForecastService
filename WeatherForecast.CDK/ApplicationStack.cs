using System;
using System.Collections.Generic;

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;

using Constructs;

using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

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
