# The rotor mast (ship part hull.rotor_mast): a mast carrying two lift rotors on a vertical axis and two pennants.
# Builds the model from nothing and exports it as FBX; with a second path, also renders a preview PNG.
# Contract: .docs/plans/airship-parts-pack.md ("The mesh contract for Blender parts"); manifest beside the FBX.
#
#   blender --background --factory-startup --python .tools/blender/rotor_mast.py -- <fbx path> [<preview png>]
#
# Blender is Z-up with the bow along -Y; the export turns that into Unity's Y-up with the bow along +Z, so the pennants
# (Blender +Y) stream along Unity -Z, aft. One unit is one metre; the pivot is the node the mast stands on.
# Three objects under the root: RotorMast_Mast (static), RotorLow and RotorHigh (each pivoted on its hub, spinning about
# local up). Every rotation lives in the mesh data: the FBX export's axis bake mishandles rotated children.
import math
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FBX_PATH = args[0] if args else "rotor_mast.fbx"
PREVIEW_PATH = args[1] if len(args) > 1 else None

# Aft, in Blender space: the pennants stream this way.
AFT = Vector((0.0, 1.0, 0.0))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, rgb, metallic=0.0, roughness=0.6):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (*rgb, 1.0)
    return mat


# The names are the contract with ShipDesignsAssetBuilder, which swaps each for a shared ship material.
IRON = material("RotorMast_Iron", (0.20, 0.30, 0.30), metallic=0.6, roughness=0.45)
BRASS = material("RotorMast_Brass", (0.62, 0.45, 0.20), metallic=0.8, roughness=0.35)
WOOD = material("RotorMast_Wood", (0.36, 0.22, 0.13))
CANVAS = material("RotorMast_Canvas", (0.86, 0.82, 0.70), roughness=0.8)
PENNANT = material("RotorMast_Pennant", (0.72, 0.18, 0.13), roughness=0.7)


class Builder:
    """One object's geometry: pieces added in place, each face tagged with its material."""

    def __init__(self, materials):
        self.bm = bmesh.new()
        self.materials = materials

    def _tag(self, before, mat, smooth=True):
        index = self.materials.index(mat)
        for face in self.bm.faces:
            if face not in before:
                face.material_index = index
                face.smooth = smooth

    def cylinder(self, radius_bottom, radius_top, z0, z1, mat, segments=16):
        before = set(self.bm.faces)
        bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=radius_bottom, radius2=radius_top, depth=z1 - z0,
                              matrix=Matrix.Translation((0.0, 0.0, (z0 + z1) * 0.5)))
        self._tag(before, mat)

    def sphere(self, radius, z, mat):
        before = set(self.bm.faces)
        bmesh.ops.create_uvsphere(self.bm, u_segments=16, v_segments=8, radius=radius, matrix=Matrix.Translation((0.0, 0.0, z)))
        self._tag(before, mat)

    def spar(self, root, length, radius, turn):
        """An iron rod along the arm turned `turn` about up, from `root` out: a blade's leading spar."""
        before = set(self.bm.faces)
        bmesh.ops.create_cone(self.bm, cap_ends=True, segments=8, radius1=radius, radius2=radius * 0.6, depth=length,
                              matrix=turn @ Matrix.Translation((root + length * 0.5, -0.02, 0.03)) @ Matrix.Rotation(math.radians(90), 4, "Y"))
        self._tag(before, IRON)

    def blade(self, root, length, chord_root, chord_tip, pitch_deg, turn):
        """A feathered canvas blade along the arm turned `turn`, tapering to a rounded tip, pitched about its length."""
        before = set(self.bm.faces)
        bm = self.bm
        stations = 7
        thickness = 0.025
        top, bottom = [], []
        for i in range(stations + 1):
            t = i / stations
            x = root + length * t
            chord = chord_root + (chord_tip - chord_root) * t
            if t > 0.8:  # round the tip
                chord *= math.sqrt(max(0.0, 1.0 - ((t - 0.8) / 0.2) ** 2)) * 0.9 + 0.1
            sweep = 0.12 * chord_root * t * t  # the trailing edge sweeps back a little, like a feather
            lead, trail = -chord * 0.4 + sweep, chord * 0.6 + sweep
            top.append((bm.verts.new((x, lead, thickness)), bm.verts.new((x, trail, thickness))))
            bottom.append((bm.verts.new((x, lead, -thickness)), bm.verts.new((x, trail, -thickness))))
        for i in range(stations):
            bm.faces.new((top[i][0], top[i + 1][0], top[i + 1][1], top[i][1]))
            bm.faces.new((bottom[i][0], bottom[i][1], bottom[i + 1][1], bottom[i + 1][0]))
            bm.faces.new((top[i][0], bottom[i][0], bottom[i + 1][0], top[i + 1][0]))
            bm.faces.new((top[i][1], top[i + 1][1], bottom[i + 1][1], bottom[i][1]))
        bm.faces.new((top[0][0], top[0][1], bottom[0][1], bottom[0][0]))
        bm.faces.new((top[-1][0], bottom[-1][0], bottom[-1][1], top[-1][1]))
        verts = [v for pair in top + bottom for v in pair]
        bmesh.ops.transform(bm, verts=verts, matrix=turn @ Matrix.Rotation(math.radians(pitch_deg), 4, "X"))
        self._tag(before, CANVAS, smooth=False)

    def pennant(self, z_top, height, length):
        """A red swallow-tailed pennant from the pole, streaming aft with a gentle wave."""
        before = set(self.bm.faces)
        bm = self.bm
        columns = 8
        rows = []
        for i in range(columns + 1):
            t = i / columns
            along = 0.09 + length * t
            half = height * 0.5 * (1.0 - 0.75 * t)
            notch = 0.0 if t < 0.85 else (t - 0.85) / 0.15 * half * 0.9
            side = Vector((0.06 * math.sin(t * math.pi * 2.2) * t, 0.0, 0.0))
            centre = z_top - height * 0.5 - 0.08 * t
            rows.append([bm.verts.new(AFT * along + side + Vector((0.0, 0.0, centre + half))),
                         bm.verts.new(AFT * (along - notch) + side + Vector((0.0, 0.0, centre))),
                         bm.verts.new(AFT * along + side + Vector((0.0, 0.0, centre - half)))])
        cloth = []
        for i in range(columns):
            for j in range(2):
                cloth.append(bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1])))
        # A little thickness, so the cloth shows from both sides under back-face culling.
        bmesh.ops.solidify(bm, geom=cloth, thickness=0.015)
        self._tag(before, PENNANT)

    def finish(self, name, z=0.0, parent=None):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat in self.materials:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = (0.0, 0.0, z)
        scene.collection.objects.link(obj)
        obj.parent = parent
        return obj


