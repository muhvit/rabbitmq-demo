param(
    [Parameter(Mandatory = $true)]
    [string]$ClusterName,

    [Parameter(Mandatory = $true)]
    [string]$OverlayPath,

    [Parameter(Mandatory = $true)]
    [string]$Namespace
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

if (-not $clusterExists) {
    Write-Host "Cluster '$ClusterName' does not exist. Nothing to destroy."
    exit 0
}

try {
    Invoke-NativeCommand kubectl config use-context "k3d-$ClusterName"
    Invoke-NativeCommand kubectl delete -k $OverlayPath --ignore-not-found=true
}
catch {
    Write-Warning "Skipping workload deletion because the cluster is not reachable: $($_.Exception.Message)"
}

Write-Host "Deleting cluster '$ClusterName'"
Invoke-NativeCommand k3d cluster delete $ClusterName
