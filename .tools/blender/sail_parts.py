# The sail parts of the Clipper (ShipFrameDesigns.ClipperDesign), four node parts built from nothing:
#   hull.sail_mast    SailMast/sail_mast.fbx       a timber mast with a top, two yards and two teal sails in canvas trim
#   hull.rigging_net  RiggingNet/rigging_net.fbx   shrouds and ratlines from a rail node up to the mast's top
#   hull.wing_sail    WingSail/wing_sail.fbx       a fin of three spars and a teal sail hanging from the hull's chine
#   hull.figurehead   Figurehead/figurehead.fbx    a carved prow horn sweeping up from the bow
# Each goes to <models root>/<Folder>/ with its manifest.json. The manifest's "footprint" lists the part's 1 m blocks,
# worked out here from the geometry, and ShipDesignsAssetBuilder reads it from there. With a second path (a folder),
# it also renders a preview PNG of each part.
# Contract: .docs/plans/airship-parts-pack.md ("The mesh contract for Blender parts").
#
#   blender --background --factory-startup --python .tools/blender/sail_parts.py -- <models root> [<preview folder>]
#
# Everything is written in Unity's frame (Y up, +Z the bow, +X starboard) through U(), as in helm.py; one unit is one
# metre. Each pivot is the frame node the part hangs on. The sizes fit the Clipper: its rails are 5 m off the centreline
# where the masts stand, so a net reaches 4.4 m inboard to the mast's top at 8.45 m. Blocks keep the parts apart: a
# mast's pole is its column x = 0, z = 0, its sails and yards stand forward of it (z = 1); a net keeps to z = 0, so it
# meets the mast without overlapping it, and turns half round for the port side unchanged.
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else "."
PREVIEW = args[1] if len(args) > 1 else None

SOURCE = ".tools/blender/sail_parts.py (Blender 5.2, builds the models from nothing)"

bpy.ops.wm.read_factory_settings(use_empty=True)


def U(x, y, z):
    """A point in Unity's frame as Blender's (Z up, -Y forward); X flips too, so starboard lands on Unity's +X."""
    return Vector((-x, -z, y))


def material(name, rgb, metallic=0.0, roughness=0.6):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (*rgb, 1.0)
    return mat


# The names are the contract with ShipDesignsAssetBuilder, which swaps each for a shared ship material:
# Wood M_ShipHull, Brass M_ShipEngine, Iron M_ShipIron, Sail M_ShipSail, Trim M_ShipCanvas, Rope M_ShipDeck.
WOOD = material("Sail_Wood", (0.54, 0.35, 0.21))
BRASS = material("Sail_Brass", (0.72, 0.54, 0.24), metallic=0.8, roughness=0.35)
IRON = material("Sail_Iron", (0.24, 0.37, 0.37), metallic=0.6, roughness=0.45)
SAIL = material("Sail_Sail", (0.28, 0.57, 0.56), roughness=0.8)
TRIM = material("Sail_Trim", (0.90, 0.86, 0.75), roughness=0.8)
ROPE = material("Sail_Rope", (0.79, 0.64, 0.45), roughness=0.9)
ALL = [WOOD, BRASS, IRON, SAIL, TRIM, ROPE]


