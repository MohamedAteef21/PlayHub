# Reliable send from PowerShell (avoids curl.exe JSON quoting issues)
# Usage: .\send.ps1 -SessionId "YOUR-UUID" -Number "201064553646" -Message "hello"

param(
    [Parameter(Mandatory = $true)][string]$SessionId,
    [Parameter(Mandatory = $true)][string]$Number,
    [Parameter(Mandatory = $true)][string]$Message,
    [string]$Url = "http://localhost:3000/send"
)

$body = @{
    sessionId = $SessionId
    number    = $Number
    message   = $Message
} | ConvertTo-Json -Compress

Invoke-RestMethod -Uri $Url -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body $body
