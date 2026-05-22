#Requires -Version 7

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$FormatKubeContextForOutput = {
    param([AllowNull()][string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return '<empty>'
    }

    if ($Value -match '(?i)https?://|certificate-authority|client-certificate|client-key|bearer|token|eyJ|-----BEGIN') {
        return '<redacted-context>'
    }

    return $Value
}

function Assert-KubeContext {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Expected
    )

    try {
        $active = (& kubectl config current-context 2>&1 | Out-String).Trim()
    }
    catch {
        Write-Host "[context] active: <error>"
        throw "ConfirmContext mismatch: expected '$(& $FormatKubeContextForOutput $Expected)', got '<error>'"
    }

    if ([string]::IsNullOrWhiteSpace($active)) {
        Write-Host "[context] active: <empty>"
        throw "ConfirmContext mismatch: expected '$(& $FormatKubeContextForOutput $Expected)', got '<empty>'"
    }

    $safeActive = & $FormatKubeContextForOutput $active
    Write-Host "[context] active: $safeActive"

    if ($active -cne $Expected) {
        throw "ConfirmContext mismatch: expected '$(& $FormatKubeContextForOutput $Expected)', got '$safeActive'"
    }

    return $active
}
