$ErrorActionPreference='Stop'
$coastRoot=Split-Path $PSScriptRoot -Parent
$coastSource=Join-Path $coastRoot 'ArtSource/CoastalLife'
$coastOutput=Join-Path $coastRoot 'Lighthouse/Assets/Art/CoastalLife/Audio'
New-Item -ItemType Directory -Path $coastOutput -Force | Out-Null
function Clip($inputFile,$name,$filter) {
    & ffmpeg -hide_banner -loglevel error -y -i $inputFile -af $filter -ac 1 -ar 48000 -c:a pcm_s16le (Join-Path $coastOutput ($name+'.wav'))
    if($LASTEXITCODE -ne 0){throw "Processing failed: $name"}
}
foreach($id in @('02','03','04','06')) {
    Clip (Join-Path $coastSource "GullCall$id.wav") "GullCall$id" 'highpass=f=500,lowpass=f=10500,loudnorm=I=-22:TP=-5:LRA=8,afade=t=in:d=0.015,areverse,afade=t=in:d=0.04,areverse'
}
Clip (Join-Path $coastSource 'Camera2544.wav') 'CameraShutter' 'silenceremove=start_periods=1:start_duration=0.005:start_threshold=-45dB,highpass=f=100,loudnorm=I=-19:TP=-3:LRA=7,afade=t=in:d=0.002,areverse,afade=t=in:d=0.025,areverse'
Clip (Join-Path $coastSource 'Wing2891.ogg') 'WingTakeoff' 'highpass=f=140,lowpass=f=8500,loudnorm=I=-22:TP=-5:LRA=8,afade=t=in:d=0.02,areverse,afade=t=in:d=0.08,areverse'
Clip (Join-Path $coastRoot 'ArtSource/Audio/Marine/Wood/wood_squeak_01.ogg') 'DoorHinge' 'highpass=f=150,lowpass=f=6500,loudnorm=I=-24:TP=-5:LRA=9,afade=t=in:d=0.015'
Clip (Join-Path $coastRoot 'ArtSource/Audio/Marine/Wood/wood_cracking_03.ogg') 'GalleryCreak' 'asetrate=33075,aresample=48000,highpass=f=120,lowpass=f=4200,loudnorm=I=-26:TP=-7:LRA=9,afade=t=in:d=0.03'
Clip (Join-Path $coastRoot 'ArtSource/Audio/Marine/Wood/wood_squeak_02.ogg') 'LampLever' 'highpass=f=200,lowpass=f=5500,loudnorm=I=-24:TP=-5:LRA=9,afade=t=in:d=0.015'
function Loop($file,$name,$start,$duration,$crossfade,$filter) {
    $end=$duration-$crossfade
    $graph="[0:a]atrim=start=${start}:duration=${duration},asetpts=PTS-STARTPTS,$filter,asplit=3[a][b][c];[a]atrim=start=${crossfade}:end=${end},asetpts=PTS-STARTPTS[mid];[b]atrim=start=${end}:end=${duration},asetpts=PTS-STARTPTS[tail];[c]atrim=start=0:end=${crossfade},asetpts=PTS-STARTPTS[head];[tail][head]acrossfade=d=${crossfade}:c1=qsin:c2=qsin[seam];[mid][seam]concat=n=2:v=0:a=1[out]"
    & ffmpeg -hide_banner -loglevel error -y -i (Join-Path $coastSource $file) -filter_complex $graph -map '[out]' -ac 1 -ar 48000 -c:a libvorbis -q:a 6 (Join-Path $coastOutput ($name+'.ogg'))
    if($LASTEXITCODE -ne 0){throw "Loop failed: $name"}
}
Loop 'Burner0057.ogg' 'LampBurner' 8 8 1 'highpass=f=100,lowpass=f=4600,loudnorm=I=-25:TP=-5:LRA=6'
Loop 'Flag1633.ogg' 'FlagFlutter' 8 18 1 'highpass=f=180,lowpass=f=8500,loudnorm=I=-24:TP=-5:LRA=8'
Loop 'Radio0313.ogg' 'RadioStatic' .2 3 .3 'highpass=f=650,lowpass=f=3000,loudnorm=I=-26:TP=-6:LRA=7'
Clip (Join-Path $coastSource 'Radio0313.ogg') 'RadioTransmit' 'highpass=f=400,lowpass=f=3800,loudnorm=I=-22:TP=-5:LRA=7,afade=t=in:d=0.03,areverse,afade=t=in:d=0.1,areverse'
Get-ChildItem $coastOutput -File | Where-Object Extension -ne '.meta' | Select-Object Name,Length
