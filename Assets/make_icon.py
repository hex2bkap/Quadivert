"""
PixShift アイコン生成スクリプト
デザイン: A-2（水平矢印＋左塗り正方形・右枠正方形）× A-3カラー（グリーン→ブルー）
出力: PixShift/Assets/PixShift.ico (16/24/32/48/64/128/256px 全サイズ埋め込み)
"""

from PIL import Image, ImageDraw
import os

# A-3 カラー: グリーン→ブルー
BG_COLOR1 = (5, 150, 105)    # #059669 エメラルド
BG_COLOR2 = (37, 99, 235)    # #2563EB ブルー

VB = 96.0


def lerp(a, b, t):
    return a + (b - a) * t


def make_bg(size):
    c1, c2 = BG_COLOR1, BG_COLOR2
    n = size - 1
    data = []
    for y in range(size):
        for x in range(size):
            t = (x + y) / (2.0 * n) if n > 0 else 0
            r = int(lerp(c1[0], c2[0], t))
            g = int(lerp(c1[1], c2[1], t))
            b = int(lerp(c1[2], c2[2], t))
            data.append((r, g, b, 255))

    grad = Image.new("RGBA", (size, size))
    grad.putdata(data)

    radius = max(2, int(size * 22 / 96))
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, size - 1, size - 1], radius=radius, fill=255
    )

    result = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    result.paste(grad, mask=mask)
    return result


def draw_icon(img):
    """A-2 デザイン: 左塗り正方形 → 右矢印 → 右枠正方形"""
    size = img.width
    draw = ImageDraw.Draw(img)
    s = size / 96.0

    # --- 座標 (viewBox 96x96 ベース) ---
    # 左: 塗り正方形  x=8, y=30, w=30, h=30
    lx, ly, lw, lh = 8*s, 30*s, 30*s, 30*s
    # 右: 枠正方形    x=58, y=30, w=30, h=30
    rx, ry, rw, rh = 58*s, 30*s, 30*s, 30*s

    # 左 塗り正方形
    draw.rectangle(
        [lx, ly, lx + lw, ly + lh],
        fill=(255, 255, 255, 235)
    )

    # 右 枠正方形
    sw = max(1, int(5 * s))  # stroke-width
    draw.rectangle(
        [rx, ry, rx + rw, ry + rh],
        outline=(255, 255, 255, 210),
        width=sw
    )

    # 矢印シャフト: rect x=40, y=42, w=16, h=7
    ax, ay, aw, ah = 40*s, 42*s, 16*s, 7*s
    # 先端 polygon: (56,35) (56,56) (72,45) → right-pointing triangle
    tx1, tx2 = 56*s, 72*s
    ty_top, ty_bot, ty_mid = 35*s, 56*s, 45*s

    draw.rectangle(
        [ax, ay, ax + aw, ay + ah],
        fill=(255, 255, 255, 235)
    )
    draw.polygon(
        [(tx1, ty_top), (tx1, ty_bot), (tx2, ty_mid)],
        fill=(255, 255, 255, 235)
    )

    return img


def create_icon(size):
    img = make_bg(size)
    img = draw_icon(img)
    return img


OUT_DIR = os.path.dirname(os.path.abspath(__file__))
os.makedirs(OUT_DIR, exist_ok=True)

SIZES = [256, 128, 64, 48, 32, 24, 16]
print("アイコン生成中...")
icons = {}
for s in SIZES:
    icons[s] = create_icon(s)
    print(f"  {s:3}px  完了")

ico_path = os.path.join(OUT_DIR, "PixShift.ico")
icons[256].save(
    ico_path,
    format="ICO",
    sizes=[(s, s) for s in SIZES],
)
print(f"ICO 保存: {ico_path}")
print("完了！")