class Builder:
    """One object's geometry in Unity-frame points; each face tagged with its material. Blocks are sampled from it."""

    def __init__(self):
        self.bm = bmesh.new()
        self.samples = []  # Unity-frame points the blocks are taken from: every vertex and points along every rod

    def _tag(self, before, mat, smooth=True):
        index = ALL.index(mat)
        for face in self.bm.faces:
            if face not in before:
                face.material_index = index
                face.smooth = smooth

    def rod(self, a, b, ra, rb, mat, segments=10):
        """A tapering round rod from a to b (Unity frame)."""
        a, b = Vector(a), Vector(b)
        before = set(self.bm.faces)
        axis = b - a
        length = axis.length
        turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
        bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=ra, radius2=rb, depth=length,
                              matrix=Matrix.Translation((a + b) * 0.5) @ turn)
        self._tag(before, mat)
        steps = max(1, int(length / 0.2))
        self.samples += [a.lerp(b, i / steps) for i in range(steps + 1)]

    def sweep(self, points, radii, mat, segments=12, flat=1.0, steps=6):
        """A smooth tube through points (a Catmull-Rom curve), its radius eased between theirs, capped at both ends.
        Flat below 1 squeezes it across world X into a blade."""
        def at(seq, i):
            return Vector(seq[max(0, min(len(seq) - 1, i))]) if isinstance(seq[0], tuple) else seq[max(0, min(len(seq) - 1, i))]

        centres, rs = [], []
        for i in range(len(points) - 1):
            p0, p1, p2, p3 = at(points, i - 1), at(points, i), at(points, i + 1), at(points, i + 2)
            for k in range(steps + (1 if i == len(points) - 2 else 0)):
                t = k / steps
                centres.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3))
                rs.append(radii[i] + (radii[i + 1] - radii[i]) * (3 * t * t - 2 * t ** 3))
        before = set(self.bm.faces)
        rings = []
        for i, (c, r) in enumerate(zip(centres, rs)):
            tangent = (centres[min(i + 1, len(centres) - 1)] - centres[max(i - 1, 0)]).normalized()
            across = Vector((1.0, 0.0, 0.0))
            across = (across - tangent * across.dot(tangent)).normalized()
            up = tangent.cross(across)
            rings.append([self.bm.verts.new(c + across * math.cos(a) * r * flat + up * math.sin(a) * r)
                          for a in (2 * math.pi * j / segments for j in range(segments))])
            self.samples.append(c.copy())
        for i in range(len(rings) - 1):
            for j in range(segments):
                k = (j + 1) % segments
                self.bm.faces.new((rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]))
        for ring, c in ((rings[0], centres[0]), (rings[-1], centres[-1])):
            hub = self.bm.verts.new(c)
            for j in range(segments):
                self.bm.faces.new((hub, ring[(j + 1) % segments], ring[j]))
        self._tag(before, mat)
        return centres, rs

    def sphere(self, centre, radius, mat, segments=12, squash=1.0):
        before = set(self.bm.faces)
        bmesh.ops.create_uvsphere(self.bm, u_segments=segments, v_segments=max(4, segments // 2), radius=radius,
                                  matrix=Matrix.Translation(Vector(centre)) @ Matrix.Diagonal((1.0, squash, 1.0, 1.0)))
        self._tag(before, mat)

    def cloth(self, rows, mat_of, thickness=0.03):
        """A sheet through a grid of points (rows of columns), each quad's material from mat_of(row, col), made solid."""
        before = set(self.bm.faces)
        verts = [[self.bm.verts.new(Vector(p)) for p in row] for row in rows]
        sheet = []
        for i in range(len(rows) - 1):
            for j in range(len(rows[0]) - 1):
                face = self.bm.faces.new((verts[i][j], verts[i][j + 1], verts[i + 1][j + 1], verts[i + 1][j]))
                face.material_index = ALL.index(mat_of(i, j))
                face.smooth = True
                sheet.append(face)
        # A little thickness, so the cloth shows from both sides under back-face culling.
        bmesh.ops.solidify(self.bm, geom=sheet, thickness=thickness)
        for face in self.bm.faces:
            if face not in before and face not in sheet:
                face.smooth = True
        self.samples += [Vector(p) for row in rows for p in row]

    def ribbon(self, points, normals, width, mat, lift):
        """A flat strip along points, lying on a surface with these normals, lifted off it by `lift`."""
        before = set(self.bm.faces)
        left, right = [], []
        for i, (p, n) in enumerate(zip(points, normals)):
            p, n = Vector(p), Vector(n).normalized()
            along = (Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(i - 1, 0)])).normalized()
            side = n.cross(along).normalized() * width * 0.5
            base = p + n * lift
            left.append(self.bm.verts.new(base + side))
            right.append(self.bm.verts.new(base - side))
        strip = [self.bm.faces.new((left[i], left[i + 1], right[i + 1], right[i])) for i in range(len(points) - 1)]
        bmesh.ops.solidify(self.bm, geom=strip, thickness=0.01)
        self._tag(before, mat, smooth=False)
        self.samples += [Vector(p) for p in points]

    def finish(self, name, parent):
        """Turns the geometry into Blender's frame and makes the object; returns it and its blocks."""
        for v in self.bm.verts:
            self.samples.append(v.co.copy())
            v.co = U(*v.co)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in ALL:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = parent
        # A hair of slack, so a face lying on a block's boundary (the plinth on the deck) stays in the block above it.
        blocks = sorted({(math.floor(p.x + 0.5), math.floor(p.y + 1e-4), math.floor(p.z + 0.5)) for p in self.samples})
        return obj, blocks


