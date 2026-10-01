[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string]$Command,

    [Parameter(Mandatory = $false)]
    [string]$User = "postgres",

    [Parameter(Mandatory = $false)]
    [string]$Database = "app_db",

    [Parameter(Mandatory = $false)]
    [string]$Service = "postgres"
)

# Execute query through docker compose
docker compose exec -T $Service psql -U $User -d $Database -c $Command