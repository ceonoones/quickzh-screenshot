"""Generate README GIF and video from the application's native demo frames.

Development only: pip install Pillow imageio-ffmpeg
First run src/build.ps1 -Tests, then src/验证.exe --render-demo site/assets.
"""
from pathlib import Path
import json
import os
import subprocess

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
assets = root / 'site/assets'
# Remove only the uniform toolbar panel pixel, keeping the native glyph strokes.
for path in (assets / 'icons').glob('*.png'):
    icon = Image.open(path).convert('RGBA')
    icon.putdata([(r,g,b,0 if (r,g,b)==(29,33,45) else a) for r,g,b,a in icon.getdata()])
    icon.save(path)
frames = [Image.open(p).convert('RGB') for p in sorted((assets / 'frames').glob('*.png'))]
steps = json.loads((assets / 'steps.json').read_text(encoding='utf-8-sig'))
if len(frames) != len(steps):
    raise ValueError('Native frames and steps differ')
font_path = Path(os.environ.get('WINDIR', 'C:/Windows')) / 'Fonts/msyh.ttc'
font = ImageFont.truetype(str(font_path), 20)
small = ImageFont.truetype(str(font_path), 16)
fps = 8
durations = [3, 2.5] + [3.2] * 18 + [3]
points = [(100,180),(1258,619),(760,430),(939,670),(600,270),(650,420),(780,470),(660,411),(1140,450),(780,540),(840,560),(920,580),(1158,467),(1100,430),(250,140),(810,668),(865,668),(705,668),(1045,668),(992,668),(660,450)]
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
video = assets / 'demo.mp4'
command = [ffmpeg, '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '1056x624', '-r', str(fps), '-i', '-', '-an', '-c:v', 'libx264', '-preset', 'medium', '-crf', '23', '-pix_fmt', 'yuv420p', '-map_metadata', '-1', '-movflags', '+faststart', str(video)]
process = subprocess.Popen(command, stdin=subprocess.PIPE)
try:
    for index, native in enumerate(frames):
        count = round(durations[index]*fps)
        for tick in range(count):
            image = native.copy()
            draw = ImageDraw.Draw(image)
            phase = min(1, tick / max(1, count*.65))
            smooth = phase * phase * (3-2*phase)
            prev = points[max(0,index-1)]
            x = prev[0]+(points[index][0]-prev[0])*smooth
            y = prev[1]+(points[index][1]-prev[1])*smooth
            if index == 1:
                # Animate selection of the complete native screenshot.
                draw.rectangle((58,100,1260,622), fill='#142137')
                draw.bitmap((0,0), Image.new('1',(1,1)), fill='white')
                image.paste(frames[0].crop((59,101,1259,621)), (59,101))
                endx,endy=59+int(1200*smooth),101+int(520*smooth)
                draw.rectangle((59,101,endx,endy), outline='#50dcbe', width=3)
                x,y=endx,endy
            if index == 18:
                draw.rounded_rectangle((951,524,1199,630),radius=8,fill='white',outline='#cdd8d0',width=2)
                draw.text((983,539),'复制原文',font=font,fill='#20392b')
                draw.text((983,582),'复制译文',font=font,fill='#20392b')
            if index == 19:
                draw.rounded_rectangle((690,557,1207,612),radius=8,fill='#effbf3',outline='#95c5ab',width=2)
                draw.text((707,572),'成功后自动关闭（此处为行为演示）',font=small,fill='#17543c')
            if index == 20:
                draw.rectangle((58,100,1261,701),fill='#e5eee8')
                draw.text((483,330),'本次截图已结束',font=font,fill='#146859')
                draw.text((398,379),'再按 Ctrl + Alt + Q 开始下一次截图',font=font,fill='#657c6c')
            else:
                draw.polygon([(x+2,y+2),(x+6,y+29),(x+14,y+20),(x+23,y+31),(x+28,y+27),(x+18,y+16),(x+32,y+15)],fill='#0007')
                draw.polygon([(x,y),(x+4,y+27),(x+12,y+18),(x+21,y+29),(x+26,y+25),(x+16,y+14),(x+30,y+13)],fill='white',outline='#283c3c')
                if .65 < phase < .95:
                    radius = 12 + int(phase*8)
                    draw.ellipse((x-radius,y-radius,x+radius,y+radius),outline='#50dcbe',width=3)
            process.stdin.write(image.resize((1056,624),Image.Resampling.LANCZOS).tobytes())
finally:
    process.stdin.close()
if process.wait() != 0:
    raise RuntimeError('Video generation failed')
subprocess.run([ffmpeg,'-y','-loglevel','error','-i',str(video),'-filter_complex','fps=5,scale=792:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer:bayer_scale=4','-map_metadata','-1','-loop','0',str(assets/'demo.gif')],check=True)
print('Generated native-frame GIF and MP4:', sum(durations), 'seconds')
