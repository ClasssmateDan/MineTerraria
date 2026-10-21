[CmdletBinding()]
param([string]$TerrariaDirectory = 'F:/SteamLibrary/steamapps/common/Terraria')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
Add-Type -AssemblyName System.Drawing
$fna = [Reflection.Assembly]::LoadFrom((Resolve-Path '.dev/tModLoader/Libraries/FNA/1.0.0/FNA.dll'))
$decoderType = $fna.GetType('Microsoft.Xna.Framework.Content.LzxDecoder', $true)
function Read-XnbBitmap([string]$path) {
    $inputStream = [IO.File]::OpenRead($path)
    $reader = [IO.BinaryReader]::new($inputStream)
    try {
        $inputStream.Position = 5
        $compressed = ($reader.ReadByte() -band 128) -ne 0
        $length = $reader.ReadInt32()
        if ($compressed) {
            $size = $reader.ReadInt32()
            $output = [IO.MemoryStream]::new()
            $decoder = [Activator]::CreateInstance($decoderType, [Reflection.BindingFlags]'Instance, Public, NonPublic', $null, @([int]16), $null)
            while ($inputStream.Position -lt $length) {
                $first = [int]$reader.ReadByte(); $second = [int]$reader.ReadByte()
                $blockSize = ($first -shl 8) -bor $second; $frameSize = 32768
                if ($first -eq 255) {
                    $frameSize = ($second -shl 8) -bor $reader.ReadByte()
                    $blockSize = ([int]$reader.ReadByte() -shl 8) -bor $reader.ReadByte()
                }
                if ($blockSize -eq 0 -or $frameSize -eq 0) { break }
                $next = $inputStream.Position + $blockSize
                $null = $decoderType.GetMethod('Decompress').Invoke($decoder, @($inputStream, [int]$blockSize, $output, [int]$frameSize))
                $inputStream.Position = $next
            }
            if ($output.Length -ne $size) { throw 'Bad XNB decompression' }
            $output.Position = 0; $payload = [IO.BinaryReader]::new($output)
        } else { $payload = $reader }
        $count = $payload.Read7BitEncodedInt()
        for ($i=0; $i -lt $count; $i++) { $null = $payload.ReadString(); $null = $payload.ReadInt32() }
        $null = $payload.Read7BitEncodedInt(); $null = $payload.Read7BitEncodedInt()
        $format = $payload.ReadInt32(); $width = $payload.ReadInt32(); $height = $payload.ReadInt32()
        $levels = $payload.ReadInt32(); $pixels = $payload.ReadBytes($payload.ReadInt32())
        if ($format -ne 0 -or $pixels.Length -ne $width*$height*4) { throw "Unsupported texture $format" }
        $bitmap = [Drawing.Bitmap]::new($width,$height)
        for ($y=0; $y -lt $height; $y++) { for ($x=0; $x -lt $width; $x++) {
            $p = ($x+$y*$width)*4
            $bitmap.SetPixel($x,$y,[Drawing.Color]::FromArgb($pixels[$p+3],$pixels[$p],$pixels[$p+1],$pixels[$p+2]))
        } }
        return $bitmap
    } finally { $reader.Dispose(); if ($null -ne $output) { $output.Dispose() } }
}
$armorSourceRoot = Join-Path $TerrariaDirectory 'Content/Images'
foreach ($entry in @(@('Head','Armor_Head_1'),@('Body','Armor/Armor_1'),@('Legs','Armor_Legs_1'))) {
    $bitmap = Read-XnbBitmap "$armorSourceRoot/$($entry[1]).xnb"
    $bitmap.Save((Join-Path $PWD ".dev/armor-$($entry[0]).png"), [Drawing.Imaging.ImageFormat]::Png)
    Write-Output "$($entry[0]): $($bitmap.Width)x$($bitmap.Height)"
    $bitmap.Dispose()
}
Add-Type -ReferencedAssemblies @([Drawing.Bitmap].Assembly.Location, [Drawing.Rectangle].Assembly.Location,
    [Reflection.Assembly]::Load('System.Private.Windows.GdiPlus').Location,
    [Reflection.Assembly]::Load('System.Private.Windows.Core').Location) -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
