# Islandia - make the Android signing key once, and hand it to GitHub as repository secrets.
#
# Runs on the owner's Windows PC, in Windows PowerShell. Nothing here is sent anywhere except
# GitHub's encrypted repository secrets, and the password is never printed or saved to disk.
#
# What it does:
#   1. checks for Java's keytool and the GitHub command-line tool (installs the latter if missing)
#   2. signs in to GitHub in the browser, if not already signed in
#   3. makes the key at  %USERPROFILE%\IslandiaKey\islandia-release.jks  (or reuses one already there)
#   4. sets ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASSWORD, ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD
#   5. opens the folder so the key can be backed up
#
# Safe to run twice: an existing key is reused, never replaced, and the secrets are simply set again.

$Repo  = 'jvanekel-pixel/Islandia'
$Dir   = Join-Path $HOME 'IslandiaKey'
$Jks   = Join-Path $Dir 'islandia-release.jks'
$Alias = 'islandia'

function Find-Tool([string]$Name, [string[]]$Globs) {
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Source }
    foreach ($g in $Globs) {
        $hit = Get-ChildItem $g -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

function Plain([Security.SecureString]$s) { [Net.NetworkCredential]::new('', $s).Password }

function Step([string]$Text) { Write-Host ''; Write-Host $Text -ForegroundColor Cyan }

function Stop-Here([string]$Text) {
    Write-Host ''
    Write-Host "STOPPED: $Text" -ForegroundColor Red
    Write-Host 'Nothing was uploaded. Copy this message to Claude.'
    throw 'Islandia signing-key setup stopped.'
}

Step 'Step 1 of 5 - checking tools'
$keytool = Find-Tool 'keytool' @(
    'C:\Program Files\Eclipse Adoptium\*\bin\keytool.exe',
    'C:\Program Files\Microsoft\jdk-*\bin\keytool.exe',
    'C:\Program Files\Java\*\bin\keytool.exe')
if (-not $keytool) {
    Stop-Here "Java's keytool was not found. Run:  winget install EclipseAdoptium.Temurin.21.JDK  then open a new PowerShell window and run this again."
}
Write-Host "  keytool: $keytool"

$gh = Find-Tool 'gh' @('C:\Program Files\GitHub CLI\gh.exe')
if (-not $gh) {
    Write-Host '  Installing the GitHub command-line tool (one time)...'
    winget install --id GitHub.cli -e --accept-source-agreements --accept-package-agreements | Out-Host
    $gh = Find-Tool 'gh' @('C:\Program Files\GitHub CLI\gh.exe')
    if (-not $gh) { Stop-Here 'The GitHub command-line tool did not install. Run:  winget install GitHub.cli  and then run this again.' }
}
Write-Host "  gh: $gh"

Step 'Step 2 of 5 - GitHub sign-in'
& $gh auth status --hostname github.com *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host '  A browser window will open. Copy the one-time code shown below into it and approve.'
    & $gh auth login --hostname github.com --git-protocol https --web
    if ($LASTEXITCODE -ne 0) { Stop-Here 'GitHub sign-in did not finish.' }
}
& $gh repo view $Repo --json name *> $null
if ($LASTEXITCODE -ne 0) { Stop-Here "Signed in, but this account cannot see $Repo. Sign in as the repository owner:  gh auth login" }
Write-Host '  Signed in.'

Step 'Step 3 of 5 - the key'
if (Test-Path $Jks) {
    Write-Host "  A key already exists at $Jks - reusing it (never replacing it)."
    do {
        $Pass = Plain (Read-Host '  Type that key''s password' -AsSecureString)
        & $keytool -list -keystore $Jks -storepass $Pass *> $null
        $ok = ($LASTEXITCODE -eq 0)
        if (-not $ok) { Write-Host '  That password does not open the key. Try again.' -ForegroundColor Yellow }
    } until ($ok)
} else {
    New-Item -ItemType Directory -Force -Path $Dir | Out-Null
    Write-Host '  Choose a password for the key. Nothing shows while you type - that is normal.'
    Write-Host '  Save it in your password manager: it can never be recovered.'
    do {
        $a = Plain (Read-Host '  Password (at least 8 characters)' -AsSecureString)
        $b = Plain (Read-Host '  Same password again' -AsSecureString)
        $ok = $false
        if ($a -ne $b)          { Write-Host '  Those did not match. Try again.' -ForegroundColor Yellow }
        elseif ($a.Length -lt 8) { Write-Host '  Too short. Use at least 8 characters.' -ForegroundColor Yellow }
        elseif ($a -match '["\\]') { Write-Host '  Please avoid the " and \ characters (they break Windows command lines).' -ForegroundColor Yellow }
        else                     { $ok = $true }
    } until ($ok)
    $Pass = $a
    & $keytool -genkeypair -keystore $Jks -alias $Alias -keyalg RSA -keysize 4096 -validity 36500 `
        -storetype PKCS12 -dname 'CN=Islandia' -storepass $Pass -keypass $Pass *> $null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Jks)) { Stop-Here 'keytool could not make the key.' }
    Write-Host "  Made $Jks"
}

Step 'Step 4 of 5 - uploading the secrets to GitHub'
$secrets = [ordered]@{
    ANDROID_KEYSTORE_BASE64   = [Convert]::ToBase64String([IO.File]::ReadAllBytes($Jks))
    ANDROID_KEYSTORE_PASSWORD = $Pass
    ANDROID_KEY_ALIAS         = $Alias
    ANDROID_KEY_PASSWORD      = $Pass
}
foreach ($name in $secrets.Keys) {
    & $gh secret set $name --repo $Repo --body $secrets[$name] *> $null
    if ($LASTEXITCODE -ne 0) { Stop-Here "Could not set the secret $name." }
    Write-Host "  set $name"
}
$Pass = $null; $a = $null; $b = $null; $secrets = $null

Step 'Step 5 of 5 - back it up'
Write-Host '  The folder with the key is opening now.'
Write-Host '  Copy islandia-release.jks to a USB stick (or into your password manager).'
Write-Host '  If this file and its password are lost, future updates cannot install over the game'
Write-Host '  without losing the saved islands.'
Start-Process explorer.exe $Dir

Write-Host ''
Write-Host 'Done. Tell Claude: "signing key done".' -ForegroundColor Green
