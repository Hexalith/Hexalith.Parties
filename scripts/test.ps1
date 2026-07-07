param(
    [ValidateSet("unit", "integration", "topology", "deploy", "all", "coverage")]
    [string] $Lane = "unit",

    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$RepositoryRoot = Split-Path -Parent $PSScriptRoot

function Invoke-TestProject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,

        [string[]] $AdditionalArguments = @()
    )

    $fullPath = Join-Path $RepositoryRoot $ProjectPath
    $arguments = @(
        "test",
        $fullPath,
        "--configuration",
        $Configuration,
        "--verbosity",
        "minimal"
    ) + $AdditionalArguments

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed for $ProjectPath with exit code $LASTEXITCODE."
    }
}

$unitProjects = @(
    "tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj",
    "tests/Hexalith.Parties.Authentication.Tests/Hexalith.Parties.Authentication.Tests.csproj",
    "tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj",
    "tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj",
    "tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj",
    "tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj",
    "tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj",
    "tests/Hexalith.Parties.ConsumerPortal.Tests/Hexalith.Parties.ConsumerPortal.Tests.csproj",
    "tests/Hexalith.Parties.UI.Tests/Hexalith.Parties.UI.Tests.csproj",
    "tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj",
    "tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj"
)

$integrationProjects = @(
    "tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj",
    "tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj"
)

$topologyProjects = @(
    "tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj"
)

$deployProjects = @(
    "tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj"
)

$allProjects = $unitProjects + $integrationProjects + $topologyProjects + $deployProjects

function Assert-TestProjectInventory {
    $discoveredProjects = Get-ChildItem -Path (Join-Path $RepositoryRoot "tests") -Filter "*.csproj" -Recurse |
        ForEach-Object { [System.IO.Path]::GetRelativePath($RepositoryRoot, $_.FullName).Replace('\', '/') } |
        Sort-Object

    $configuredProjects = $allProjects | Sort-Object
    $duplicateProjects = $allProjects | Group-Object | Where-Object { $_.Count -gt 1 }

    if ($duplicateProjects) {
        $duplicateList = ($duplicateProjects | ForEach-Object { $_.Name }) -join ", "
        throw "Duplicate test project entries in scripts/test.ps1: $duplicateList"
    }

    $inventoryDiff = Compare-Object -ReferenceObject $discoveredProjects -DifferenceObject $configuredProjects
    if ($inventoryDiff) {
        $details = ($inventoryDiff | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join "; "
        throw "Test project inventory mismatch between tests/**/*.csproj and scripts/test.ps1: $details"
    }
}

Assert-TestProjectInventory

switch ($Lane) {
    "unit" {
        foreach ($project in $unitProjects) {
            Invoke-TestProject -ProjectPath $project
        }
    }
    "integration" {
        foreach ($project in $integrationProjects) {
            Invoke-TestProject -ProjectPath $project
        }
    }
    "topology" {
        foreach ($project in $topologyProjects) {
            Invoke-TestProject -ProjectPath $project
        }
    }
    "deploy" {
        foreach ($project in $deployProjects) {
            Invoke-TestProject -ProjectPath $project
        }
    }
    "all" {
        foreach ($project in $allProjects) {
            Invoke-TestProject -ProjectPath $project
        }
    }
    "coverage" {
        foreach ($project in $allProjects) {
            Invoke-TestProject -ProjectPath $project -AdditionalArguments @("--collect", "XPlat Code Coverage")
        }
    }
}
