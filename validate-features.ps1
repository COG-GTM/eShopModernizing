# Feature Validation Script for .NET Core Migration

$configurations = @(
    @{ UseMockData = $true; UseAzureStorage = $false; UseManagedIdentity = $false; UseAzureActiveDirectory = $false },
    @{ UseMockData = $false; UseAzureStorage = $true; UseManagedIdentity = $true; UseAzureActiveDirectory = $false },
    @{ UseMockData = $false; UseAzureStorage = $true; UseManagedIdentity = $true; UseAzureActiveDirectory = $true }
)

function Test-Configuration {
    param($config)
    
    Write-Host "Testing configuration: $($config | ConvertTo-Json -Compress)"
    
    # Update appsettings.json
    $appSettings = Get-Content "eShopModernized/src/eShopCoreModernized/appsettings.json" | ConvertFrom-Json
    $appSettings.AppSettings.UseMockData = $config.UseMockData
    $appSettings.AppSettings.UseAzureStorage = $config.UseAzureStorage
    $appSettings.AppSettings.UseAzureManagedIdentity = $config.UseManagedIdentity
    $appSettings.AppSettings.UseAzureActiveDirectory = $config.UseAzureActiveDirectory
    
    $appSettings | ConvertTo-Json -Depth 10 | Set-Content "eShopModernized/src/eShopCoreModernized/appsettings.json"
    
    # Build and test
    dotnet build "eShopModernized/src/eShopCoreModernized/eShopModernized.csproj"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for configuration"
        return $false
    }
    
    # Test health endpoint
    Start-Process -FilePath "dotnet" -ArgumentList "run --project eShopModernized/src/eShopCoreModernized/eShopModernized.csproj" -NoNewWindow
    Start-Sleep 10
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5002/health/detailed" -Method Get
        Write-Host "Health check passed: $($response.status)"
        return $true
    }
    catch {
        Write-Error "Health check failed: $($_.Exception.Message)"
        return $false
    }
    finally {
        # Stop the application
        Get-Process -Name "dotnet" | Where-Object { $_.ProcessName -eq "dotnet" } | Stop-Process -Force
    }
}

# Test all configurations
foreach ($config in $configurations) {
    $result = Test-Configuration $config
    if (-not $result) {
        Write-Error "Configuration test failed"
        exit 1
    }
}

Write-Host "All feature configurations validated successfully"
