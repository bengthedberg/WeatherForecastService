$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location
Set-Location (get-item $scriptRoot)
cdklocal --profile localstack deploy -c ASPNETCORE_ENVIRONMENT=LocalStack
Pop-Location