# -*- coding: utf-8 -*-
r"""
Генератор превью (thumbnail) для модов Quasimorph в Steam Workshop.

Стиль: тёмный точечный фон игры (backgroundDots), пиксельные иконки из ресурсов
игры (NEAREST, без сглаживания), крупный заголовок Impact зелёным/белым,
фиолетовый треугольник-подпись в левом нижнем углу.

Проверено на публикации: квадрат 640x640 (Steam показывает квадратный кроп),
PNG. Спрайты игры уже извлечены: D:\modding\sprites\ и sprites\class_icons\
(извлечение — UnityPy, см. QM-MODDING-GUIDE.md §3).

Примеры:
    python make_workshop_preview.py                       # дефолты (ChooseStartOperatives)
    python make_workshop_preview.py --t1 "MY MOD" --t2 "DOES THINGS" \
        --icons a.png b.png --sub "line one" "line two" --out preview.png
    python make_workshop_preview.py --frame               # с рамкой headerBackground по краям
    python make_workshop_preview.py --size 640x360        # широкая версия

Потом: скопировать в publish\thumbnail.png и
    mod_updateworkshopitem <id> <publishPath> TRUE
"""
import argparse
import os
from PIL import Image, ImageDraw, ImageFont

SPRITES = r"D:\modding\sprites"
ICONS_DIR = os.path.join(SPRITES, "class_icons")
FONT_IMPACT = r"C:\Windows\Fonts\impact.ttf"

BG = (15, 16, 16, 255)          # фон игры (#0F1010, сэмпл headerBackground)
GREEN = (129, 181, 122, 255)    # зелёный текст UI игры
LIGHT = (232, 232, 232, 255)
PURPLE = (150, 88, 236, 255)    # подпись-треугольник


def nine_slice(sprite, border, size):
    """Растянуть 9-slice спрайт до size=(w,h) без сглаживания."""
    L, T, R, B = border
    sw, sh = sprite.size
    out = Image.new("RGBA", size)
    cw, ch = size
    mw, mh = cw - L - R, ch - T - B
    out.paste(sprite.crop((0, 0, L, T)), (0, 0))
    out.paste(sprite.crop((sw - R, 0, sw, T)), (cw - R, 0))
    out.paste(sprite.crop((0, sh - B, L, sh)), (0, ch - B))
    out.paste(sprite.crop((sw - R, sh - B, sw, sh)), (cw - R, ch - B))
    out.paste(sprite.crop((0, T, L, sh - B)).resize((L, mh), Image.NEAREST), (0, T))
    out.paste(sprite.crop((sw - R, T, sw, sh - B)).resize((R, mh), Image.NEAREST), (cw - R, T))
    out.paste(sprite.crop((L, 0, sw - R, T)).resize((mw, T), Image.NEAREST), (L, 0))
    out.paste(sprite.crop((L, sh - B, sw - R, sh)).resize((mw, B), Image.NEAREST), (L, ch - B))
    out.paste(sprite.crop((L, T, sw - R, sh - B)).resize((mw, mh), Image.NEAREST), (L, T))
    return out


def load_icon(name, scale):
    """Пиксельная иконка из ресурсов игры, увеличенная кратно (NEAREST — чёткие пиксели)."""
    for d in (ICONS_DIR, SPRITES):
        p = os.path.join(d, name if name.endswith(".png") else name + ".png")
        if os.path.exists(p):
            im = Image.open(p).convert("RGBA")
            return im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    raise FileNotFoundError("иконка не найдена ни в %s, ни в %s: %s" % (ICONS_DIR, SPRITES, name))


def main():
    ap = argparse.ArgumentParser(description="Превью для Workshop в стиле Quasimorph")
    ap.add_argument("--t1", default="CHOOSE STARTING", help="первая строка заголовка (зелёная)")
    ap.add_argument("--t2", default="OPERATIVES & CLASSES", help="вторая строка заголовка (белая)")
    ap.add_argument("--sub", nargs=2, default=["Pick your starting squad", "and classes at new game start"],
                    help="две строки подписи (белая, зелёная)")
    ap.add_argument("--icons", nargs="+", default=["SoH_ClassIcon", "SBF_ClassIcon", "TE_ClassIcon"],
                    help="имена файлов иконок (sprites/ или sprites/class_icons/)")
    ap.add_argument("--icon-scale", type=int, default=7, help="кратность увеличения иконок")
    ap.add_argument("--size", default="640x640", help="размер, ШxВ (квадрат — Steam кропает квадратом)")
    ap.add_argument("--frame", action="store_true", help="рисовать рамку headerBackground по краям")
    ap.add_argument("--triangle", type=int, default=72, help="сторона фиолетового треугольника, px (0 — выключить)")
    ap.add_argument("--out", default=r"D:\modding\ChooseStartOperatives\publish\thumbnail.png")
    args = ap.parse_args()

    w, h = (int(x) for x in args.size.split("x"))

    # фон: цвет игры + точечная текстура меню
    canvas = Image.new("RGBA", (w, h), BG)
    dots = Image.open(os.path.join(SPRITES, "backgroundDots.png")).convert("RGBA")
    for y in range(0, h, dots.height):
        for x in range(0, w, dots.width):
            canvas.alpha_composite(dots, (x, y))

    if args.frame:
        hb = Image.open(os.path.join(SPRITES, "headerBackground.png")).convert("RGBA")
        canvas.alpha_composite(nine_slice(hb, (4, 2, 3, 3), (w, h)))

    draw = ImageDraw.Draw(canvas)
    title_font = ImageFont.truetype(FONT_IMPACT, max(36, w // 13))
    sub_font = ImageFont.truetype(FONT_IMPACT, max(18, w // 21))

    def text_h(font, text):
        b = draw.textbbox((0, 0), text, font=font)
        return b[3] - b[1]

    def centered(y, text, font, fill, gap):
        b = draw.textbbox((0, 0), text, font=font)
        draw.text(((w - (b[2] - b[0])) // 2, y), text, font=font, fill=fill)
        return y + text_h(font, text) + gap

    icons = [load_icon(n, args.icon_scale) for n in args.icons]
    gap_px = 52
    icons_h = max(i.height for i in icons)
    icons_w = sum(i.width for i in icons) + gap_px * (len(icons) - 1)

    # высота блока для вертикального центрирования
    block = (text_h(title_font, args.t1) + 12) * 2 + 18 + icons_h + 30 \
        + (text_h(sub_font, args.sub[0]) + 10) * 2
    y = (h - block) // 2

    y = centered(y, args.t1, title_font, GREEN, 12)
    y = centered(y, args.t2, title_font, LIGHT, 12)
    y += 18

    x = (w - icons_w) // 2
    for im in icons:
        canvas.alpha_composite(im, (x, y))
        x += im.width + gap_px
    y += icons_h + 30

    y = centered(y, args.sub[0], sub_font, LIGHT, 10)
    y = centered(y, args.sub[1], sub_font, GREEN, 10)

    if args.triangle > 0:
        t = args.triangle
        draw.polygon([(0, h), (t, h), (0, h - t)], fill=PURPLE)

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    canvas.save(args.out)
    print("saved %s %s, контент до y=%d" % (args.out, args.size, y))


if __name__ == "__main__":
    main()