# --- The sail mast -------------------------------------------------------------------------------------------------

SAIL_Z = 0.68  # the yards and the sails' heads hang this far forward of the pole's axis (in the z = 1 blocks)


def sail(b, top, bottom, half_top, half_bottom, belly, emblem):
    """A square sail from its yard (y top) down to its foot (y bottom), bellied forward, with canvas trim at its edges and,
    when asked, a canvas swirl on both faces."""
    us = [0.0, 0.035] + [i / 14 for i in range(1, 14)] + [0.965, 1.0]
    vs = [0.0, 0.05] + [i / 9 for i in range(1, 9)] + [0.93, 1.0]

    def point(u, v):
        half = half_top + (half_bottom - half_top) * v
        x = (u * 2.0 - 1.0) * half
        foot_rise = 0.3 * math.sin(math.pi * u) * v * v  # the foot sags up in the middle
        y = top - (top - bottom) * v + foot_rise
        z = SAIL_Z + belly * math.sin(math.pi * u) * math.sin(math.pi * min(1.0, v * 0.9 + 0.05))
        return Vector((x, y, z))

    rows = [[point(u, v) for u in us] for v in vs]
    last_u, last_v = len(us) - 2, len(vs) - 2
    b.cloth(rows, lambda i, j: TRIM if i in (0, last_v) or j in (0, last_u) else SAIL)

    if emblem:
        # A swirl, 1.75 turns out from the sail's middle, on both faces of the cloth.
        centre_u, centre_v = 0.5, 0.48
        width, height = half_top + half_bottom, top - bottom
        points, normals = [], []
        for i in range(56):
            t = i / 55
            angle = t * math.pi * 3.5
            r = 0.15 + 1.05 * t
            u = centre_u + r * math.cos(angle) / width
            v = centre_v - r * math.sin(angle) / height
            p = point(u, v)
            du = (point(u + 0.01, v) - point(u - 0.01, v))
            dv = (point(u, v + 0.01) - point(u, v - 0.01))
            points.append(p)
            normals.append(dv.cross(du).normalized())
        for side in (1.0, -1.0):
            b.ribbon(points, [n * side for n in normals], 0.16, TRIM, 0.03)


def sail_mast():
    b = Builder()
    # The step on the deck: an octagonal plinth and a brass collar.
    b.rod((0, 0, 0), (0, 0.14, 0), 0.44, 0.42, WOOD, segments=8)
    b.rod((0, 0.14, 0), (0, 0.48, 0), 0.30, 0.25, BRASS, segments=12)
    # The pole, tapering, banded in brass where the rigging and yards bear on it.
    b.rod((0, 0.1, 0), (0, 13.6, 0), 0.21, 0.11, WOOD, segments=12)
    for y in (1.6, 3.0, 6.2, 7.7, 10.6, 11.9):
        r = 0.215 - 0.1 * y / 13.5
        b.rod((0, y, 0), (0, y + 0.1, 0), r, r, BRASS, segments=12)
    # The top: a wooden cup with a brass rim, which the nets reach; a smaller cup over the upper yard; the finial.
    b.rod((0, 8.45, 0), (0, 8.85, 0), 0.26, 0.46, WOOD, segments=12)
    b.rod((0, 8.85, 0), (0, 8.93, 0), 0.47, 0.47, BRASS, segments=12)
    b.rod((0, 12.55, 0), (0, 12.78, 0), 0.2, 0.3, BRASS, segments=12)
    b.sphere((0, 13.72, 0), 0.14, BRASS)
    b.rod((0, 13.8, 0), (0, 14.6, 0), 0.06, 0.0, BRASS, segments=8)

    # The yards, each on an iron crane off the pole, tapering out to brass points.
    for y, half in ((8.05, 4.08), (12.25, 3.1)):
        b.rod((0, y, 0.1), (0, y, SAIL_Z), 0.07, 0.07, IRON, segments=8)
        for s in (1, -1):
            b.rod((0, y, SAIL_Z), (s * half, y, SAIL_Z), 0.12, 0.07, WOOD)
            b.rod((s * half, y, SAIL_Z), (s * (half + 0.36), y, SAIL_Z), 0.07, 0.0, BRASS, segments=8)
            b.rod((s * (half - 0.25), y, SAIL_Z), (s * (half - 0.15), y, SAIL_Z), 0.09, 0.09, BRASS)
    # Lifts from the lower yard's arms up to the upper yard, kept in the yards' plane.
    for s in (1, -1):
        b.rod((s * 3.7, 8.1, SAIL_Z), (s * 0.5, 12.2, SAIL_Z), 0.025, 0.025, ROPE, segments=6)

    sail(b, top=7.95, bottom=3.35, half_top=3.95, half_bottom=4.2, belly=0.7, emblem=True)
    sail(b, top=12.15, bottom=9.05, half_top=2.95, half_bottom=3.6, belly=0.55, emblem=False)
    return b


