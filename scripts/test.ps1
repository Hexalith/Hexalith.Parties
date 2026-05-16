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
        [string] $ProjectPath
    )

    $fullPath = Join-Path $RepositoryRoot $ProjectPath
    dotnet test $fullPath --configuration $Configuration --verbosity minimal
}

$unitProjects = @(
    "tests/Hexalith.Parties.Contracts.Tests/Hexalith.Parties.Contracts.Tests.csproj",
    "tests/Hexalith.Parties.Client.Tests/Hexalith.Parties.Client.Tests.csproj",
    "tests/Hexalith.Parties.Server.Tests/Hexalith.Parties.Server.Tests.csproj",
    "tests/Hexalith.Parties.Projections.Tests/Hexalith.Parties.Projections.Tests.csproj",
    "tests/Hexalith.Parties.Security.Tests/Hexalith.Parties.Security.Tests.csproj",
    "tests/Hexalith.Parties.AdminPortal.Tests/Hexalith.Parties.AdminPortal.Tests.csproj",
    "tests/Hexalith.Parties.Picker.Tests/Hexalith.Parties.Picker.Tests.csproj",
    "tests/Hexalith.Parties.Mcp.Tests/Hexalith.Parties.Mcp.Tests.csproj"
)

$integrationProjects = @(
    "tests/Hexalith.Parties.Tests/Hexalith.Parties.Tests.csproj",
    "tests/Hexalith.Parties.Sample.Tests/Hexalith.Parties.Sample.Tests.csproj"
)

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
        Invoke-TestProject -ProjectPath "tests/Hexalith.Parties.IntegrationTests/Hexalith.Parties.IntegrationTests.csproj"
    }
    "deploy" {
        Invoke-TestProject -ProjectPath "tests/Hexalith.Parties.DeployValidation.Tests/Hexalith.Parties.DeployValidation.Tests.csproj"
    }
    "all" {
        dotnet test (Join-Path $RepositoryRoot "Hexalith.Parties.slnx") --configuration $Configuration --verbosity minimal
    }
    "coverage" {
        dotnet test (Join-Path $RepositoryRoot "Hexalith.Parties.slnx") --configuration $Configuration --collect:"XPlat Code Coverage" --verbosity minimal
    }
}