public static class NetheriteArmorBuilder {
    public static void Build(string root) {
        string mc = Path.Combine(root, "MineTerraria/Textures/Minecraft");
        string output = Path.Combine(root, "MineTerraria/Textures/Equipment");
        Directory.CreateDirectory(output);
        using var layer1 = new Bitmap(Path.Combine(mc, "netherite_layer_1.png"));
        using var layer2 = new Bitmap(Path.Combine(mc, "netherite_layer_2.png"));
        foreach (var icon in new[] { ("netherite_pickaxe", "HigherDimensionalPickaxe"), ("netherite_helmet", "HigherDimensionalHelmet"),
            ("netherite_chestplate", "HigherDimensionalChestplate"), ("netherite_leggings", "HigherDimensionalLeggings") }) {
            using var input = new Bitmap(Path.Combine(mc, icon.Item1 + ".png"));
            using var scaled = new Bitmap(32, 32);
            for (int y=0; y<32; y++) for (int x=0; x<32; x++) scaled.SetPixel(x,y,input.GetPixel(x/2,y/2));
            scaled.Save(Path.Combine(output, icon.Item2 + ".png"), ImageFormat.Png);
        }
        foreach (var part in new[] { ("Head", "HigherDimensionalHelmet"), ("Body", "HigherDimensionalChestplate"), ("Legs", "HigherDimensionalLeggings") }) {
            // Vanilla masks provide engine-aligned male/female/walk/attack poses.
            // All visible colors come from original Minecraft armor UV regions.
            using var mask = new Bitmap(Path.Combine(root, ".dev/armor-" + part.Item1 + ".png"));
            using var target = new Bitmap(mask.Width, mask.Height);
            for (int fy=0; fy<mask.Height/56; fy++) for (int fx=0; fx<mask.Width/40; fx++) {
                int minX=40, minY=56, maxX=-1, maxY=-1;
                for (int y=0; y<56; y++) for (int x=0; x<40; x++) if(mask.GetPixel(fx*40+x,fy*56+y).A>0) {
                    minX=Math.Min(minX,x); minY=Math.Min(minY,y); maxX=Math.Max(maxX,x); maxY=Math.Max(maxY,y);
                }
                if(maxX<0) continue;
                for (int y=minY; y<=maxY; y++) for (int x=minX; x<=maxX; x++) {
                    byte alpha=mask.GetPixel(fx*40+x,fy*56+y).A;
                    if(alpha==0 && part.Item1!="Head") continue;
                    if(part.Item1=="Head") alpha=255;
                    float u=(x-minX)/(float)(maxX-minX+1), v=(y-minY)/(float)(maxY-minY+1);
                    Color color;
                    if(part.Item1=="Head") color=Helmet(layer1,u,v);
                    else if(part.Item1=="Legs") {
                        // Each leg samples the same front UV. The lower third is boots.
                        u=(u*2)%1;
                        color=v>=0.65f ? Sample(layer1,4,28,4,4,u,(v-0.65f)/0.35f) : Sample(layer2,4,20,4,12,u,v);
                    } else if(fx<=1) color=Sample(layer1,20,20,8,12,u,v);
                    else color=Sample(layer1,44,20,4,12,u,v);
                    target.SetPixel(fx*40+x,fy*56+y,Color.FromArgb(alpha*color.A/255,color.R,color.G,color.B));
                }
            }
            target.Save(Path.Combine(output, part.Item2 + "_" + part.Item1 + ".png"), ImageFormat.Png);
        }
        using var preview=new Bitmap(560,192);
        using var g=Graphics.FromImage(preview);
        g.Clear(Color.FromArgb(80,105,125));
        foreach(var entry in new[]{("HigherDimensionalHelmet_Head",0), ("HigherDimensionalChestplate_Body",0), ("HigherDimensionalLeggings_Legs",0)}) {
            using var img=new Bitmap(Path.Combine(output,entry.Item1+".png"));
            for(int y=0;y<56;y++) for(int x=0;x<40;x++) {
                Color c=img.GetPixel(x,y);
                if(c.A==0) continue;
                using var brush=new SolidBrush(c);
                g.FillRectangle(brush,16+x*3,12+y*3,3,3);
            }
        }
        using(var body=new Bitmap(Path.Combine(output,"HigherDimensionalChestplate_Body.png"))) {
            g.DrawImageUnscaled(body,176,0);
        }
        preview.Save(Path.Combine(root,".dev/projection-armor-preview.png"),ImageFormat.Png);
        BuildSteve(root,output,layer1,layer2);
    }
    private static Color Helmet(Bitmap armor,float u,float v) => u<0.72f ? Sample(armor,0,8,8,8,u/0.72f,v) : Sample(armor,8,8,8,8,(u-0.72f)/0.28f,v);
    private static void BuildSteve(string root,string output,Bitmap armor,Bitmap pants) {
        using var steve=new Bitmap(Path.Combine(root,"MineTerraria/Textures/Steve.png"));
        using var head=new Bitmap(steve.Width,steve.Height);
        using var body=new Bitmap(steve.Width,steve.Height);
        using var legs=new Bitmap(steve.Width,steve.Height);
        Rectangle[] heads={new(94,120,161,141),new(397,120,163,141),new(691,120,160,141),new(1006,120,161,141),
            new(94,716,161,140),new(400,710,161,152),new(708,735,162,136),new(1007,739,161,137)};
        // Shoulder-to-hand axes follow the source Steve poses. Only existing
        // skin pixels are covered, keeping the lower hand and all body edges.
        int[][][] arms={
            new[]{new[]{140,260,142,432},new[]{197,260,199,399}},
            new[]{new[]{470,282,538,394},new[]{434,282,396,403}},
            new[]{new[]{757,284,794,413},new[]{714,280,714,402}},
            new[]{new[]{1073,284,1129,411},new[]{1036,282,1001,405}},
            new[]{new[]{143,872,208,990},new[]{134,872,99,1000}},
            new[]{new[]{453,879,562,920},new[]{420,883,396,933}},
            new[]{new[]{743,887,666,810},new[]{847,877,838,1000}},
            new[]{new[]{1086,894,1211,906},new[]{1049,906,991,981}}};
        for(int frame=0;frame<8;frame++) {
            Rectangle h=heads[frame];
            int x0=frame%4*(steve.Width/4),x1=(frame%4+1)*(steve.Width/4);
            int y0=frame/4*(steve.Height/2),y1=(frame/4+1)*(steve.Height/2),foot=h.Bottom;
            for(int y=h.Bottom;y<y1;y++) for(int x=x0;x<x1;x++) if(steve.GetPixel(x,y).A>=128) foot=Math.Max(foot,y);
            for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++) {
                Color source=steve.GetPixel(x,y);
                if(source.A<8) continue;
                if(h.Contains(x,y)) {
                    Color c=Helmet(armor,(x-h.Left)/(float)h.Width,(y-h.Top)/(float)h.Height);
                    head.SetPixel(x,y,Color.FromArgb(source.A*c.A/255,c.R,c.G,c.B));
                    continue;
                }
                if(y<h.Bottom) continue;
                bool shirt=source.G>source.R*1.25f && source.B>source.R*1.2f && source.G>source.B*0.72f;
                bool trouser=source.B>source.R*1.25f && source.B>source.G*1.15f;
                bool boot=y>h.Bottom+(foot-h.Bottom)*0.6f && Math.Abs(source.R-source.G)<26 && Math.Abs(source.B-source.G)<26;
                float u=((x-h.X)/(float)h.Width)%1; if(u<0) u+=1;
                if(shirt) {
                    float v=Math.Clamp((y-h.Bottom)/(float)(foot-h.Bottom)*2.5f,0,0.999f);
                    Color c=Sample(armor,20,20,8,12,u,v);
                    body.SetPixel(x,y,Color.FromArgb(source.A*c.A/255,c.R,c.G,c.B));
                } else if(source.R>source.G*1.1f && source.G>source.B*1.03f && source.R>70) {
                    foreach(int[] axis in arms[frame]) {
                        float dx=axis[2]-axis[0],dy=axis[3]-axis[1], length=dx*dx+dy*dy;
                        float along=((x-axis[0])*dx+(y-axis[1])*dy)/length;
                        float across=((x-axis[0])*dy-(y-axis[1])*dx)/(float)Math.Sqrt(length);
                        if(along<0 || along>0.72f || Math.Abs(across)>38) continue;
                        Color c=Sample(armor,44,20,4,12,across/76+0.5f,along);
                        body.SetPixel(x,y,Color.FromArgb(source.A*c.A/255,c.R,c.G,c.B));
                        break;
                    }
                } else if(trouser || boot) {
                    float v=Math.Clamp((y-h.Bottom)/(float)(foot-h.Bottom),0,0.999f);
                    Color c=boot || v>0.84f ? Sample(armor,4,28,4,4,u,v) : Sample(pants,4,20,4,12,u,(v-0.38f)/0.62f);
                    legs.SetPixel(x,y,Color.FromArgb(source.A*c.A/255,c.R,c.G,c.B));
                }
            }
        }
        head.Save(Path.Combine(output,"SteveArmor_Head.png"),ImageFormat.Png);
        body.Save(Path.Combine(output,"SteveArmor_Body.png"),ImageFormat.Png);
        legs.Save(Path.Combine(output,"SteveArmor_Legs.png"),ImageFormat.Png);
        using var preview=new Bitmap(steve.Width,steve.Height);
        using(var g=Graphics.FromImage(preview)) {g.Clear(Color.FromArgb(80,105,125));g.DrawImageUnscaled(steve,0,0);g.DrawImageUnscaled(legs,0,0);g.DrawImageUnscaled(body,0,0);g.DrawImageUnscaled(head,0,0);}
        preview.Save(Path.Combine(root,".dev/steve-armor-preview.png"),ImageFormat.Png);
    }
    private static Color Sample(Bitmap image,int x,int y,int w,int h,float u,float v) =>
        image.GetPixel(x+Math.Clamp((int)(u*w),0,w-1),y+Math.Clamp((int)(v*h),0,h-1));
}
'@
[NetheriteArmorBuilder]::Build($PWD.Path)
Write-Output 'Generated native-size item icons and three animated armor sheets.'