# --- The rigging net -----------------------------------------------------------------------------------------------

NET_BASE = (0.38, 1.12)   # (x, y): the deadeyes, just outboard of the rail
NET_APEX = (-4.4, 8.45)  # (x, y): under the mast's top, 4.4 m inboard


def rigging_net():
    b = Builder()
    bx, by = NET_BASE
    ax, ay = NET_APEX
    spread = 0.38

    def on_shroud(z0, t):
        return Vector((bx + (ax - bx) * t, by + (ay - by) * t, z0 * (1.0 - 0.8 * t)))

    # The chain plates down the side and the deadeyes on them; a sheer pole across the shrouds over them.
    for z in (-spread, 0.0, spread):
        b.rod((bx, 0.05, z), (bx, by - 0.08, z), 0.03, 0.03, IRON, segments=6)
        b.sphere((bx, by, z), 0.1, BRASS, segments=10, squash=0.7)
    b.rod((bx - 0.05, by + 0.25, -spread - 0.1), (bx - 0.05, by + 0.25, spread + 0.1), 0.045, 0.045, WOOD, segments=8)
    # Three shrouds to the top, and ratlines across them every 0.42 m of height.
    for z in (-spread, 0.0, spread):
        b.rod(on_shroud(z, 0.0) + Vector((0, 0.08, 0)), on_shroud(z, 1.0), 0.035, 0.03, ROPE, segments=6)
    steps = int((ay - by - 0.5) / 0.42)
    for i in range(1, steps + 1):
        t = i * 0.42 / (ay - by)
        b.rod(on_shroud(-spread, t), on_shroud(spread, t), 0.022, 0.022, ROPE, segments=5)
    b.sphere(on_shroud(0.0, 1.0), 0.06, BRASS, segments=10)
    return b


# --- The wing sail -------------------------------------------------------------------------------------------------

def wing_sail():
    b = Builder()
    root_fore, root_aft, root_mid = Vector((0.22, -0.15, 0.85)), Vector((0.22, -0.15, -0.85)), Vector((0.22, -0.15, 0.0))
    tip_fore, tip_aft, tip_mid = Vector((2.95, -2.95, 0.55)), Vector((2.95, -2.95, -0.55)), Vector((3.55, -3.55, 0.0))
    # The cleat on the hull: a wooden roll with brass caps and a brass band.
    b.rod((0.18, -0.12, -1.02), (0.18, -0.12, 1.02), 0.2, 0.2, WOOD)
    for z in (-1.02, 1.02):
        b.sphere((0.18, -0.12, z), 0.21, BRASS, squash=1.0)
    b.rod((0.18, -0.12, -0.08), (0.18, -0.12, 0.08), 0.23, 0.23, BRASS)
    # Three spars, each ending in a brass point; the middle one, the boom, longest.
    for root, tip, r in ((root_fore, tip_fore, 0.08), (root_aft, tip_aft, 0.08), (root_mid, tip_mid, 0.1)):
        b.rod(root, tip, r, r * 0.6, WOOD)
        along = (tip - root).normalized()
        b.rod(tip, tip + along * 0.32, r * 0.6, 0.0, BRASS, segments=8)
        b.rod(root.lerp(tip, 0.5), root.lerp(tip, 0.5) + along * 0.08, r * 1.15, r * 1.15, BRASS)

    # The cloth between the outer spars, over the boom, sagging a little below the spars' plane.
    sag_dir = Vector((-1.0, -1.0, 0.0)).normalized()  # the spars' plane normal: down and inboard, under the spars
    us = [0.0, 0.05] + [i / 8 for i in range(1, 8)] + [0.95, 1.0]
    vs = [0.04] + [i / 7 for i in range(1, 7)] + [0.92, 0.97]
    rows = []
    for v in vs:
        fore, aft = root_fore.lerp(tip_fore, v), root_aft.lerp(tip_aft, v)
        mid = root_mid.lerp(tip_mid, v)
        row = []
        for u in us:
            p = fore.lerp(aft, u)
            # Pass over the boom in the middle, sag between it and the outer spars.
            hump = 1.0 - abs(u * 2.0 - 1.0)
            p = p.lerp(Vector((p.x, p.y, p.z)) + (mid - fore.lerp(aft, 0.5)), hump)
            p += sag_dir * 0.12 * math.sin(math.pi * u * 2.0) ** 2
            row.append(p)
        rows.append(row)
    last_u, last_v = len(us) - 2, len(vs) - 2
    b.cloth(rows, lambda i, j: TRIM if i == last_v or j in (0, last_u) else SAIL)
    return b


