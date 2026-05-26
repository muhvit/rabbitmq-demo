param(
    [Parameter(Mandatory = $true)]
    [string]$ClusterName,

    [Parameter(Mandatory = $true)]
    [string]$ConfigPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments

    $exitCode = if (Get-Variable LASTEXITCODE -ErrorAction SilentlyContinue) {
        $LASTEXITCODE
    }
    else {
        0
    }

    if ($exitCode -ne 0) {
        throw "Command failed with exit code ${exitCode}: $FilePath $($Arguments -join ' ')"
    }
}

$clusterExists = $false

$clusterListJson = & k3d cluster list -o json 2>$null
$clusterList = @()

if ($clusterListJson) {
    $clusterList = $clusterListJson | ConvertFrom-Json
}

$clusterExists = @($clusterList | ForEach-Object { $_.name }) -contains $ClusterName

if ($clusterExists) {
    Write-Host "Cluster '$ClusterName' already exists. Ensuring it is running."
    & k3d cluster start $ClusterName

    $startExitCode = if (Get-Variable LASTEXITCODE -ErrorAction SilentlyContinue) {
        $LASTEXITCODE
    }
    else {
        0
    }

    if ($startExitCode -ne 0) {
        Write-Warning "Existing cluster '$ClusterName' could not be started. Recreating it."
        Invoke-NativeCommand k3d cluster delete $ClusterName
        Invoke-NativeCommand k3d cluster create --config $ConfigPath
    }
}
else {
    Write-Host "Creating cluster '$ClusterName' from $ConfigPath"
    Invoke-NativeCommand k3d cluster create --config $ConfigPath
}

Invoke-NativeCommand kubectl config use-context "k3d-$ClusterName"
