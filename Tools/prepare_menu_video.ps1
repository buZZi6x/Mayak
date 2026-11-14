$ErrorActionPreference = 'Stop'
$menuRepo = Split-Path $PSScriptRoot -Parent
$menuFrames = Join-Path $menuRepo 'Artifacts/UI/MainMenu/SeaCapture/frame-%04d.jpg'
$menuVideo = Join-Path $menuRepo 'ArtSource/UI/assets/menu-simulation.mp4'
$menuPoster = Join-Path $menuRepo 'ArtSource/UI/assets/menu-simulation-poster.jpg'
if (!(Test-Path -LiteralPath (Join-Path $menuRepo 'Artifacts/UI/MainMenu/SeaCapture/frame-0479.jpg'))) {
    throw 'Capture 480 frames with Lighthouse/AgentScripts/CaptureMenuSea.cs first.'
}
# Original seconds 1..20; the last second fades into seconds 0..1.
# The end then joins the beginning at the same simulation moment.
$menuLoop = '[0:v]split[head][tail];[head]trim=start=0:end=1,setpts=PTS-STARTPTS,fps=24,settb=AVTB[first];[tail]trim=start=1,setpts=PTS-STARTPTS,fps=24,settb=AVTB[main];[main][first]xfade=transition=fade:duration=1:offset=18,format=yuv420p[loop]'
& ffmpeg -hide_banner -loglevel error -y -framerate 24 -i $menuFrames -filter_complex_threads 2 -filter_complex $menuLoop -map '[loop]' -c:v libx264 -preset fast -crf 20 -threads 4 -movflags +faststart -an $menuVideo
if ($LASTEXITCODE -ne 0) { throw 'Menu video encoding failed.' }
Copy-Item -LiteralPath (Join-Path $menuRepo 'Artifacts/UI/MainMenu/SeaCapture/frame-0024.jpg') -Destination $menuPoster
