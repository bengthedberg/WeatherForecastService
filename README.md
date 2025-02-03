# Weather Forecast Example with LocalStack

This AWS solution uses LocalStack to develop and test locally.

[Localstack](https://docs.localstack.cloud/overview/)

## Prerequistes

- [Docker](https://docs.docker.com/get-started/introduction/get-docker-desktop/)
- [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)

## LocalStack CLI

[more](https://docs.localstack.cloud/getting-started/installation/)

### Windows

[installation](https://github.com/localstack/localstack-cli/releases/download/v4.1.0/localstack-cli-4.1.0-windows-amd64-onefile.zip)

### Mac

`brew install localstack/tap/localstack-cli`

## AWS CLI

[more](https://docs.localstack.cloud/user-guide/integrations/aws-cli/)

You can configure a custom profile to use with LocalStack. Add the following profile to your AWS configuration file (by default, this file is at `~/.aws/config`):

```
[profile localstack]
region=us-east-1
output=json
endpoint_url = http://localhost:4566
```

Add the following profile to your AWS credentials file (by default, this file is at `~/.aws/credentials`):

```shell
[localstack]
aws_access_key_id=test
aws_secret_access_key=test
```

You can now use the localstack profile with the aws CLI:

```shell
aws s3 mb s3://test --profile localstack
aws s3 ls --profile localstack
```

## Manual Setup

### Create DynamoDB table

```shell
aws --profile localstack dynamodb create-table \
    --table-name WeatherForecastData \
    --attribute-definitions \
        AttributeName=City,AttributeType=S \
        AttributeName=Date,AttributeType=S \
    --key-schema AttributeName=City,KeyType=HASH AttributeName=Date,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST \
    --table-class STANDARD
```

### Create SQS

```shell
aws --profile localstack sqs create-queue --queue-name weather-data
```

### Build and Deploy WeatherForecastService

```shell
cd .\WeatherForecast.Service

dotnet lambda package

aws --profile localstack lambda create-function `
  --function-name weatherforecast-service `
  --runtime dotnet8 `
  --zip-file "fileb://bin/Release/net8.0/WeatherForecast.Service.zip" `
  --handle "WeatherForecast.Service::WeatherForecast.Service.Function::FunctionHandler" `
  --role arn:aws:iam::000000000000:role/lambda-role

aws --profile localstack lambda create-event-source-mapping `
  --function-name weatherforecast-service `
  --batch-size 10 `
  --event-source-arn arn:aws:sqs:ap-southeast-2:000000000000:weather-data
```

### Useful Commands

**Tail Lambda log**

- Tail Service Lambda:  
  `aws --profile localstack logs tail /aws/lambda/weatherforecast-service --follow`
- List Service Log Entries in last 30 min:  
  ` aws --profile localstack logs tail /aws/lambda/weatherforecast-service --since 30m`

**Query DynamoDb Table**

- Get all Items:  
  `aws --profile localstack dynamodb scan --table-name WeatherForecastData`

- Get Specific Item:  
  `aws --profile localstack dynamodb execute-statement --statement "SELECT * FROM WeatherForecastData WHERE City='Brisbane'"`

**Update Existing Lambda Function**

- Update service Lambda

```shell
cd .\WeatherForecast.Service
dotnet lambda package
aws --profile localstack lambda update-function-code `
  --function-name weatherforecast-service `
  --zip-file "fileb://bin/Release/net8.0/WeatherForecast.Service.zip"
```
