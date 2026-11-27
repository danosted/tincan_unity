# The ray cannon (the CannonStation fixture): a spirit-beam gun built like a deck harpoon. A wooden plinth and tripod
# carry a slim iron post; on it a brass turntable and an iron fork (the yoke) hold the gun on its trunnions. The gun is
# a long iron barrel on a timber stock, wound with brass focusing coils, a spirit crystal clasped along the breech, two
# spade grips at the back, and a harpoon head at the front: three brass prongs, each with a back-swept barb, closing
# round a small crystal at the muzzle, where the beam leaves.
# Builds the model from nothing and exports it as FBX; with a second path, also renders preview PNGs.
# Contract: .docs/plans/airship-parts-pack.md ("The mesh contract for Blender parts"); manifest beside the FBX.
#
#   blender --background --factory-startup --python .tools/blender/ray_cannon.py -- <fbx path> [<preview png>]
#
# Everything is written in Unity's frame (Y up, +Z the barrel at rest, +X right) through U(), which turns it into
# Blender's; the export turns it back. One unit is one metre; the pivot is the centre of the deck under the post.
# The gameplay points are fixed by CannonStation.prefab and kept here: the yaw and pitch pivots at (0, 2.4, 0), the
# muzzle 2.3 m along the barrel, elevation -15..+40 degrees (CannonConfig), the gunner's Seat at (0, 1.05, -1.8).
# Three objects under the root: RayCannon_Base (static), RayCannon_Yoke (pivot on the yaw axis at the trunnions, turns
# about local Y) and RayCannon_Barrel (same pivot, pitches about local X). Every rotation lives in the mesh data.
import math
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FBX_PATH = args[0] if args else "ray_cannon.fbx"
PREVIEW_PATH = args[1] if len(args) > 1 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def U(x, y, z):
    """A point in Unity's frame (Y up, +Z forward) as Blender's (Z up, -Y forward); X flips with the import's mirror."""
    return Vector((-x, -z, y))


