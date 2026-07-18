"""Reusable asset-preview generator. Composites transparent/dark image assets onto a
checkerboard + a scene tone so they're legible, writes one preview per asset plus a
combined contact sheet. Usage: make_previews.py <assets_glob> <out_dir> [label]"""
from PIL import Image, ImageDraw
import numpy as np, glob, os, sys

srcglob = sys.argv[1] if len(sys.argv)>1 else "references/voidpet-ip/cutouts/*-cut.png"
outdir  = sys.argv[2] if len(sys.argv)>2 else "references/voidpet-ip/cutouts/previews"
os.makedirs(outdir, exist_ok=True)
CELL=220; SCENE=(96,150,74)

def checker(size,c1=(148,148,148),c2=(200,200,200),s=16):
    im=Image.new("RGB",(size,size),c1); d=ImageDraw.Draw(im)
    for y in range(0,size,s):
        for x in range(0,size,s):
            if (x//s+y//s)%2: d.rectangle([x,y,x+s,y+s],fill=c2)
    return im

def tile(sp,bg):
    t=bg.copy(); sp=sp.copy(); sp.thumbnail((CELL-28,CELL-28))
    t.paste(sp,((CELL-sp.width)//2,(CELL-sp.height)//2),sp); return t

assets=sorted(f for f in glob.glob(srcglob) if "previews" not in f)
labels=[]
for f in assets:
    name=os.path.splitext(os.path.basename(f))[0]
    sp=Image.open(f).convert("RGBA")
    pv=Image.new("RGB",(CELL*2,CELL),(255,255,255))
    pv.paste(tile(sp,checker(CELL)),(0,0))
    pv.paste(tile(sp,Image.new("RGB",(CELL,CELL),SCENE)),(CELL,0))
    pv.save(os.path.join(outdir,name+"-preview.png"))
    labels.append(name)

# combined contact sheet: checker row + scene row
n=len(assets)
sheet=Image.new("RGB",(CELL*n, CELL*2),(255,255,255))
for i,f in enumerate(assets):
    sp=Image.open(f).convert("RGBA")
    sheet.paste(tile(sp,checker(CELL)),(i*CELL,0))
    sheet.paste(tile(sp,Image.new("RGB",(CELL,CELL),SCENE)),(i*CELL,CELL))
contact=os.path.join(outdir,"_contact.png")
sheet.save(contact)
print(f"{n} assets -> {outdir}")
print("per-asset:", ", ".join(labels))
print("contact:", contact)
