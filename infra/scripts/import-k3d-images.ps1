param(
    [Parameter(Mandatory = $true)]
    [string]$ClusterName,

    [Parameter(Mandatory = $true)]
    [string]$OrdersImage,

    [Parameter(Mandatory = $true)]
    [string]$ShippingImage
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

Write-Host "Importing demo images into cluster '$ClusterName'"
Invoke-NativeCommand k3d image import $OrdersImage $ShippingImage -c $ClusterName
