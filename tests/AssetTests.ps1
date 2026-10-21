[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$assetRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
if (-not ('SpriteAssetReader' -as [type])) {
    Add-Type -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Rectangle].Assembly.Location,
        [Reflection.Assembly]::Load('System.Private.Windows.GdiPlus').Location,
        [Reflection.Assembly]::Load('System.Private.Windows.Core').Location) -TypeDefinition @'
using System;
using System.Drawing;
public sealed class SpriteAssetReader : IDisposable {
    private readonly Bitmap image;
    public int Width => image.Width;
    public int Height => image.Height;
    public SpriteAssetReader(string path) { image = new Bitmap(path); }
    public Func<int, int, byte> AlphaReader() => (x, y) => image.GetPixel(x, y).A;
    public byte[] Pixels() {
        byte[] data = new byte[Width * Height * 4];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) {
            Color c = image.GetPixel(x, y); int i = (x + y * Width) * 4;
            data[i] = c.R; data[i + 1] = c.G; data[i + 2] = c.B; data[i + 3] = c.A;
        }
        return data;
    }
    public static void ValidateOverlay(string basePath, string overlayPath) {
        using var source = new Bitmap(basePath);
        using var overlay = new Bitmap(overlayPath);
        if (source.Width != overlay.Width || source.Height != overlay.Height) throw new Exception("Steve overlay coordinate mismatch");
        int[] counts = new int[8];
        for (int y=0; y<source.Height; y++) for (int x=0; x<source.Width; x++) {
            int alpha = overlay.GetPixel(x,y).A;
            if (alpha==0) continue;
            if (alpha>source.GetPixel(x,y).A) throw new Exception("Armor changes Steve's original silhouette or alpha");
            int col=Math.Min(3,x/(source.Width/4)), row=Math.Min(1,y/(source.Height/2));
            counts[col+row*4]++;
        }
        foreach(int count in counts) if(count<500) throw new Exception("Missing Steve equipment pose");
    }
    public void Dispose() { image.Dispose(); }
}
'@
}
foreach ($steveArmorPart in @('Head','Body','Legs')) {
    [SpriteAssetReader]::ValidateOverlay((Join-Path $assetRoot 'DimensionalTerraria/Textures/Steve.png'),
        (Join-Path $assetRoot "DimensionalTerraria/Textures/Equipment/SteveArmor_$steveArmorPart.png"))
    Write-Output "PASS Steve armor overlay: $steveArmorPart, eight aligned poses, original body silhouette and alpha retained"
}
$assetLogic = [Reflection.Assembly]::LoadFrom((Join-Path $assetRoot 'tests\SpatialTests\bin\Release\net8.0\SpatialTests.dll'))
$assetSlice = $assetLogic.GetType('DimensionalTerraria.Core.SpriteSheetLayout').GetMethod('Slice')
$assetTryFrame = $assetLogic.GetType('DimensionalTerraria.Core.SpriteSheetLayout').GetMethod('TryFrame')
foreach ($assetName in @('Steve', 'Sheep', 'IronGolem', 'Villager', 'HigherDimensionalProjection')) {
    $assetReader = [SpriteAssetReader]::new((Join-Path $assetRoot "DimensionalTerraria\Textures\$assetName.png"))
    try {
        $assetColumns = if ($assetName -eq 'HigherDimensionalProjection') { 1 } else { 4 }
        $assetRows = if ($assetColumns -eq 1) { 1 } else { 2 }
        $assetFrames = $assetSlice.Invoke($null, @($assetReader.Width, $assetReader.Height, $assetColumns, $assetRows, $assetReader.AlphaReader()))
        if ($assetFrames.Count -ne $assetColumns * $assetRows) { throw "Wrong frame count: $assetName" }
        if ($assetName -in @('IronGolem', 'Villager') -and $assetReader.AlphaReader().Invoke(0, 0) -ge 8) { throw "$assetName needs a transparent background (allowing near-zero edge alpha)." }
        foreach ($assetFrame in $assetFrames) {
            if ($assetFrame.Empty -or $assetFrame.Width -lt 20 -or $assetFrame.Height -lt 20) { throw "Empty or damaged frame: $assetName" }
            if ($assetFrame.FootX -lt 0 -or $assetFrame.FootX -ge $assetFrame.Width) { throw "Invalid foot anchor: $assetName" }
        }
        for ($assetFrameIndex = 0; $assetFrameIndex -lt $assetFrames.Count; $assetFrameIndex++) {
            $assetArguments = [object[]]@($assetFrames, $assetFrameIndex, $null)
            if (-not $assetTryFrame.Invoke($null, $assetArguments) -or $assetArguments[2].Empty) { throw "Cannot safely select frame $assetFrameIndex of $assetName" }
        }
        if ($assetColumns -gt 1 -and @($assetFrames | Where-Object { $_.Height -gt $assetReader.Height / 2 }).Count -gt 0) { throw "Stacked sheet detected: $assetName" }
        Write-Output "PASS asset: $assetName, $($assetFrames.Count) isolated frames, $($assetReader.Width)x$($assetReader.Height) source"
    }
    finally { $assetReader.Dispose() }
}
$mcAssetDirectory = Join-Path $assetRoot 'DimensionalTerraria\Textures\Minecraft'
$mcClientZipPath = Join-Path $assetRoot '.dev\minecraft-1.21.1\client.jar'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$mcZip = if (Test-Path -LiteralPath $mcClientZipPath) { [IO.Compression.ZipFile]::OpenRead($mcClientZipPath) } else { $null }
try {
    foreach ($mcName in @('netherite_ingot', 'firework_rocket', 'oak_planks', 'crafting_table_front', 'crafting_table_side', 'crafting_table_top', 'oak_boat', 'redstone', 'redstone_block', 'piston_top', 'piston_top_sticky', 'piston_bottom', 'piston_side', 'piston_inner', 'observer_back', 'observer_back_on', 'observer_side', 'hopper', 'hopper_top', 'hopper_inside', 'hopper_outside', 'bedrock', 'netherite_pickaxe', 'netherite_helmet', 'netherite_chestplate', 'netherite_leggings', 'netherite_boots')) {
        $mcPath = Join-Path $mcAssetDirectory "$mcName.png"
        $mcReader = [SpriteAssetReader]::new($mcPath)
        try {
            if ($mcReader.Width -ne 16 -or $mcReader.Height -ne 16) { throw "Non-native block size: $mcName" }
            $mcPixels = $mcReader.Pixels()
            for ($mcAlpha = 3; $mcAlpha -lt $mcPixels.Length; $mcAlpha += 4) {
                if ($mcName -notlike 'netherite_*' -and $mcName -notin @('firework_rocket', 'oak_boat', 'redstone', 'hopper', 'hopper_top') -and $mcPixels[$mcAlpha] -ne 255) { throw "Unexpected transparent block face: $mcName" }
            }
        } finally { $mcReader.Dispose() }
        if ($null -ne $mcZip) {
            $mcKind = if ($mcName -like 'netherite_*' -or $mcName -in @('firework_rocket', 'oak_boat', 'redstone', 'hopper')) { 'item' } else { 'block' }
            $mcEntryStream = $mcZip.GetEntry("assets/minecraft/textures/$mcKind/$mcName.png").Open()
            $mcEntryBytes = [IO.MemoryStream]::new()
            try {
                $mcEntryStream.CopyTo($mcEntryBytes)
                $mcOriginalHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($mcEntryBytes.ToArray()))
                if ($mcOriginalHash -ne (Get-FileHash -LiteralPath $mcPath -Algorithm SHA256).Hash) { throw "Minecraft source differs: $mcName" }
            } finally { $mcEntryStream.Dispose(); $mcEntryBytes.Dispose() }
        }
        Write-Output "PASS native Minecraft asset: $mcName, 16x16 texture, original bytes"
    }
    foreach ($mcLayer in @('netherite_layer_1','netherite_layer_2','chest_normal')) {
        $mcLayerPath = Join-Path $mcAssetDirectory "$mcLayer.png"
        $mcLayerReader = [SpriteAssetReader]::new($mcLayerPath)
        try {
            $mcLayerHeight = if ($mcLayer -eq 'chest_normal') { 64 } else { 32 }
            if ($mcLayerReader.Width -ne 64 -or $mcLayerReader.Height -ne $mcLayerHeight) { throw "Incorrect UV atlas size: $mcLayer" }
        } finally { $mcLayerReader.Dispose() }
        if ($null -ne $mcZip) {
            $mcLayerEntry = if ($mcLayer -eq 'chest_normal') { 'assets/minecraft/textures/entity/chest/normal.png' } else { "assets/minecraft/textures/models/armor/$mcLayer.png" }
            $mcLayerStream = $mcZip.GetEntry($mcLayerEntry).Open()
            $mcLayerBytes = [IO.MemoryStream]::new()
            try {
                $mcLayerStream.CopyTo($mcLayerBytes)
                if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($mcLayerBytes.ToArray())) -ne (Get-FileHash -LiteralPath $mcLayerPath -Algorithm SHA256).Hash) { throw "Original armor layer bytes differ: $mcLayer" }
            } finally { $mcLayerStream.Dispose(); $mcLayerBytes.Dispose() }
        }
        Write-Output "PASS native Minecraft UV: $mcLayer, 64x$mcLayerHeight, original bytes"
    }
} finally { if ($null -ne $mcZip) { $mcZip.Dispose() } }
foreach ($equip in @(@('HigherDimensionalHelmet_Head',40,1120),@('HigherDimensionalChestplate_Body',360,224),@('HigherDimensionalLeggings_Legs',40,1120),@('HigherDimensionalPickaxe',32,32),@('HigherDimensionalHelmet',32,32),@('HigherDimensionalChestplate',32,32),@('HigherDimensionalLeggings',32,32))) {
    $equipReader = [SpriteAssetReader]::new((Join-Path $assetRoot "DimensionalTerraria/Textures/Equipment/$($equip[0]).png"))
    try {
        if ($equipReader.Width -ne $equip[1] -or $equipReader.Height -ne $equip[2]) { throw "Invalid native equipment layout: $($equip[0])" }
        $equipPixels = $equipReader.Pixels()
        if ($equipPixels[3] -ne 0 -or -not ($equipPixels | Where-Object { $_ -ne 0 } | Select-Object -First 1)) { throw "Invalid transparent equipment texture: $($equip[0])" }
        if ($equip[0] -match '_Head|_Legs') {
            for ($equipFrame = 0; $equipFrame -lt 20; $equipFrame++) {
                $equipVisible = 0
                for ($equipY = $equipFrame * 56; $equipY -lt ($equipFrame + 1) * 56; $equipY++) {
                    for ($equipX = 0; $equipX -lt 40; $equipX++) { if ($equipPixels[($equipX + $equipY * 40) * 4 + 3] -gt 0) { $equipVisible++ } }
                }
                if ($equipVisible -lt 10) { throw "Missing equipment animation frame: $($equip[0]) frame $equipFrame" }
            }
        }
        Write-Output "PASS equipment texture: $($equip[0]), $($equip[1])x$($equip[2]), native layout and transparent edges"
    } finally { $equipReader.Dispose() }
}