# --- The figurehead ------------------------------------------------------------------------------------------------

def figurehead():
    b = Builder()
    # The horn: up the stem, out over the bow and curling back at its tip.
    spine = [(0, -1.4, -0.75), (0, -0.55, -0.2), (0, 0.35, 0.45), (0, 1.25, 1.2), (0, 2.1, 1.95), (0, 2.85, 2.45), (0, 3.4, 2.55),
             (0, 3.7, 2.25), (0, 3.72, 1.95)]
    radii = [0.3, 0.36, 0.34, 0.29, 0.22, 0.15, 0.1, 0.06, 0.02]
    centres, rs = b.sweep(spine, radii, TRIM, segments=14)
    # Teal bands along it, each edged in brass on its upper side.
    for i in (8, 16, 24, 31):
        c, d, r = centres[i], (centres[i + 1] - centres[i - 1]).normalized(), rs[i]
        b.rod(c - d * 0.1, c + d * 0.1, r + 0.035, r + 0.03, SAIL, segments=14)
        b.rod(c + d * 0.1, c + d * 0.14, r + 0.045, r + 0.04, BRASS, segments=14)
    b.sphere(centres[-1], 0.05, BRASS, segments=8)
    # Two swept fins either side, blades like a bird's wing folded back along the bow, each with a teal vane under it.
    for s in (1, -1):
        b.sweep([(s * 0.2, 0.3, 0.6), (s * 0.45, 0.75, 0.15), (s * 0.62, 1.25, -0.45), (s * 0.6, 1.7, -0.95)],
                [0.2, 0.17, 0.11, 0.02], TRIM, segments=10, flat=0.3)
        b.sweep([(s * 0.25, 0.15, 0.35), (s * 0.5, 0.45, -0.1), (s * 0.58, 0.85, -0.55)], [0.13, 0.1, 0.02], SAIL, segments=10, flat=0.3)
    return b


# --- Export --------------------------------------------------------------------------------------------------------

PARTS = [
    ("SailMast", "sail_mast", "hull.sail_mast", "Sail mast", sail_mast,
     "A 14 m timber mast banded in brass with a cup top, two yards on iron cranes and two bellied teal sails trimmed in "
     "canvas, a canvas swirl on the lower one. Pivot on the frame node it stands on.",
     "The pole and finial (x 0, z 0, y 0..14); the yards and sails forward of it (z 1). One box round the pole on the prefab; "
     "the sails and yards are not solid."),
    ("RiggingNet", "rigging_net", "hull.rigging_net", "Rigging net", rigging_net,
     "Three shrouds and their ratlines from deadeyes just outboard of a rail node up 4.4 m inboard to under a mast's top "
     "(8.45 m). Inboard is -X: starboard nets stand unturned, port nets turned half round. Pivot on the rail node.",
     "One row of blocks at z 0, from the rail (x 0) in to x -4; not solid."),
    ("WingSail", "wing_sail", "hull.wing_sail", "Wing sail", wing_sail,
     "A cleat on the hull and three spars (the middle one the boom) splaying down and out 3.9 m, teal cloth between them "
     "trimmed in canvas, brass points. Outboard is +X: starboard wings unturned, port wings turned half round. Pivot on "
     "the chine node it hangs from.",
     "Blocks out to x 4 and down to y -4, z -1..1; not solid."),
    ("Figurehead", "figurehead", "hull.figurehead", "Figurehead", figurehead,
     "A pale carved horn up the stem and out over the bow (+Z), curling back at a brass point, banded in teal, with two "
     "swept fins either side. Pivot on the bow's tip node.",
     "Blocks z -1..3, y -2..3 on the centreline; not solid."),
]


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs:
        obj.select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"EMPTY", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)


