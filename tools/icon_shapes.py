import math, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops
S = 1024
def squircle(n=5.0, pad=40, s=S):
    a = (s - 2*pad)/2; c = s/2; pts=[]
    for i in range(720):
        t = 2*math.pi*i/720; ct, st = math.cos(t), math.sin(t)
        x = c + a*math.copysign(abs(ct)**(2/n), ct); y = c + a*math.copysign(abs(st)**(2/n), st)
        pts.append((x,y))
    return pts
def mask_from(fn):
    m = Image.new('L',(S,S),0); fn(ImageDraw.Draw(m)); return m
def vgrad(c1,c2):
    g = Image.new('RGBA',(S,S)); d=ImageDraw.Draw(g)
    for y in range(S):
        t=y/(S-1); d.line([(0,y),(S,y)], fill=tuple(int(c1[i]+(c2[i]-c1[i])*t) for i in range(3))+(255,))
    return g
def dgrad(c1,c2):
    g = Image.new('RGBA',(S,S)); px=g.load()
    small = Image.new('RGBA',(64,64)); sp=small.load()
    for y in range(64):
        for x in range(64):
            t=(x+y)/126; sp[x,y]=tuple(int(c1[i]+(c2[i]-c1[i])*t) for i in range(3))+(255,)
    return small.resize((S,S), Image.BILINEAR)
def H(h): h=h.lstrip('#'); return tuple(int(h[i:i+2],16) for i in (0,2,4))
def paste(base, layer, m): base.paste(layer,(0,0),m); return base
def arc_mask(cx,cy,r,w,a0,a1):
    m=Image.new('L',(S,S),0); d=ImageDraw.Draw(m)
    d.arc([cx-r,cy-r,cx+r,cy+r],a0,a1,fill=255,width=int(w))
    for a in (a0,a1):  # round caps
        rr=r-w/2; x=cx+rr*math.cos(math.radians(a)); y=cy+rr*math.sin(math.radians(a))
        d.ellipse([x-w/2,y-w/2,x+w/2,y+w/2],fill=255)
    return m
