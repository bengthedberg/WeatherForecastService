$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location
Set-Location (get-item $scriptRoot ).parent

dotnet lambda package --project-location .\WeatherForecast.API\ `
    --configuration Release `
    --framework net8.0 `
    --output-package artifacts\WeatherForecast.API.zip

dotnet lambda package --project-location .\WeatherForecast.Service\ `
    --configuration Release `
    --framework net8.0 `
    --output-package artifacts\WeatherForecast.Service.zip
    
Pop-Location