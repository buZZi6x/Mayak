param([string]$Only = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Split-Path (Split-Path $root)
$runtime = Join-Path $project 'Lighthouse/Assets/Resources/KeeperRadio'
$preview = Join-Path $project 'ArtSource/UI/assets/radio'
$raw = Join-Path $root 'VoiceSources'
foreach ($folder in @($runtime,$preview,$raw)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
$catalog = Get-Content -LiteralPath (Join-Path $root 'broadcasts.json') -Raw | ConvertFrom-Json
$voice = New-Object -ComObject SAPI.SpVoice
$token = New-Object -ComObject SAPI.SpObjectToken
$token.SetId('HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens\MSTTS_V110_ruRU_PavelM')
$voice.Voice = $token
$voice.Rate = 0
$voice.Volume = 100
$report = @()
foreach ($entry in $catalog.broadcasts) {
    if ($Only -and $entry.key -ne $Only) { continue }
    $source = Join-Path $raw ($entry.key + '.wav')
    $destination = Join-Path $runtime ($entry.key + '.wav')
    $stream = New-Object -ComObject SAPI.SpFileStream
    $stream.Format.Type = 22
    $stream.Open($source,3,$false)
    try { $voice.AudioOutputStream = $stream; [void]$voice.Speak($entry.text,0) }
    finally { $stream.Close() }
    # Voice stays intelligible; narrow-band EQ, gentle saturation and short room tail.
    & ffmpeg -hide_banner -loglevel error -y -i $source -af 'highpass=f=260,lowpass=f=3400,acompressor=threshold=0.12:ratio=2.3:attack=10:release=95,alimiter=limit=0.82,afade=t=in:d=0.04,apad=pad_dur=0.22' -ar 24000 -ac 1 -c:a pcm_s16le $destination
    if ($LASTEXITCODE -ne 0) { throw "Audio processing failed: $($entry.key)" }
    Copy-Item -LiteralPath $destination -Destination $preview
    $duration = & ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $destination
    $report += [PSCustomObject]@{Key=$entry.key; Voice=$token.GetDescription(); Seconds=[double]::Parse($duration,[Globalization.CultureInfo]::InvariantCulture); Text=$entry.text; SHA256=(Get-FileHash -LiteralPath $destination).Hash}
}
Copy-Item -LiteralPath (Join-Path $root 'broadcasts.json') -Destination (Join-Path $runtime 'Broadcasts.json')
Copy-Item -LiteralPath (Join-Path $root 'broadcasts.json') -Destination $preview
$manifestPath = Join-Path $root 'audio-manifest.json'
if ($Only -and (Test-Path -LiteralPath $manifestPath)) {
    $report = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json | Where-Object { $_.Key -ne $Only }) + $report
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Output "Rendered $($report.Count) Russian broadcasts."
