"""Background-remove the flat VoidPet originals into transparent billboard sprites.
Not generation — a local color key: near-bg gray -> transparent, with
(a) exterior flood-fill so interior white eyes are NOT punched out, and
(b) bottom text-label band dropped."""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np, glob, os
from collections import deque

def exterior_mask(bgmask_bool):
    """True where a background pixel is reachable from the border (i.e. NOT an interior hole)."""
    h,w = bgmask_bool.shape
    ext = np.zeros((h,w), bool)
    dq = deque()
    for x in range(w):
        for y in (0,h-1):
            if bgmask_bool[y,x] and not ext[y,x]: ext[y,x]=True; dq.append((y,x))
    for y in range(h):
        for x in (0,w-1):
            if bgmask_bool[y,x] and not ext[y,x]: ext[y,x]=True; dq.append((y,x))
    while dq:
        y,x = dq.popleft()
        for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
            ny,nx = y+dy, x+dx
            if 0<=ny<h and 0<=nx<w and bgmask_bool[ny,nx] and not ext[ny,nx]:
                ext[ny,nx]=True; dq.append((ny,nx))
    return ext

SRC = "references/voidpet-ip"
OUT = os.path.join(SRC, "cutouts")
os.makedirs(OUT, exist_ok=True)

def creature_band_bottom(lum):
    h = lum.shape[0]
    hasink = np.array([(lum[r] < 120).mean() > 0.008 for r in range(h)])
    s=None; bands=[]
    for r in range(h):
        if hasink[r] and s is None: s=r
        if (not hasink[r]) and s is not None: bands.append((s,r-1)); s=None
    if s is not None: bands.append((s,h-1))
    return bands[0][1] if bands else h-1   # bottom of the FIRST (creature) band

results=[]
for f in sorted(glob.glob(os.path.join(SRC,"*.png"))):
    name=os.path.basename(f)
    if "base_color" in name or "sheet" in name: continue
    rgb = np.asarray(Image.open(f).convert("RGB")).astype(np.int16)
    h,w = rgb.shape[:2]
    # bg = median of the four 8x8 corner patches
    corners = np.concatenate([rgb[:8,:8], rgb[:8,-8:], rgb[-8:,:8], rgb[-8:,-8:]]).reshape(-1,3)
    bg = np.median(corners,0)
    dist = np.sqrt(((rgb-bg)**2).sum(2))          # per-pixel distance from bg
    lum = rgb.mean(2)

    # --- drop the bottom text-label band: everything below the creature band is bg ---
    cb = creature_band_bottom(lum)
    label_cut = cb + 2

    # --- choose threshold above the drop-shadow: sample the strip just under the creature ---
    strip = dist[max(cb-6,0):min(cb+8,h), w//4:3*w//4]
    shadow_p = np.percentile(strip, 98) if strip.size else 40
    T = float(np.clip(shadow_p + 8, 55, 95))       # bg/shadow < T ; creature ink >> T

    bgmask = (dist < T)                              # True = background-ish (incl shadow + eye-white)
    exterior = exterior_mask(bgmask)                 # bg reachable from the border
    interior_bg = bgmask & ~exterior                 # enclosed holes: eyes AND donut-hole bg
    eye = interior_bg & (rgb.min(2) > 240)           # near-white -> a real feature (eye), keep it
    remove = exterior | (interior_bg & ~eye)         # transparent: outside bg + enclosed *gray* bg

    alpha = np.where(remove, 0, 255).astype(np.uint8)
    alpha[label_cut:,:] = 0                          # kill label band regardless
    # 1px feather so the crisp edge isn't jagged when scaled on a quad
    alpha = np.asarray(Image.fromarray(alpha,"L").filter(ImageFilter.GaussianBlur(0.6)))

    out = np.dstack([rgb.astype(np.uint8), alpha])
    im = Image.fromarray(out,"RGBA")
    # crop to content + 6px transparent margin
    bbox = im.getchannel("A").point(lambda v:255 if v>8 else 0).getbbox()
    if bbox:
        x0,y0,x1,y1 = bbox
        pad=6
        im = im.crop((max(x0-pad,0),max(y0-pad,0),min(x1+pad,w),min(y1+pad,h)))
    dst = os.path.join(OUT, name.replace("pet-","").replace(".png","")+"-cut.png")
    im.save(dst)
    had_label = label_cut < h-1
    results.append((name, im.size, round(T,1), had_label))
    print(f"{name:20s} -> {os.path.basename(dst):18s} {im.size}  T={T:.1f}  label_dropped={had_label}")

# ---- verification contact sheet: each cutout on checkerboard + on a grassy green ----
cuts = sorted(glob.glob(os.path.join(OUT,"*-cut.png")))
cell=200; cols=len(cuts)
def checker(size,c1=(150,150,150),c2=(200,200,200),s=16):
    im=Image.new("RGB",(size,size),c1); d=ImageDraw.Draw(im)
    for y in range(0,size,s):
        for x in range(0,size,s):
            if (x//s+y//s)%2: d.rectangle([x,y,x+s,y+s],fill=c2)
    return im
sheet=Image.new("RGB",(cell*cols, cell*2),(255,255,255))
green=Image.new("RGB",(cell,cell),(96,150,74))
for i,c in enumerate(cuts):
    sp=Image.open(c).convert("RGBA")
    sp.thumbnail((cell-24,cell-24))
    for row,bgtile in enumerate([checker(cell), green.copy()]):
        tile=bgtile.copy()
        ox=(cell-sp.width)//2; oy=(cell-sp.height)//2
        tile.paste(sp,(ox,oy),sp)
        sheet.paste(tile,(i*cell, row*cell))
sheet.save(os.path.join(OUT,"_contact.png"))
print("contact:", os.path.join(OUT,"_contact.png"))