def rrect(d,box,r,fill=255): d.rounded_rectangle(box,r,fill=fill)
def shadow(m, off=(0,18), blur=28, alpha=110):
    sh=m.filter(ImageFilter.GaussianBlur(blur)).point(lambda v:v*alpha//255)
    o=Image.new('L',(S,S),0); o.paste(sh,off); return o

def concept_a():
    # 노랑 스퀘어클 + 진한 스피커와 음파
    img=Image.new('RGBA',(S,S),(0,0,0,0))
    sq=mask_from(lambda d:d.polygon(squircle(),fill=255))
    img=paste(img, Image.new('RGBA',(S,S),(0,0,0,255)), shadow(sq,(0,14),24,90))
    img=paste(img, dgrad(H('FFE36E'),H('FFB800')), sq)
    hl=mask_from(lambda d:d.polygon(squircle(pad=40),fill=255)); inner=mask_from(lambda d:d.polygon(squircle(pad=52),fill=255))
    edge=ImageChops.subtract(hl,inner); top=vgrad((255,255,255),(255,255,255)); 
    img=paste(img, top, edge.point(lambda v:v*70//255))
    ink=Image.new('RGBA',(S,S),H('1B1A16')+(255,))
    g=mask_from(lambda d:(rrect(d,[200,400,330,624],40), d.polygon([(300,420),(470,280),(470,744),(300,604)],fill=255),))
    g=g.filter(ImageFilter.GaussianBlur(1))
    g=ImageChops.lighter(g, mask_from(lambda d:d.rounded_rectangle([420,262,500,762],40,fill=255)))
    for r in (150,260):
        g=ImageChops.lighter(g, arc_mask(460,512,r+40,62,-48,48))
    img=paste(img, ink, g)
    return img

def concept_b():
    # 다크 스퀘어클 + 노랑 토글 스위치, 손잡이 안에 음파
    img=Image.new('RGBA',(S,S),(0,0,0,0))
    sq=mask_from(lambda d:d.polygon(squircle(),fill=255))
    img=paste(img, Image.new('RGBA',(S,S),(0,0,0,255)), shadow(sq,(0,14),24,90))
    img=paste(img, vgrad(H('34312A'),H('141310')), sq)
    edge=ImageChops.subtract(sq, mask_from(lambda d:d.polygon(squircle(pad=50),fill=255)))
    img=paste(img, Image.new('RGBA',(S,S),(255,255,255,255)), edge.point(lambda v:v*40//255))
    track=mask_from(lambda d:d.rounded_rectangle([170,322,854,702],190,fill=255))
    img=paste(img, dgrad(H('FFE36E'),H('FFB800')), track)
    knob=mask_from(lambda d:d.ellipse([494,352,824,682],fill=255))
    img=paste(img, Image.new('RGBA',(S,S),(0,0,0,255)), shadow(knob,(0,10),18,120))
    img=paste(img, vgrad(H('2A2822'),H('171612')), knob)
    # 손잡이 안 음파 (노랑)
    w=Image.new('L',(S,S),0)
    for r,a in ((70,40),(130,45)):
        w=ImageChops.lighter(w, arc_mask(600,517,r,40,-a,a))
    w=ImageChops.lighter(w, mask_from(lambda d:d.ellipse([560,477,616,557],fill=255)))
    img=paste(img, Image.new('RGBA',(S,S),H('FFD43B')+(255,)), w)
    # 트랙 왼쪽 점 2개 (진한)
    dots=mask_from(lambda d:(d.ellipse([272,487,332,547],fill=255), d.ellipse([370,487,430,547],fill=255)))
    img=paste(img, Image.new('RGBA',(S,S),H('1B1A16')+(150,)), dots.point(lambda v:v*150//255))
    return img

def concept_c():
    # 노랑 스퀘어클 + 헤드폰 + 전환 화살표
    img=Image.new('RGBA',(S,S),(0,0,0,0))
    sq=mask_from(lambda d:d.polygon(squircle(),fill=255))
    img=paste(img, Image.new('RGBA',(S,S),(0,0,0,255)), shadow(sq,(0,14),24,90))
    img=paste(img, vgrad(H('2C2A24'),H('141310')), sq)
    edge=ImageChops.subtract(sq, mask_from(lambda d:d.polygon(squircle(pad=50),fill=255)))
    img=paste(img, Image.new('RGBA',(S,S),(255,255,255,255)), edge.point(lambda v:v*40//255))
    y=Image.new('L',(S,S),0)
    y=ImageChops.lighter(y, arc_mask(512,560,300,70,180,360))
    y=ImageChops.lighter(y, mask_from(lambda d:(d.rounded_rectangle([196,540,336,790],60,fill=255), d.rounded_rectangle([688,540,828,790],60,fill=255))))
    img=paste(img, dgrad(H('FFE36E'),H('FFB800')), y)
    # 가운데 전환 화살표
    a=Image.new('L',(S,S),0); d=ImageDraw.Draw(a)
    d.rounded_rectangle([400,560,600,600],20,fill=255); d.polygon([(590,520),(660,580),(590,640)],fill=255)
    d.rounded_rectangle([424,690,624,730],20,fill=255); d.polygon([(434,650),(364,710),(434,770)],fill=255)
    img=paste(img, Image.new('RGBA',(S,S),(255,255,255,255)), a)
    return img

if __name__=='__main__':
    out=sys.argv[1]
    ims=[concept_a(),concept_b(),concept_c()]
    sheet=Image.new('RGBA',(3*300+40, 300+120),(236,234,228,255))
    dark=Image.new('RGBA',(3*300+40, 300+120),(30,30,30,255))
    for i,im in enumerate(ims):
        im.save(f'{out}/concept_{"abc"[i]}.png')
        sheet.alpha_composite(im.resize((256,256),Image.LANCZOS),(20+i*300,20))
        for j,sz in enumerate((48,32,16)):
            sheet.alpha_composite(im.resize((sz,sz),Image.LANCZOS),(20+i*300+ [0,60,104][j],300))
    sheet.save(f'{out}/sheet.png')
