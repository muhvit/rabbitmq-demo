param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

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

Push-Location $RepoRoot

try {
    Write-Host "Building Orders API image: $OrdersImage"
    Invoke-NativeCommand docker build -t $OrdersImage -f src/Orders.Api/Dockerfile .

    Write-Host "Building Shipping API image: $ShippingImage"
    Invoke-NativeCommand docker build -t $ShippingImage -f src/Shipping.Api/Dockerfile .
}
finally {
    Pop-Location
}
