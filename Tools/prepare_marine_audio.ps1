$ErrorActionPreference='Stop'
$marineRoot=Split-Path -Parent $PSScriptRoot
$wood=Join-Path $marineRoot 'ArtSource/Audio/Marine/Wood'
$water=Join-Path $marineRoot 'ArtSource/Audio/Marine/Water'
$output=Join-Path $marineRoot 'Lighthouse/Assets/Art/Marine/Audio'
New-Item -ItemType Directory -Force $output | Out-Null
function Layer($name,$one,$two,$rate,$delay,$gain) {
    $filter="[0:a]asetrate=$rate,aresample=44100,highpass=f=35,lowpass=f=3800[a];[1:a]adelay=$delay|$delay,highpass=f=55,lowpass=f=5500,volume=$gain[b];[a][b]amix=inputs=2:normalize=0,loudnorm=I=-18:TP=-3:LRA=9,afade=t=in:d=0.008,alimiter=limit=0.72[out]"
    & ffmpeg -hide_banner -loglevel error -y -i $one -i $two -filter_complex $filter -map '[out]' -ac 1 -ar 44100 (Join-Path $output ($name+'.wav'))
    if($LASTEXITCODE -ne 0){throw "Audio processing failed: $name"}
}
Layer 'TimberStrain' (Join-Path $wood 'wood_squeak_01.ogg') (Join-Path $wood 'wood_cracking_03.ogg') 26000 280 0.65
Layer 'MastBreakA' (Join-Path $wood 'wood_breaking_01.ogg') (Join-Path $wood 'wood_cracking_01.ogg') 26000 95 0.8
Layer 'MastBreakB' (Join-Path $wood 'wood_breaking_02.ogg') (Join-Path $wood 'wood_cracking_04.ogg') 29000 130 0.7
Layer 'HullRupture' (Join-Path $wood 'wood_breaking_02.ogg') (Join-Path $wood 'wood_slam_02.ogg') 19000 65 0.7
Layer 'TentacleImpactA' (Join-Path $water 'splash_06.ogg') (Join-Path $wood 'wood_slam_01.ogg') 23000 35 0.95
Layer 'TentacleImpactB' (Join-Path $water 'splash_09.ogg') (Join-Path $wood 'wood_slam_03.ogg') 25000 60 0.8
Layer 'HullSubmerging' (Join-Path $water 'loop_bubbles_02.ogg') (Join-Path $water 'splash_14.ogg') 26000 300 0.9
Layer 'SeaDisplacement' (Join-Path $water 'splash_13.ogg') (Join-Path $water 'splash_04.ogg') 19000 150 0.7
Get-ChildItem $output -File | Select-Object Name,Length
