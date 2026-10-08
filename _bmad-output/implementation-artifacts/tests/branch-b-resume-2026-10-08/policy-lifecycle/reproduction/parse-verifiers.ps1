param([string]$Root)
$failure=$false
foreach($path in @('eng/verify-ext-parties-1.ps1','eng/verify-ext-parties-history-readiness.ps1')){
 $tokens=$null; $errors=$null
 [void][System.Management.Automation.Language.Parser]::ParseFile((Join-Path $Root $path), [ref]$tokens, [ref]$errors)
 if($errors.Count){$failure=$true; $errors | ForEach-Object { Write-Error $_.Message }}
 else {Write-Output "Syntax passed: $path"}
}
if($failure){exit 1}
