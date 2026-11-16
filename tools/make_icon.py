import sys, io, struct
sys.path.insert(0, sys.argv[1])
from icon_shapes import *
from PIL import Image, ImageDraw, ImageChops

def small(level):
    # level 0: 16~20px (손잡이에 점 하나), 1: 24~32px (점 + 음파 하나)
    img=Image.new('RGBA',(S,S),(0,0,0,0))
    sq=mask_from(lambda d:d.polygon(squircle(n=4.5,pad=8),fill=255))
    img=paste(img, vgrad(H('34312A'),H('171612')), sq)
    track=mask_from(lambda d:d.rounded_rectangle([90,272,934,752],240,fill=255))
    img=paste(img, dgrad(H('FFE36E'),H('FFB800')), track)
    knob=mask_from(lambda d:d.ellipse([494,312,894,712],fill=255))
    img=paste(img, Image.new('RGBA',(S,S),H('1B1A16')+(255,)), knob)
    w=Image.new('L',(S,S),0)
    if level==0:
        w=mask_from(lambda d:d.ellipse([629,447,759,577],fill=255))
    else:
        w=ImageChops.lighter(mask_from(lambda d:d.ellipse([590,462,690,562],fill=255)), arc_mask(620,512,150,64,-45,45))
    img=paste(img, Image.new('RGBA',(S,S),H('FFD43B')+(255,)), w)
    return img

big=concept_b(); s0=small(0); s1=small(1)
sizes=[16,20,24,32,40,48,64,96,128,256]
frames=[]
for z in sizes:
    src = s0 if z<=20 else (s1 if z<=32 else big)
    frames.append(src.resize((z,z),Image.LANCZOS))
def dib(im):
    w,h=im.size; px=im.load()
    hdr=struct.pack('<IiiHHIIiiII',40,w,h*2,1,32,0,0,0,0,0,0)
    rows=b''.join(bytes(c for x in range(w) for c in (px[x,y][2],px[x,y][1],px[x,y][0],px[x,y][3])) for y in range(h-1,-1,-1))
    stride=((w+31)//32)*4
    return hdr+rows+b'\0'*(stride*h)
# ICO (작은 크기 BMP, 256은 PNG)
entries=[]; data=b''; off=6+16*len(frames)
for im in frames:
    if im.size[0]>=256:
        b=io.BytesIO(); im.save(b,'PNG'); p=b.getvalue()
    else:
        p=dib(im)   # 작은 크기는 오래된 API에서도 읽히도록 BMP 형식
    z=im.size[0]; entries.append(struct.pack('<BBBBHHII', z%256, z%256, 0,0,1,32,len(p),off)); off+=len(p); data+=p
open(sys.argv[2],'wb').write(struct.pack('<HHH',0,1,len(frames))+b''.join(entries)+data)
big.resize((256,256),Image.LANCZOS).save(sys.argv[3])
sheet=Image.new('RGBA',(620,140),(236,234,228,255)); x=10
for im in frames[::-1][:0]+frames:
    sheet.alpha_composite(im,(x,10)) if im.size[0]<=128 else None; x+= im.size[0]+10 if im.size[0]<=128 else 0
sheet.save(sys.argv[4])
