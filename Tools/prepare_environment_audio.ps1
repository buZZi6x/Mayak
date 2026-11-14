$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$source=Join-Path $taskRoot 'ArtSource/Audio'
$destination=Join-Path $taskRoot 'Lighthouse/Assets/Environment/Audio'
New-Item -ItemType Directory -Force $destination | Out-Null
$loop='[0:a]atrim=start=25:duration=64,asetpts=PTS-STARTPTS,highpass=f=65,lowpass=f=6500,loudnorm=I=-22:TP=-3:LRA=7,asplit=3[a][b][c];[a]atrim=start=4:end=60,asetpts=PTS-STARTPTS[mid];[b]atrim=start=60:end=64,asetpts=PTS-STARTPTS[tail];[c]atrim=start=0:end=4,asetpts=PTS-STARTPTS[head];[tail][head]acrossfade=d=4:c1=qsin:c2=qsin[seam];[mid][seam]concat=n=2:v=0:a=1[out]'
& ffmpeg -hide_banner -loglevel error -y -i (Join-Path $source WaterBed.mp3) -filter_complex $loop -map '[out]' -ar 48000 -c:a libvorbis -q:a 7 (Join-Path $destination Offshore.ogg)
& ffmpeg -hide_banner -loglevel error -y -i (Join-Path $source Wind.ogg) -af 'highpass=f=85,lowpass=f=9000,loudnorm=I=-23:TP=-5:LRA=8' -ar 48000 -c:a pcm_s16le (Join-Path $destination Wind.wav)
& ffmpeg -hide_banner -loglevel error -y -i (Join-Path $source Rain/1.ogg) -af 'highpass=f=100,loudnorm=I=-23:TP=-4:LRA=7' -ar 48000 -c:a libvorbis -q:a 7 (Join-Path $destination RainLight.ogg)
& ffmpeg -hide_banner -loglevel error -y -i (Join-Path $source Rain/3.ogg) -af 'highpass=f=70,loudnorm=I=-22:TP=-4:LRA=7' -ar 48000 -c:a libvorbis -q:a 7 (Join-Path $destination RainHeavy.ogg)
& ffmpeg -hide_banner -loglevel error -y -i (Join-Path $source Thunder_3113.ogg) -af 'highpass=f=35,lowpass=f=6500,loudnorm=I=-18:TP=-3:LRA=12,afade=t=in:d=0.015,afade=t=out:st=4.7:d=0.3' -ar 48000 -c:a pcm_s16le (Join-Path $destination Thunder.wav)
1..4 | ForEach-Object {
    $name='Surf_0'+$_
    & ffmpeg -hide_banner -loglevel error -y -i (Join-Path $source "$name.flac") -af 'highpass=f=65,loudnorm=I=-22:TP=-4:LRA=8,afade=t=in:d=0.06' -ar 48000 -ac 1 -c:a libvorbis -q:a 7 (Join-Path $destination "$name.ogg")
}
if($LASTEXITCODE -ne 0){throw 'Audio processing failed'}