def render(objs, path, eye, target, size):
    scene = bpy.context.scene
    for obj in scene.objects:
        if obj.type != "CAMERA":
            obj.hide_render = obj not in objs
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = size
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Sky")
    scene.world.color = (0.62, 0.72, 0.78)
    cam = scene.camera
    cam.location = U(*eye)
    cam.rotation_euler = (U(*target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


scene = bpy.context.scene
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
scene.collection.objects.link(cam)
scene.camera = cam

built = {}
for folder, stem, part_id, title, make, summary, blocks_note in PARTS:
    root = bpy.data.objects.new(folder, None)
    root.empty_display_type = "PLAIN_AXES"
    scene.collection.objects.link(root)
    mesh, blocks = make().finish(f"{folder}_Mesh", root)
    built[folder] = (root, mesh)
    fbx = os.path.join(ROOT, folder, f"{stem}.fbx")
    export([root, mesh], fbx)
    manifest = {
        "scene": stem,
        "formats": ["fbx"],
        "source": SOURCE,
        "coordinate_system": "Y-up, -Z forward; one exported unit is one meter",
        "asset_contract": {
            "title": title,
            "summary": f"{summary} Ship part {part_id} (node part).",
            "asset_type": "ship_part",
            "placement": "Pivot at the frame node the part hangs on (part pivot offset zero).",
            "nodes": [{"name": f"{folder}_Mesh", "role": "Static: the whole part."}],
            "materials": "Sail_Wood, Sail_Brass, Sail_Iron, Sail_Sail, Sail_Trim, Sail_Rope; ShipDesignsAssetBuilder swaps each for the shared "
                         "ship material (M_ShipHull, M_ShipEngine, M_ShipIron, M_ShipSail, M_ShipCanvas, M_ShipDeck).",
            "footprint_blocks": blocks_note,
            "movable_parts": "None yet: the cloth is to sway in its material (ART_DIRECTION.md, Motion and life).",
        },
        "footprint": [{"x": x, "y": y, "z": z} for x, y, z in blocks],
    }
    with open(os.path.join(ROOT, folder, "manifest.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"[sail_parts] {part_id}: {fbx}, {len(blocks)} blocks, y {min(b[1] for b in blocks)}..{max(b[1] for b in blocks)}, "
          f"x {min(b[0] for b in blocks)}..{max(b[0] for b in blocks)}, z {min(b[2] for b in blocks)}..{max(b[2] for b in blocks)}")

if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
    views = {
        "SailMast": ((9.0, 9.0, -11.0), (0.0, 7.0, 0.5), (900, 1100)),
        "RiggingNet": ((6.0, 5.0, -6.0), (-2.0, 4.5, 0.0), (900, 900)),
        "WingSail": ((5.5, 1.0, 5.0), (1.8, -1.8, 0.0), (900, 900)),
        "Figurehead": ((6.5, 2.5, 4.0), (0.0, 1.2, 0.8), (900, 900)),
    }
    for folder, (eye, target, size) in views.items():
        render(list(built[folder]), os.path.join(PREVIEW, f"{folder}.png"), eye, target, size)
    # The mast with a net either side, as on the deck: the port net turned half round.
    mast_root, mast_mesh = built["SailMast"]
    net_root, net_mesh = built["RiggingNet"]
    net_root.location = U(5.0, 0.0, 0.0)
    port = net_mesh.copy()
    scene.collection.objects.link(port)
    port.parent = None
    port.location = U(-5.0, 0.0, 0.0)
    port.rotation_euler = (0.0, 0.0, math.pi)
    render([mast_root, mast_mesh, net_root, net_mesh, port], os.path.join(PREVIEW, "MastRigged.png"), (12.0, 7.0, -13.0), (0.0, 6.5, 0.0),
           (1100, 1100))
    print(f"[sail_parts] previews in {PREVIEW}")
