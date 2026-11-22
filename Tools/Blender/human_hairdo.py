"""A quick look at a hairstyle (or a beard) of hair cards on a person's head, in a minute instead of a whole figure: the
head, the sculpted mass under the cards and the cards with the strand texture cut out by alpha, lit (EEVEE) — from the
front, three quarters, the side, the back, above, and two close-ups.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/human_hairdo.py -- <out.png> <who> <style|beard> [fine]
"""
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT, WHO, STYLE = os.path.abspath(argv[0]), argv[1], argv[2]
FINE = "fine" in argv
sys.argv = sys.argv[:1]
import human  # noqa: E402
from human import H, HEAD_S  # noqa: E402
from humanlib import build, face, sdf, view  # noqa: E402

bpy.ops.wm.read_factory_settings(use_empty=True)
human.K = 1.0 if FINE else 1.4
c = human.Who(WHO)
skin = np.array(human.SKIN[WHO])
hd = human.sculpt("head", lambda p: sdf.smax(c.head_f(p), 1.48 - p[..., 2], 0.004), (-0.113, -0.15, 1.47), (0.113, 0.14, 1.82), 0.0016)
rgb, gloss = human.paint_head(H(sdf.points_of(hd)), c.person)
build.paint(hd, np.clip(rgb * skin[None, :], 0, 1), gloss)
pieces = human.item_beard(c, []) if STYLE == "beard" else human.item_hair(STYLE)(c, [STYLE])
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
strands = bpy.data.images.load(os.path.join(root, "Assets", "_Game", "Art", "Textures", "Humans", "hair_strands.png"))
colour = (*c.person.hair_colour, 1)


def vertex_material(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "Col"
    nt.links.new(attr.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.6
    return m


def card_material():
    m = bpy.data.materials.new("cards")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = strands
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    nt.links.new(tex.outputs["Color"], mix.inputs[6])
    mix.inputs[7].default_value = colour
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    cut = nt.nodes.new("ShaderNodeMath")
    cut.operation = "GREATER_THAN"
    cut.inputs[1].default_value = 0.45
    nt.links.new(tex.outputs["Alpha"], cut.inputs[0])
    nt.links.new(cut.outputs[0], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.55
    m.surface_render_method = "DITHERED"
    m.use_backface_culling = False
    return m


hd.data.materials.append(vertex_material("skin"))
objs = [hd]
for p in pieces:
    o = p.lo if p.own_uv else p.hi
    o.data.materials.clear()
    o.data.materials.append(card_material() if p.own_uv else vertex_material(p.name))
    objs.append(o)
for name, loc, energy in (("key", (1.2, -2.0, 2.6), 160), ("fill", (-2.0, -1.0, 1.9), 50), ("rim", (0.3, 2.2, 2.4), 110)):
    light = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
    light.data.energy = energy
    light.data.size = 1.5
    light.location = loc
    from mathutils import Vector
    light.rotation_euler = (Vector((0, 0, 1.66)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(light)
cen = (0, 0.0, 1.69) if STYLE != "beard" else (0, -0.05, 1.60)
span = 0.30 if STYLE != "beard" else 0.18
shots = [("front", (0, -1, 0.05), cen, span), ("3/4", (0.7, -0.7, 0.1), cen, span), ("side", (1, 0, 0.05), cen, span),
         ("back", (0, 1, 0.1), cen, span), ("top", (0, -0.3, 1), cen, span),
         ("close", (0.5, -0.8, 0.3), (0.0, -0.04, 1.76) if STYLE != "beard" else (0.02, -0.08, 1.58), 0.09),
         ("close2", (0.9, 0.3, 0.1), (0.07, 0.02, 1.70) if STYLE != "beard" else (0.05, -0.05, 1.56), 0.09)]
view.sheet(OUT, objs, shots=shots, width=420, height=420, engine="BLENDER_EEVEE")
print("DONE")
