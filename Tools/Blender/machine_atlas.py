"""Where the pictures of machines lie on their textures (pure data: Tools/Art/generate_machines.py draws them, the model
scripts map faces onto them). Cells are (x0, y0, x1, y1) in pixels, y down, on a SIZE x SIZE texture.
"""
SIZE = 1024
EDGE = 2                                                    # faces keep this many pixels from a cell's border (mip bleeding)

# machines.png: what is printed on machines (a lit surface)
PRINT = {
    "arcade_side": (0, 0, 256, 512), "arcade_panel": (256, 0, 512, 128), "arcade_front": (256, 128, 512, 320), "coin": (256, 320, 384, 448),
    "bezel": (384, 320, 512, 448), "keypad": (512, 384, 640, 544), "slots": (640, 384, 768, 544), "cards": (768, 384, 1024, 544),
    "record": (0, 512, 128, 640), "danger": (128, 512, 256, 640), "circuits": (256, 512, 384, 640), "boxlabel": (384, 512, 512, 576), "boxlabel2": (384, 576, 512, 640),
}
for _k in range(8):
    PRINT[f"bottle{_k}"] = (512 + 64 * _k, 0, 576 + 64 * _k, 128)
    PRINT[f"can{_k}"] = (512 + 64 * _k, 128, 576 + 64 * _k, 224)
    PRINT[f"bag{_k}"] = (512 + 64 * _k, 224, 576 + 64 * _k, 320)
    PRINT[f"bar{_k}"] = (512 + 64 * _k, 320, 576 + 64 * _k, 352)
    PRINT[f"tag{_k}"] = (512 + 64 * _k, 352, 576 + 64 * _k, 384)
# plain colours, for the rest of a printed thing
SWATCHES = {"white": (226, 226, 222), "black": (24, 24, 26), "grey": (120, 122, 124), "chrome": (176, 178, 180), "red": (150, 24, 22), "cap": (206, 40, 30),
            "blue": (34, 66, 140), "yellow": (214, 168, 30), "brown": (74, 40, 22), "clear": (196, 210, 212), "foil": (190, 192, 196), "cola": (46, 20, 12),
            "lime": (120, 170, 60), "orange": (214, 110, 30), "violet": (80, 40, 120), "cream": (222, 208, 178)}
SWATCHES.update({"kraft": (176, 140, 96), "canvas": (196, 184, 156), "green": (52, 120, 66), "tin": (150, 152, 150), "paper": (236, 236, 230), "navy": (30, 44, 80),
                 "binred": (176, 36, 30), "binblue": (30, 70, 150), "binyellow": (220, 170, 20), "bingreen": (40, 130, 70), "bingrey": (110, 114, 118), "brass": (170, 130, 50),
                 "copper": (170, 96, 50), "white2": (240, 240, 236), "rust": (120, 70, 40), "tape": (150, 110, 60)})
for _k, _n in enumerate(SWATCHES):
    PRINT[_n] = (64 * (_k % 16), 656 + 64 * (_k // 16), 64 * (_k % 16) + 64, 720 + 64 * (_k // 16))
# what stands on shelves
PRINT.update({"carton": (0, 800, 128, 928), "carton2": (128, 800, 256, 928), "paperbox": (256, 800, 384, 928), "ream": (384, 800, 512, 864),
              "notes": (512, 944, 640, 1008), "noteedge": (640, 944, 704, 1008), "band": (704, 944, 768, 1008), "bagmark": (768, 944, 832, 1008)})
for _k in range(8):
    PRINT[f"paint{_k}"] = (64 * _k, 944, 64 * _k + 64, 1008)
    PRINT[f"binlabel{_k}"] = (512 + 64 * _k, 800, 576 + 64 * _k, 832)
for _k in range(4):
    PRINT[f"toner{_k}"] = (512 + 128 * _k, 832, 640 + 128 * _k, 928)

# machine_glow.png: what shines of itself (dimly)
GLOW = {
    "screen": (0, 0, 512, 512), "marquee": (512, 0, 1024, 170), "header": (512, 170, 1024, 300), "display": (512, 300, 768, 380), "lit": (768, 300, 832, 364),
    "button": (832, 300, 896, 364), "coinslot": (896, 300, 960, 364), "tube": (0, 512, 128, 1024), "cards": (128, 512, 640, 700),
}

# keyboard.png: two keyboards seen from above (KEY_PX pixels a key's width), plain colours under them
KEY_PX = 45
KEYBOARDS = {"black": (0, 0), "light": (0, 300)}            # the top left corner of each
KEY_SWATCH = {"black": (16, 620), "light": (80, 620), "wheel": (144, 620), "cable": (208, 620)}

# a full-size keyboard: rows of (legend, width in keys) or a gap (None, width); y of each row in keys
ROWS = [
    (0.0, [("Esc", 1), (None, 1), ("F1", 1), ("F2", 1), ("F3", 1), ("F4", 1), (None, .5), ("F5", 1), ("F6", 1), ("F7", 1), ("F8", 1), (None, .5),
           ("F9", 1), ("F10", 1), ("F11", 1), ("F12", 1), (None, .25), ("Prt", 1), ("Scr", 1), ("Brk", 1)]),
    (1.25, [("~", 1), *[(c, 1) for c in "1234567890-="], ("Back", 2), (None, .25), ("Ins", 1), ("Home", 1), ("PgUp", 1), (None, .25),
            ("Num", 1), ("/", 1), ("*", 1), ("-", 1)]),
    (2.25, [("Tab", 1.5), *[(c, 1) for c in "QWERTYUIOP[]"], ("\\", 1.5), (None, .25), ("Del", 1), ("End", 1), ("PgDn", 1), (None, .25),
            ("7", 1), ("8", 1), ("9", 1), ("+", 1, 2)]),
    (3.25, [("Caps", 1.75), *[(c, 1) for c in "ASDFGHJKL;'"], ("Enter", 2.25), (None, 3.5), ("4", 1), ("5", 1), ("6", 1)]),
    (4.25, [("Shift", 2.25), *[(c, 1) for c in "ZXCVBNM,./"], ("Shift", 2.75), (None, 1.25), ("^", 1), (None, 1.25), ("1", 1), ("2", 1), ("3", 1), ("Ent", 1, 2)]),
    (5.25, [("Ctrl", 1.25), ("Win", 1.25), ("Alt", 1.25), ("", 6.25), ("Alt", 1.25), ("Win", 1.25), ("Menu", 1.25), ("Ctrl", 1.25), (None, .25),
            ("<", 1), ("v", 1), (">", 1), (None, .25), ("0", 2), (".", 1)]),
]
KEYS_W, KEYS_H = 22.5, 6.25


def keys():
    """Every key as (legend, x, y, w, h) in keys, x right and y towards the sitter, from the top left corner."""
    for y, row in ROWS:
        x = 0.0
        for k in row:
            w, h = k[1], k[2] if len(k) > 2 else 1
            if k[0] is not None:
                yield k[0], x, y, w, h
            x += w


# displays.png: the small displays of machines, 4 x 6 cells
DISPLAYS = ["synth", "clock", "program", "printer", "treadmill", "score", "vitals", "wave", "server", "power", "temp", "plain", "scan", "turnstile", "welder",
            "balance", "kiosk", "game", "rower", "bars", "paint", "plot", "print3d", "ok"]
DISPLAY_COLS, DISPLAY_ROWS = 4, 6
