param(
    [ValidateSet("unit", "integration", "topology", "ci", "all", "coverage")]
    [string] $Lane = "unit",

    [string] $Configuration = "Release",

    # Continue running every project in the lane even after one fails, then report
    # all failing projects in a single summary. Without this switch the lane keeps
    # the historical fail-fast behavior (stops at the first failing project).
    [switch] $ContinueOnFailure,

    # Emit an inspectable TRX result file per project into this directory (local
    # parity with the CI shards). Relative paths resolve against the repository root.
    [string] $ResultsDirectory,

    # MSBuild properties forwarded to each dotnet test invocation as -p:<value>,
    # e.g. -Properties UseHexalithProjectReferences=true,UseNuGetDeps=false.
    [string[]] $Properties = @()
)

$ErrorActionPreference = "Stop"

$RepositoryRoot = Split-Path -Parent $PSScriptRoot

if ($ResultsDirectory) {
    if (-not [System.IO.Path]::IsPathRooted($ResultsDirectory)) {
        $ResultsDirectory = Join-Path $RepositoryRoot $ResultsDirectory
    }

    New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
}

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

    if ($ResultsDirectory) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
        $arguments += @(
            "--results-directory",
            $ResultsDirectory,
            "--report-xunit-trx",
            "--report-xunit-trx-filename",
            "$projectName.trx"
        )
    }

    foreach ($property in $Properties) {
        $arguments += "-p:$property"
    }

    & dotnet @arguments | ForEach-Object { Write-Host $_ }
    return [int]$LASTEXITCODE
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

$ciProjects = @(
    "tests/Hexalith.Parties.Ci.Tests/Hexalith.Parties.Ci.Tests.csproj"
)

$allProjects = $unitProjects + $integrationProjects + $topologyProjects + $ciProjects

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

$laneProjects = switch ($Lane) {
    "unit" { $unitProjects }
    "integration" { $integrationProjects }
    "topology" { $topologyProjects }
    "ci" { $ciProjects }
    "all" { $allProjects }
    "coverage" { $allProjects }
}

$additionalArguments = @()
if ($Lane -eq "coverage") {
    throw "The coverage lane is not currently supported under Microsoft.Testing.Platform/xUnit v3. Use the test lanes for validation until an MTP-compatible coverage extension is configured."
}

$results = @()
foreach ($project in $laneProjects) {
    $exitCode = Invoke-TestProject -ProjectPath $project -AdditionalArguments $additionalArguments
    $results += [pscustomobject]@{
        Project  = $project
        ExitCode = $exitCode
        Passed   = ($exitCode -eq 0)
    }

    if ($exitCode -ne 0 -and -not $ContinueOnFailure) {
        throw "dotnet test failed for $project with exit code $exitCode."
    }
}

$failedResults = @($results | Where-Object { -not $_.Passed })

if ($ContinueOnFailure) {
    Write-Host ""
    Write-Host "Lane '$Lane' result summary ($($results.Count) project(s)):"
    foreach ($result in $results) {
        $status = if ($result.Passed) { "PASS" } else { "FAIL ($($result.ExitCode))" }
        Write-Host ("  {0,-9} {1}" -f $status, $result.Project)
    }
    Write-Host ""

    if ($failedResults.Count -gt 0) {
        Write-Host "$($failedResults.Count) of $($results.Count) project(s) failed."
        exit 1
    }

    Write-Host "All $($results.Count) project(s) passed."
}
