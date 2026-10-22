"""Rebuild original File Voyager vector sources and multi-resolution Windows icons."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import html
import math

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'source/assets/file-voyager'
OUT.mkdir(parents=True, exist_ok=True)
SCALE = 4

class Vector:
    def __init__(self):
        self.image = Image.new('RGBA', (1024, 1024))
        self.draw = ImageDraw.Draw(self.image)
        self.svg = []

    def rect(self, box, color, radius=0):
        x, y, xx, yy = box
        self.draw.rounded_rectangle(tuple(round(v*SCALE) for v in box), radius=radius*SCALE, fill=color)
        self.svg.append(f'<rect x="{x}" y="{y}" width="{xx-x}" height="{yy-y}" rx="{radius}" fill="{color}"/>')

    def circle(self, x, y, r, color):
        self.draw.ellipse(tuple(round(v*SCALE) for v in (x-r,y-r,x+r,y+r)), fill=color)
        self.svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{color}"/>')

    def polygon(self, points, color):
        self.draw.polygon([(round(x*SCALE),round(y*SCALE)) for x,y in points], fill=color)
        self.svg.append('<polygon points="'+' '.join(f'{x},{y}' for x,y in points)+f'" fill="{color}"/>')

    def line(self, points, color, width):
        self.draw.line([(round(x*SCALE),round(y*SCALE)) for x,y in points], fill=color, width=round(width*SCALE), joint='curve')
        for x,y in (points[0],points[-1]):
            self.draw.ellipse(tuple(round(v*SCALE) for v in (x-width/2,y-width/2,x+width/2,y+width/2)),fill=color)
        self.svg.append('<polyline points="'+' '.join(f'{x},{y}' for x,y in points)+f'" fill="none" stroke="{color}" stroke-width="{width}" stroke-linejoin="round" stroke-linecap="round"/>')

def folder(v):
    v.rect((17,48,113,105),'#D99112',13)
    v.rect((17,67,231,210),'#D99112',15)
    v.rect((26,76,222,188),'#FFF2C6',8)
    v.rect((17,99,231,216),'#F3B321',15)
    v.rect((17,99,231,199),'#FFCC48',15)
    v.rect((27,109,219,116),'#FFE28A',3)

def compass(v,x,y,r,dark=False):
    v.circle(x,y+3,r+3,'#A56F15' if not dark else '#102B43')
    v.circle(x,y,r,'#FFFFFF' if not dark else '#F4CE70')
    v.circle(x,y,r-6,'#126FA7' if not dark else '#16394F')
    v.circle(x,y,r-12,'#168CC2' if not dark else '#20526B')
    for dx,dy in ((0,-1),(1,0),(0,1),(-1,0)):
        v.circle(x+dx*(r-11),y+dy*(r-11),2,'#CDEFFF')
    v.polygon([(x+19,y-30),(x+9,y+5),(x-6,y-10)],'#FF715C' if not dark else '#56E0D0')
    v.polygon([(x-19,y+30),(x-9,y-5),(x+6,y+10)],'#FFFFFF')
    v.circle(x,y,5,'#FFFFFF' if not dark else '#F4CE70')

icons = []
v=Vector(); folder(v); compass(v,183,170,53); icons.append(('FileVoyager-Compass','Compass',v))
v=Vector(); v.rect((15,25,241,231),'#155D92',27); v.rect((25,35,231,221),'#197DB6',20)
v.rect((36,57,120,203),'#F5FCFF',11); v.rect((132,57,216,203),'#FFCF54',11)
v.rect((36,57,120,84),'#C4ECFB',10); v.rect((132,57,216,84),'#FFE69A',10)
for y in (103,128,153,178):
    v.rect((48,y,61,y+10),'#27A6CF',3); v.rect((69,y+2,106,y+8),'#8BC6DD',2)
    v.rect((144,y,157,y+10),'#B77B12',3); v.rect((165,y+2,202,y+8),'#D6A039',2)
icons.append(('FileVoyager-Panels','Dual panels',v))
v=Vector(); folder(v); v.circle(183,170,56,'#FFFFFF'); v.circle(183,170,49,'#048E88')
v.line([(157,186),(174,169),(192,180),(211,151)],'#FFFFFF',7)
for x,y in ((157,186),(174,169),(192,180),(211,151)): v.circle(x,y,6,'#D2FFF3')
v.polygon([(211,145),(214,162),(199,152)],'#D2FFF3'); icons.append(('FileVoyager-Route','Route',v))
v=Vector(); v.rect((15,15,241,241),'#142F48',48); v.rect((23,23,233,233),'#1C405B',41)
compass(v,128,128,91,True); icons.append(('FileVoyager-Night','Night compass',v))

def floppy(dark=False, label=True):
    v = Vector()
    v.polygon([(31,17),(201,17),(235,51),(235,226),(224,238),(31,238),(19,226),(19,30)], '#0C285F' if dark else '#0753B5')
    v.polygon([(33,21),(198,21),(230,54),(230,220),(219,231),(33,231),(25,222),(25,33)], '#143B80' if dark else '#0879E5')
    v.line([(32,42),(32,216)], '#25519D' if dark else '#169BFF', 3)
    v.rect((64,19,188,87),'#092D68',6)
    v.rect((69,19,185,82),'#C5D3E2',5)
    v.rect((73,21,181,27),'#EFF5FB',2)
    v.rect((143,29,169,74),'#0D326D',3)
    if label:
        v.rect((43,103,212,232),'#D8E7F2',10)
        v.rect((45,104,210,226),'#F4FAFF',9)
    return v

v=floppy()
# A continuous voyage between two points; broad enough to survive small sizes.
points=[]
for i in range(65):
    t=i/64; u=1-t
    points.append((u*u*u*68+3*u*u*t*153+3*u*t*t*112+t*t*t*187,
                   u*u*u*195+3*u*u*t*209+3*u*t*t*123+t*t*t*131))
v.line(points,'#EB860B',12)
v.circle(68,195,13,'#F59A17'); v.circle(68,192,9,'#FFB52A')
v.circle(187,131,13,'#F59A17'); v.circle(187,128,9,'#FFB52A')
icons.append(('FileVoyager-FloppyRoute','Disketa – Trasa',v))

v=floppy(True,False)
points=[]
for i in range(97):
    a=2*math.pi*i/96
    x=85*math.cos(a); y=33*math.sin(a)
    points.append((128+x*.92+y*.39,163-x*.39+y*.92))
v.line(points,'#05BBCD',10)
v.polygon([(91,169),(168,118),(143,197),(126,174)],'#FAFDFF')
v.polygon([(126,174),(168,118),(136,167)],'#C2E3F8')
v.circle(207,142,13,'#047C9F'); v.circle(207,139,11,'#25D9D2')
icons.append(('FileVoyager-FloppyOrbit','Disketa – Orbita',v))

v=floppy()
v.line([(67,155),(92,155)],'#A0E3E9',7)
v.line([(60,177),(87,177)],'#6BD0D9',7)
v.line([(70,198),(92,198)],'#A0E3E9',7)
v.polygon([(104,157),(148,157),(148,134),(193,176),(148,216),(148,194),(104,194)],'#009AA5')
v.polygon([(106,155),(150,155),(150,132),(195,174),(150,214),(150,192),(106,192)],'#12BEC3')
icons.append(('FileVoyager-FloppyClassic','Disketa – Klasik',v))

sizes=[(n,n) for n in (16,20,24,32,40,48,64,96,128,256)]
for name,label,v in icons:
    image=v.image.resize((256,256),Image.Resampling.LANCZOS)
    image.save(OUT/(name+'.png'))
    image.save(OUT/(name+'.ico'),sizes=sizes)
    (OUT/(name+'.svg')).write_text('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><title>File Voyager — '+html.escape(label)+'</title>'+''.join(v.svg)+'</svg>\n',encoding='utf-8')

preview=Image.new('RGB',(1120,830),'#F0F2F5'); d=ImageDraw.Draw(preview)
font_path=Path('C:/Windows/Fonts/segoeui.ttf')
font=ImageFont.truetype(str(font_path),21) if font_path.exists() else ImageFont.load_default()
small=ImageFont.truetype(str(font_path),15) if font_path.exists() else font
for i,(name,label,v) in enumerate(icons):
    x=20+(i%4)*275
    offset=(i//4)*410
    d.rounded_rectangle((x,20+offset,x+255,400+offset),radius=16,fill='white',outline='#D5DCE3')
    image=v.image.resize((180,180),Image.Resampling.LANCZOS); preview.paste(image,(x+38,46+offset),image)
    d.text((x+127,244+offset),label,font=font,fill='#20374A',anchor='mm')
    cursor=x+22
    for size in (16,24,32,48):
        image=v.image.resize((size,size),Image.Resampling.LANCZOS); preview.paste(image,(cursor,294+offset+(48-size)//2),image); cursor+=size+15
    d.text((x+127,370+offset),'16 / 24 / 32 / 48 px',font=small,fill='#5B6B7B',anchor='mm')
preview.save(OUT/'FileVoyager-Preview.png')
print('Created seven SVG, PNG and multi-resolution ICO variants.')