def rotor(name, z, blades, length, chord_root, chord_tip, hub_radius, offset_deg, parent):
    """A rotor pivoted on its hub at height z: the hub and cap, and its blades each with a spar."""
    b = Builder([BRASS, CANVAS, IRON])
    b.cylinder(hub_radius, hub_radius * 0.8, -0.14, 0.14, BRASS)
    b.cylinder(hub_radius * 0.6, 0.02, 0.14, 0.34, BRASS, segments=12)
    for i in range(blades):
        turn = Matrix.Rotation(math.radians(offset_deg + 360.0 / blades * i), 4, "Z")
        b.blade(hub_radius * 0.8, length, chord_root, chord_tip, 9.0, turn)
        b.spar(hub_radius * 0.6, length * 0.9, 0.035, turn)
    return b.finish(name, z, parent)


root = bpy.data.objects.new("RotorMast", None)
root.empty_display_type = "PLAIN_AXES"
scene.collection.objects.link(root)

mast = Builder([WOOD, BRASS, IRON, PENNANT])
# The step on the deck: an octagonal wooden plinth and a brass collar.
mast.cylinder(0.46, 0.42, 0.0, 0.12, WOOD, segments=8)
mast.cylinder(0.30, 0.24, 0.12, 0.42, BRASS)
# The pole, tapering, banded in brass.
mast.cylinder(0.15, 0.09, 0.12, 7.35, IRON)
for z in (1.4, 2.6, 4.9, 5.7):
    mast.cylinder(0.17 - z * 0.008, 0.17 - z * 0.008, z, z + 0.08, BRASS)
# A gear housing under each rotor, and the finial.
mast.cylinder(0.22, 0.16, 3.25, 3.45, BRASS)
mast.cylinder(0.26, 0.18, 5.95, 6.15, BRASS)
mast.sphere(0.12, 7.42, BRASS)
mast.cylinder(0.05, 0.0, 7.5, 7.85, BRASS, segments=8)
mast.pennant(7.38, 0.34, 0.95)
mast.pennant(4.75, 0.26, 0.7)
mast.finish("RotorMast_Mast", parent=root)

# Low: 3 m across (inside a 3 x 3 block layer); high: 4.8 m across (inside 5 x 5).
rotor("RotorLow", 3.6, 4, 1.28, 0.46, 0.24, 0.16, 45.0, root)
rotor("RotorHigh", 6.35, 4, 2.2, 0.72, 0.34, 0.22, 0.0, root)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"EMPTY", "MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
print(f"[rotor_mast] exported {FBX_PATH}")

if PREVIEW_PATH:
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1100
    world = bpy.data.worlds.new("Sky")
    world.color = (0.62, 0.72, 0.78)
    scene.world = world
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    cam.location = (8.5, -9.5, 12.5)
    cam.rotation_euler = (Vector((0.0, 0.0, 4.2)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    print(f"[rotor_mast] preview {PREVIEW_PATH}")
