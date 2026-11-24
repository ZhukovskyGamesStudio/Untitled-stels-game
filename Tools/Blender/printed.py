"""UVs of printed things onto the pictures of Tools/Art/generate_covers.py (Art/Textures/Covers).

printed.png is 4 x 3 cells (cell 0 top left, MAGAZINE 0-5, BACK, BOOK, PAGES 8 and 9, CLOTH, PAPER); spines_<colour>.png is
eight spines over the top three quarters and plain cloth below. Call these on an object shapes.part() made (its box UVs
are replaced face by face; a rule returns the cell and where in it each corner is).
"""
from mathutils import Vector

MAGAZINES, BACK, BOOK, PAGE_L, PAGE_R, CLOTH, PAPER = 6, 6, 7, 8, 9, 10, 11
EDGE = 0.012                                     # cells keep this far from their borders (mip bleeding)


def cell_uv(cell, u, v):
    """Atlas UV of the point (u right, v up, 0..1) of a cell of printed.png."""
    u, v = EDGE + min(max(u, 0.0), 1.0) * (1 - 2 * EDGE), EDGE + min(max(v, 0.0), 1.0) * (1 - 2 * EDGE)
    return ((cell % 4 + u) / 4, 1 - (cell // 4 + 1 - v) / 3)


def spine_uv(k, u, v):
    """Atlas UV of spine k of spines_<colour>.png."""
    return ((k + 0.03 + 0.94 * min(max(u, 0.0), 1.0)) / 8, 0.253 + 0.744 * min(max(v, 0.0), 1.0))


def cloth_uv(co):
    """The plain cloth under the spines, for any face (a few centimetres a tile)."""
    u, v = co.x * 1.7 + co.y * 2.3, co.z * 1.1 + co.y * 0.7
    return (u, 0.02 + 0.2 * abs((v * 2) % 2 - 1))


def paint(obj, rule):
    """rule(polygon normal, polygon centre, corner) -> (u, v)."""
    me = obj.data
    uv = me.uv_layers.active.data
    for p in me.polygons:
        n, c = p.normal.copy(), p.center.copy()
        for li in p.loop_indices:
            uv[li].uv = rule(n, c, me.vertices[me.loops[li].vertex_index].co)


def spine_rule(k, h, t, back=None):
    """An upright book, spine to -Y, its bottom centre at the origin (back: undoes a transform it was given)."""
    def rule(n, c, co):
        if back is not None:
            n, co = (back.to_3x3() @ n).normalized(), back @ co
        if n.y < -0.25:
            return spine_uv(k, (co.x + t / 2) / t, co.z / h)
        return cloth_uv(co)
    return rule


def swap_uv(obj):
    """Turns a part's UVs a quarter (the edge lines of pages of a book lying down)."""
    for d in obj.data.uv_layers.active.data:
        d.uv = Vector((d.uv[1], d.uv[0]))
