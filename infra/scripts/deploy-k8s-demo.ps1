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

Invoke-NativeCommand kubectl config use-context "k3d-$ClusterName"

Write-Host "Applying Kubernetes overlay from $OverlayPath"
Invoke-NativeCommand kubectl apply -k $OverlayPath

Write-Host "Waiting for RabbitMQ rollout"
Invoke-NativeCommand kubectl rollout status deployment/rabbitmq -n $Namespace --timeout=180s

Write-Host "Waiting for Orders API rollout"
Invoke-NativeCommand kubectl rollout status deployment/orders-api -n $Namespace --timeout=180s

Write-Host "Waiting for Shipping API rollout"
Invoke-NativeCommand kubectl rollout status deployment/shipping-api -n $Namespace --timeout=180s
