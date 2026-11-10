"""Draws the 8 seasonal item icons (16x16) into assets/seasonal/. Run from the Content folder: python3 source/build_seasonal.py"""
from PIL import Image, ImageDraw
import os

OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "seasonal")
os.makedirs(OUT, exist_ok=True)

OL = (59, 42, 26, 255)          # outline
WHITE = (250, 246, 240, 255)
SHADE = (0, 0, 0, 40)

def new():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)

def px(d, pts, c):
    for x, y in pts:
        d.point((x, y), fill=c)

def cup(d, liquid, lid=WHITE, straw=None):
    # tapered cup, x 4..11, y 5..14
    for y in range(5, 15):
        inset = (y - 5) // 4          # narrows toward the bottom
        d.line([(4 + inset, y), (11 - inset, y)], fill=liquid)
        d.point((4 + inset, y), fill=OL)
        d.point((11 - inset, y), fill=OL)
    d.line([(6, 14), (9, 14)], fill=OL)
    d.rectangle([3, 3, 12, 5], fill=lid, outline=OL)   # lid
    if straw:
        d.line([(9, 0), (9, 3)], fill=straw)
        d.point((8, 0), fill=straw)

def lemonade():
    img, d = new()
    cup(d, (232, 84, 120, 255), straw=(255, 255, 255, 255))
    d.ellipse([10, 3, 14, 7], fill=(255, 226, 80, 255), outline=OL)   # lemon slice on rim
    d.point((12, 5), fill=(255, 250, 200, 255))
    d.line([(6, 8), (6, 11)], fill=(255, 255, 255, 110))
    return img

def garden_wrap():
    img, d = new()
    d.polygon([(3, 12), (4, 6), (11, 3), (13, 9), (7, 13)], fill=(232, 205, 150, 255), outline=OL)
    d.polygon([(3, 12), (7, 13), (6, 15), (2, 14)], fill=(232, 205, 150, 255), outline=OL)
    px(d, [(5, 6), (6, 5), (7, 4)], (96, 176, 70, 255))               # lettuce peeking out
    px(d, [(8, 4), (9, 3), (10, 3), (11, 4)], (96, 176, 70, 255))
    px(d, [(9, 5), (10, 6)], (224, 70, 60, 255))                      # tomato
    d.line([(6, 9), (10, 10)], fill=(200, 165, 110, 255))            # fold line
    return img

def slushie():
    img, d = new()
    cup(d, (70, 190, 245, 255), lid=(130, 215, 250, 255), straw=(230, 60, 70, 255))
    d.ellipse([4, 1, 11, 5], fill=(190, 235, 255, 255), outline=OL)  # frozen dome
    d.point((6, 2), fill=WHITE); d.point((7, 3), fill=WHITE)
    d.line([(6, 8), (6, 12)], fill=(255, 255, 255, 110))
    return img

def corn_cup():
    img, d = new()
    cup(d, (250, 205, 60, 255), lid=(250, 215, 90, 255))
    for x, y in [(5, 7), (7, 7), (9, 7), (6, 9), (8, 9), (10, 9), (7, 11), (9, 11)]:
        d.point((x, y), fill=(255, 240, 150, 255))                    # kernels
    d.rectangle([4, 1, 11, 4], fill=(250, 215, 90, 255), outline=OL)   # corn heaped on top
    for x in (5, 7, 9):
        d.point((x, 2), fill=(255, 240, 150, 255))
    d.line([(5, 3), (10, 3)], fill=(200, 140, 40, 255))              # grill marks
    return img

def pumpkin_shake():
    img, d = new()
    cup(d, (236, 140, 44, 255), lid=(255, 230, 190, 255), straw=(120, 70, 30, 255))
    d.ellipse([4, 1, 11, 5], fill=WHITE, outline=OL)                  # whipped cream
    px(d, [(7, 2), (8, 3)], (226, 150, 70, 255))                      # cinnamon dust
    d.line([(6, 8), (6, 12)], fill=(255, 255, 255, 90))
    return img

def apple_pie():
    img, d = new()
    d.polygon([(2, 12), (8, 4), (14, 12)], fill=(222, 170, 95, 255), outline=OL)   # slice from above
    d.rectangle([2, 12, 14, 14], fill=(222, 170, 95, 255), outline=OL)
    d.polygon([(5, 11), (8, 7), (11, 11)], fill=(196, 62, 52, 255))               # apple filling
    px(d, [(8, 9), (7, 10), (9, 10)], (240, 120, 90, 255))
    d.line([(3, 13), (13, 13)], fill=(180, 125, 60, 255))                         # crust shadow
    px(d, [(8, 5)], (250, 230, 190, 255))
    return img

def hot_cocoa():
    img, d = new()
    d.rectangle([3, 5, 11, 13], fill=WHITE, outline=OL)                          # mug
    d.rectangle([4, 6, 10, 8], fill=(110, 62, 38, 255))                          # cocoa
    d.arc([10, 6, 15, 12], 270, 90, fill=OL)                                      # handle
    d.point((14, 9), fill=OL)
    d.ellipse([4, 4, 7, 7], fill=WHITE, outline=OL)                               # marshmallows
    d.ellipse([7, 4, 10, 7], fill=(255, 235, 235, 255), outline=OL)
    d.line([(4, 11), (10, 11)], fill=(214, 70, 70, 255))                          # winter stripe
    px(d, [(5, 2), (6, 1), (9, 2), (10, 1)], (255, 255, 255, 180))               # steam
    return img

def chili_bowl():
    img, d = new()
    d.pieslice([2, 4, 14, 16], 0, 180, fill=(150, 150, 160, 255), outline=OL)    # bowl
    d.ellipse([2, 4, 14, 8], fill=(150, 36, 28, 255), outline=OL)                 # chili surface
    px(d, [(5, 6), (8, 5), (10, 6), (7, 7)], (200, 70, 40, 255))
    px(d, [(6, 6), (9, 6)], (110, 20, 18, 255))
    px(d, [(5, 1), (6, 0), (9, 1), (10, 0), (7, 2)], (255, 255, 255, 170))        # steam
    d.line([(5, 13), (11, 13)], fill=OL)
    return img

ICONS = {
    "berrylemonade": lemonade, "gardenwrap": garden_wrap,
    "slushie": slushie, "corncup": corn_cup,
    "pumpkinshake": pumpkin_shake, "applepie": apple_pie,
    "hotcocoa": hot_cocoa, "chilibowl": chili_bowl,
}

if __name__ == "__main__":
    sheet = Image.new("RGBA", (16 * 8 * 8, 16 * 8), (120, 160, 120, 255))
    for i, (name, fn) in enumerate(ICONS.items()):
        img = fn()
        img.save(os.path.join(OUT, name + ".png"))
        sheet.alpha_composite(img.resize((128, 128), Image.NEAREST), (i * 128, 0))
    sheet.save(os.path.join(os.path.dirname(__file__), "_seasonal_preview.png"))
    print("wrote", len(ICONS), "icons")
