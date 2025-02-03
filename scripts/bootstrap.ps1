$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location
Set-Location (get-item $scriptRoot ).parent.'\WeatherForecast.CDK'
cdklocal --profile localstack bootstrap aws://000000000000/ap-southeast-2
Pop-Location