# Ship a finished step: pull --rebase, check translation keys, commit tracked changes, push.
# Usage: powershell -File tools/ship.ps1 -Message "text" [-Add "path1","path2"]
# Only tracked files are staged (git add -u); new files must be listed in -Add, so stray art is never swept in.
param(
    [Parameter(Mandatory = $true)][string]$Message,
    [string[]]$Add = @()
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'dev') { throw "Branch is '$branch', expected 'dev'. Stopping." }

git pull --rebase --autostash origin dev
if ($LASTEXITCODE -ne 0) { throw 'pull --rebase failed, resolve by hand.' }

python tools/check_keys.py
if ($LASTEXITCODE -ne 0) { throw 'check_keys failed, nothing committed.' }

git add -u
foreach ($p in $Add) { git add -- $p }

git diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Host 'Nothing to commit.'; exit 0 }

git commit -m $Message -m 'Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>'
if ($LASTEXITCODE -ne 0) { throw 'commit failed.' }
git push -u origin dev
if ($LASTEXITCODE -ne 0) { throw 'push failed.' }
Write-Host 'Shipped.'