def srgb(hex_colour):
    """A palette hex (Assets/Art/Palette/palette.json) as linear RGB, for the preview's colours."""
    c = [int(hex_colour[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


def material(name, hex_colour, emission=0.0):
    rgb = srgb(hex_colour)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.diffuse_color = (*rgb, 1.0)
    return mat


# The names are the contract with CannonAssetBuilder, which swaps each for a shared ship material.
WOOD = material("RayCannon_Wood", "#8A5A36")      # M_ShipHull, timber warm
BRASS = material("RayCannon_Brass", "#B88A3E")    # M_ShipEngine
IRON = material("RayCannon_Iron", "#3E5E5E")      # M_ShipIron, iron teal
PAINT = material("RayCannon_Paint", "#B5402F")    # M_ShipPennant, lacquer red
SPIRIT = material("RayCannon_Spirit", "#8FE3F0", emission=1.5)  # M_ShipSpirit

PIVOT = (0.0, 2.4, 0.0)
MUZZLE_Z = 2.3


class Builder:
    """One object's geometry, in Unity-frame coordinates relative to its pivot; each face tagged with its material."""

    def __init__(self, materials):
        self.bm = bmesh.new()
        self.materials = materials

    def _tag(self, before, mat, smooth=True):
        index = self.materials.index(mat)
        for face in self.bm.faces:
            if face not in before:
                face.material_index = index
                face.smooth = smooth

    def rod(self, a, b, radius_a, radius_b, mat, segments=12, smooth=True):
        """A tapered cylinder from Unity point a to b; a zero radius makes a point (a cone or a crystal's tip)."""
        before = set(self.bm.faces)
        start, end = U(*a), U(*b)
        axis = end - start
        turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
        bmesh.ops.create_cone(self.bm, cap_ends=radius_a > 0.0 and radius_b > 0.0, segments=segments, radius1=radius_a,
                              radius2=radius_b, depth=axis.length, matrix=Matrix.Translation((start + end) * 0.5) @ turn)
        self._tag(before, mat, smooth)

    def sphere(self, centre, radius, mat, segments=12):
        before = set(self.bm.faces)
        bmesh.ops.create_uvsphere(self.bm, u_segments=segments, v_segments=max(6, segments // 2), radius=radius,
                                  matrix=Matrix.Translation(U(*centre)))
        self._tag(before, mat)

    def box(self, centre, size, mat, bevel=0.015):
        """An axis-aligned box (Unity size x, y, z) with its edges rounded."""
        before = set(self.bm.faces)
        x, y, z = size
        scale = Matrix.Diagonal((x, z, y, 1.0))  # Unity x, y, z -> Blender x, y (depth), z (up)
        made = bmesh.ops.create_cube(self.bm, size=1.0, matrix=Matrix.Translation(U(*centre)) @ scale)
        verts = made["verts"]
        edges = list({e for v in verts for e in v.link_edges})
        bmesh.ops.bevel(self.bm, geom=verts + edges, offset=bevel, segments=2, affect="EDGES", profile=0.5)
        self._tag(before, mat, smooth=False)

    def ring(self, centre, radius, tube, mat, axis="z", segments=24, sides=6):
        """A torus about centre with its axis along Unity's x, y or z."""
        before = set(self.bm.faces)
        bm = self.bm
        c = Vector(centre)
        a_dir = {"x": Vector((1, 0, 0)), "y": Vector((0, 1, 0)), "z": Vector((0, 0, 1))}[axis]
        u = Vector((0, 1, 0)) if axis == "x" else Vector((1, 0, 0))
        v = a_dir.cross(u)
        loops = []
        for i in range(segments):
            a = math.tau * i / segments
            out = u * math.cos(a) + v * math.sin(a)
            loop = []
            for j in range(sides):
                b = math.tau * j / sides
                p = c + out * (radius + tube * math.cos(b)) + a_dir * (tube * math.sin(b))
                loop.append(bm.verts.new(U(*p)))
            loops.append(loop)
        for i in range(segments):
            n = (i + 1) % segments
            for j in range(sides):
                m = (j + 1) % sides
                bm.faces.new((loops[i][j], loops[n][j], loops[n][m], loops[i][m]))
        self._tag(before, mat)

    def crystal(self, a, b, radius, mat):
        """A six-sided bipyramid from a to b, widest in the middle, cut flat (no smoothing)."""
        mid = tuple((p + q) * 0.5 for p, q in zip(a, b))
        self.rod(a, mid, 0.0, radius, mat, segments=6, smooth=False)
        self.rod(mid, b, radius, 0.0, mat, segments=6, smooth=False)

    def finish(self, name, pivot, parent):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in self.materials:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = U(*pivot)
        scene.collection.objects.link(obj)
        obj.parent = parent
        print(f"[ray_cannon] {name}: {sum(len(p.vertices) - 2 for p in mesh.polygons)} triangles")
        return obj


root = bpy.data.objects.new("RayCannon", None)
root.empty_display_type = "PLAIN_AXES"
scene.collection.objects.link(root)

# --- The base (static): plinth, tripod, post, bearing collar ---
base = Builder([WOOD, BRASS, IRON])
base.rod((0, 0, 0), (0, 0.14, 0), 0.50, 0.46, WOOD, segments=8, smooth=False)
for i in range(8):  # a brass rivet on each corner of the octagon
    a = math.tau * (i + 0.5) / 8
    c = (math.cos(a) * 0.42, math.sin(a) * 0.42)
    base.rod((c[0], 0.13, c[1]), (c[0], 0.165, c[1]), 0.03, 0.018, BRASS, segments=6)
base.rod((0, 0.14, 0), (0, 0.24, 0), 0.20, 0.15, BRASS, segments=16)
base.rod((0, 0.24, 0), (0, 1.70, 0), 0.12, 0.09, IRON, segments=12)
for y in (0.92, 1.42):
    base.rod((0, y, 0), (0, y + 0.06, 0), 0.115, 0.11, BRASS, segments=12)
# Three wooden braces, each a little different (hand-made), from a brass shoe on the plinth to the lower band.
for i, (lean, top) in enumerate(((0.0, 0.93), (0.05, 0.96), (-0.04, 0.94))):
    a = math.tau * i / 3 + math.tau / 12 + lean
    d = (math.cos(a), math.sin(a))
    foot = (d[0] * 0.38, 0.14, d[1] * 0.38)
    base.box((foot[0], 0.17, foot[2]), (0.1, 0.06, 0.1), BRASS, bevel=0.012)
    base.rod((foot[0], 0.18, foot[2]), (d[0] * 0.1, top, d[1] * 0.1), 0.04, 0.032, WOOD, segments=8)
# The bearing the gun turns on.
base.rod((0, 1.66, 0), (0, 1.76, 0), 0.14, 0.20, BRASS, segments=20)
base.finish("RayCannon_Base", (0, 0, 0), root)

# --- The yoke (turns about local Y): turntable, lacquered ring, the fork and its trunnions ---
yoke = Builder([BRASS, IRON, PAINT])
yoke.rod((0, -0.64, 0), (0, -0.57, 0), 0.27, 0.26, BRASS, segments=20)
yoke.rod((0, -0.57, 0), (0, -0.53, 0), 0.24, 0.22, PAINT, segments=20)
for side in (1.0, -1.0):
    x = side * 0.255
    yoke.box((x, -0.27, 0.0), (0.06, 0.52, 0.2), IRON)  # the cheek
    yoke.rod((x - 0.03, 0.0, 0.0), (x + 0.03, 0.0, 0.0), 0.11, 0.11, IRON, segments=14)  # its rounded top
    yoke.rod((x + 0.03, 0.0, 0.0), (x + 0.07, 0.0, 0.0), 0.075, 0.055, BRASS, segments=12)  # the trunnion cap
    for z in (0.06, -0.06):  # rivets on the outer face
        yoke.rod((x + side * 0.025, -0.46, z), (x + side * 0.05, -0.46, z), 0.025, 0.014, BRASS, segments=6)
yoke.rod((-0.23, 0.0, 0.0), (0.23, 0.0, 0.0), 0.045, 0.045, IRON, segments=10)  # the trunnion pin through the breech
yoke.finish("RayCannon_Yoke", PIVOT, root)

# --- The barrel (pitches about local X) ---
gun = Builder([IRON, WOOD, BRASS, PAINT, SPIRIT])
# The breech: an iron drum, rounded off behind, banded in brass, with a lacquered band in the middle.
gun.rod((0, 0, -0.40), (0, 0, 0.20), 0.165, 0.15, IRON, segments=16)
gun.rod((0, 0, -0.40), (0, 0, -0.50), 0.165, 0.10, IRON, segments=16)
gun.sphere((0, 0, -0.51), 0.085, BRASS, segments=12)
for z in (-0.38, 0.17):
    gun.rod((0, 0, z), (0, 0, z + 0.05), 0.178, 0.178, BRASS, segments=16)
gun.rod((0, 0, -0.13), (0, 0, -0.05), 0.172, 0.17, PAINT, segments=16)
# The spirit crystal along the top of the breech, in two brass clasps on short posts.
gun.crystal((0, 0.25, -0.34), (0, 0.25, 0.14), 0.065, SPIRIT)
for z in (-0.22, 0.03):
    gun.rod((0, 0.25, z - 0.025), (0, 0.25, z + 0.025), 0.068, 0.068, BRASS, segments=8)
    gun.rod((0, 0.15, z), (0, 0.20, z), 0.022, 0.018, BRASS, segments=8)
# The spade grips behind, the gunner's handles, tipped with brass knobs.
for side in (1.0, -1.0):
    gun.rod((side * 0.10, -0.08, -0.36), (side * 0.19, -0.30, -0.74), 0.032, 0.028, WOOD, segments=8)
    gun.sphere((side * 0.19, -0.31, -0.76), 0.045, BRASS, segments=10)
# The timber stock under the barrel, strapped to it with brass bands.
gun.box((0, -0.125, 0.6), (0.13, 0.09, 0.9), WOOD, bevel=0.025)
for z in (0.35, 0.95):
    gun.box((0, -0.1, z), (0.155, 0.23, 0.05), BRASS, bevel=0.012)
# The iron barrel, slim and tapering, wound with three focusing collars that shrink and flare toward the head.
gun.rod((0, 0, 0.20), (0, 0, 1.78), 0.085, 0.065, IRON, segments=14)
for z, r in ((1.12, 0.125), (1.34, 0.115), (1.54, 0.105)):
    gun.rod((0, 0, z - 0.04), (0, 0, z + 0.04), r * 0.8, r, BRASS, segments=16)
gun.rod((0, 0, 1.64), (0, 0, 1.70), 0.082, 0.082, SPIRIT, segments=14)  # a glowing band behind the head
# The harpoon head: a brass collar, three prongs swelling out and closing round the muzzle crystal, each with a barb.
gun.rod((0, 0, 1.74), (0, 0, 1.86), 0.09, 0.11, BRASS, segments=14)
for i in range(3):
    a = math.tau * i / 3 + math.tau / 4  # one prong on top
    d = (math.cos(a), math.sin(a))
    root_p = (d[0] * 0.09, d[1] * 0.09, 1.84)
    elbow = (d[0] * 0.17, d[1] * 0.17, 2.04)
    tip = (d[0] * 0.075, d[1] * 0.075, MUZZLE_Z + 0.02)
    barb = (d[0] * 0.23, d[1] * 0.23, 1.92)
    gun.rod(root_p, elbow, 0.042, 0.036, BRASS, segments=8)
    gun.sphere(elbow, 0.04, BRASS, segments=8)
    gun.rod(elbow, tip, 0.036, 0.0, BRASS, segments=8)
    gun.rod(elbow, barb, 0.03, 0.0, BRASS, segments=6)
gun.crystal((0, 0, 1.86), (0, 0, MUZZLE_Z - 0.02), 0.055, SPIRIT)
gun.finish("RayCannon_Barrel", PIVOT, root)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"EMPTY", "MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
print(f"[ray_cannon] exported {FBX_PATH}")

if PREVIEW_PATH:
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1000
    world = bpy.data.worlds.new("Sky")
    world.color = srgb("#8FB8D8")
    scene.world = world
    # A stand-in for the 2 m gunner at the Seat, for scale (not exported).
    body = Builder([material("Preview_Body", "#6A6E9C")])
    body.rod((0, 0.0, -1.8), (0, 1.55, -1.8), 0.22, 0.22, body.materials[0], segments=12)
    body.sphere((0, 1.75, -1.8), 0.2, body.materials[0], segments=12)
    body.finish("GunnerStandIn", (0, 0, 0), None)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    barrel = bpy.data.objects["RayCannon_Barrel"]
    # (camera, target, suffix, elevation): three-quarter, side at full elevation (clearance), the gunner's camera.
    views = [
        (U(3.4, 3.2, 3.6), U(0, 1.5, 0.6), "", 0.0),
        (U(4.6, 1.9, 0.6), U(0, 1.6, 0.6), "_side_up", 40.0),
        (U(4.6, 1.9, 0.6), U(0, 1.6, 0.6), "_side_down", -15.0),
        (U(0, 3.5, -3.0), U(0, 3.08, 0.0), "_gunner", 0.0),
    ]
    for location, target, suffix, elevation in views:
        barrel.rotation_euler = (math.radians(-elevation), 0.0, 0.0)  # Blender's +X turns the nose down here
        cam.data.angle = math.radians(60.0 if suffix == "_gunner" else 40.0)
        cam.location = location
        cam.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()
        path = PREVIEW_PATH.replace(".png", f"{suffix}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print(f"[ray_cannon] preview {path}")
