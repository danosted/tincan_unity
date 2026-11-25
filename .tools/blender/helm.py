# The helm (core part core.helm, the HelmStation fixture): an airship console the helmsman steers from. A wooden plinth
# and an iron column carry a brass gearbox with a 1.1 m wheel on its aft face (yaw), and two levers on brass quadrants
# either side: throttle to starboard, pitch to port. On the gearbox, behind the wheel, a short brass stalk carries a cup
# with a thin brass ring at the glass's equator (fixed to the ship, so it shows the deck's tilt): the navigation compass
# sits in it (COMPASS_SEAT; NavigationConfig.CompassLocalPosition). From the helmsman's eyes it is about 13 degrees down
# and 1.3 m away, seen over the wheel's rim between its two top handles, and its top stays under the horizon.
# Builds the model from nothing and exports it as FBX; with a second path, also renders a preview PNG.
# Contract: .docs/plans/airship-parts-pack.md ("The mesh contract for Blender parts"); manifest beside the FBX.
#
#   blender --background --factory-startup --python .tools/blender/helm.py -- <fbx path> [<preview png>]
#
# Everything is written in Unity's frame (Y up, +Z the bow, +X starboard; the helmsman stands aft, at -Z, facing +Z)
# through U(), which turns it into Blender's (Z up, bow along -Y); the export turns it back. One unit is one metre;
# the pivot is the centre of the deck under the column. Sized for the 2 m crew body (eyes at 1.85 m): the wheel's hub is
# at chest height (1.15 m) and its rim tops out at 1.74 m, under the helmsman's eye line.
# Four objects under the root: Helm_Stand (static), Helm_Wheel (pivot on its hub, turns about local Z),
# Helm_ThrottleLever and Helm_PitchLever (pivot on their axles, swing about local X). Every rotation lives in the mesh
# data: the FBX export's axis bake mishandles rotated children.
import math
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FBX_PATH = args[0] if args else "helm.fbx"
PREVIEW_PATH = args[1] if len(args) > 1 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def U(x, y, z):
    """A point or direction in Unity's frame (Y up, +Z forward) as Blender's (Z up, -Y forward). X flips too: the
    right-handed to left-handed import mirrors it, so starboard here lands on Unity's +X (checked on import)."""
    return Vector((-x, -z, y))


def material(name, rgb, metallic=0.0, roughness=0.6, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.diffuse_color = (*rgb, 1.0)
    return mat


# The names are the contract with HelmAssetBuilder, which swaps each for a shared ship material.
WOOD = material("Helm_Wood", (0.36, 0.22, 0.13))
BRASS = material("Helm_Brass", (0.62, 0.45, 0.20), metallic=0.8, roughness=0.35)
IRON = material("Helm_Iron", (0.20, 0.30, 0.30), metallic=0.6, roughness=0.45)
PAINT = material("Helm_Paint", (0.72, 0.18, 0.13), roughness=0.7)

# Unity-frame layout, metres from the pivot on the deck.
WHEEL_HUB = (0.0, 1.15, -0.33)
WHEEL_RIM = 0.42
WHEEL_REACH = 0.56  # handle tips
LEVER_AXLE_HEIGHT = 0.86
LEVER_AXLE_Z = -0.05
LEVER_SIDE = 0.42  # throttle at +X (starboard), pitch at -X (port)
# The compass glass's centre (0.30 m across) over the gearbox; keep NavigationConfig.CompassLocalPosition on it.
COMPASS_SEAT = (0.0, 1.55, 0.05)
COMPASS_RADIUS = 0.15


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
        """A tapered cylinder from Unity point a to b."""
        before = set(self.bm.faces)
        start, end = U(*a), U(*b)
        axis = end - start
        turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
        bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=radius_a, radius2=radius_b, depth=axis.length,
                              matrix=Matrix.Translation((start + end) * 0.5) @ turn)
        self._tag(before, mat, smooth)

    def sphere(self, centre, radius, mat, segments=12):
        before = set(self.bm.faces)
        bmesh.ops.create_uvsphere(self.bm, u_segments=segments, v_segments=max(6, segments // 2), radius=radius,
                                  matrix=Matrix.Translation(U(*centre)))
        self._tag(before, mat)

    def ring(self, centre, radius, tube, mat, segments=32, sides=8, flat=False):
        """A torus about centre: in the Unity XY plane (its axis along Z, the wheel's axle), or level (axis Y) if flat."""
        before = set(self.bm.faces)
        bm = self.bm
        c = Vector(centre)
        axis = Vector((0.0, 1.0, 0.0)) if flat else Vector((0.0, 0.0, 1.0))
        loops = []
        for i in range(segments):
            a = math.tau * i / segments
            out = Vector((math.cos(a), 0.0, math.sin(a))) if flat else Vector((math.cos(a), math.sin(a), 0.0))
            loop = []
            for j in range(sides):
                b = math.tau * j / sides
                p = c + out * (radius + tube * math.cos(b)) + axis * (tube * math.sin(b))
                loop.append(bm.verts.new(U(*p)))
            loops.append(loop)
        for i in range(segments):
            n = (i + 1) % segments
            for j in range(sides):
                m = (j + 1) % sides
                bm.faces.new((loops[i][j], loops[n][j], loops[n][m], loops[i][m]))
        self._tag(before, mat)

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
        return obj


root = bpy.data.objects.new("Helm", None)
root.empty_display_type = "PLAIN_AXES"
scene.collection.objects.link(root)

# --- The stand (static) ---
stand = Builder([WOOD, BRASS, IRON])
# An octagonal wooden plinth on the deck, a brass foot collar, and the iron column banded in brass.
stand.rod((0, 0, 0), (0, 0.10, 0), 0.40, 0.36, WOOD, segments=8, smooth=False)
stand.rod((0, 0.10, 0), (0, 0.22, 0), 0.22, 0.17, BRASS, segments=16)
stand.rod((0, 0.22, 0), (0, 1.02, 0), 0.12, 0.09, IRON, segments=12)
for y in (0.46, 0.80):
    stand.rod((0, y, 0), (0, y + 0.05, 0), 0.125, 0.125, BRASS, segments=12)
# The gearbox: a brass drum on its side, its axle running aft through the wheel's hub.
stand.rod((0, 1.15, 0.13), (0, 1.15, -0.17), 0.15, 0.15, BRASS, segments=16)
stand.rod((0, 1.15, 0.13), (0, 1.15, 0.17), 0.12, 0.08, BRASS, segments=16)
stand.rod((0, 1.15, -0.17), (0, 1.15, -0.27), 0.035, 0.035, IRON, segments=8)
# The compass mount on the gearbox, behind the wheel: a short stalk, a cup flaring up under the glass, and a thin ring
# round the glass's equator (COMPASS_SEAT).
seat_y, seat_z = COMPASS_SEAT[1], COMPASS_SEAT[2]
cup_base = seat_y - COMPASS_RADIUS - 0.04
stand.rod((0, 1.27, seat_z), (0, cup_base, seat_z), 0.035, 0.03, BRASS, segments=12)
stand.rod((0, cup_base, seat_z), (0, cup_base + 0.07, seat_z), 0.04, 0.10, BRASS, segments=20)
stand.ring(COMPASS_SEAT, COMPASS_RADIUS + 0.006, 0.008, BRASS, segments=40, sides=6, flat=True)
# The lever brackets: an iron arm out of the column each side, ending in a brass quadrant plate the lever swings on.
for side in (1.0, -1.0):
    stand.rod((side * 0.08, 0.78, 0.0), (side * 0.36, 0.84, LEVER_AXLE_Z), 0.03, 0.025, IRON, segments=8)
    stand.rod((side * 0.355, LEVER_AXLE_HEIGHT, LEVER_AXLE_Z), (side * 0.385, LEVER_AXLE_HEIGHT, LEVER_AXLE_Z), 0.16, 0.16, BRASS, segments=20)
stand.finish("Helm_Stand", (0, 0, 0), root)

# --- The wheel (turns about local Z) ---
wheel = Builder([WOOD, BRASS])
wheel.rod((0, 0, 0.07), (0, 0, -0.07), 0.09, 0.08, BRASS, segments=16)
wheel.ring((0, 0, 0), 0.17, 0.02, BRASS, segments=24, sides=6)
wheel.ring((0, 0, 0), WHEEL_RIM, 0.035, WOOD, segments=40, sides=8)
for i in range(8):
    a = math.tau * i / 8 + math.tau / 16
    d = (math.cos(a), math.sin(a), 0.0)
    wheel.rod((d[0] * 0.08, d[1] * 0.08, 0), (d[0] * WHEEL_RIM, d[1] * WHEEL_RIM, 0), 0.026, 0.022, WOOD, segments=8)
    wheel.rod((d[0] * (WHEEL_RIM + 0.03), d[1] * (WHEEL_RIM + 0.03), 0), (d[0] * WHEEL_REACH, d[1] * WHEEL_REACH, 0), 0.028, 0.022, WOOD, segments=8)
    wheel.sphere((d[0] * WHEEL_REACH, d[1] * WHEEL_REACH, 0), 0.032, WOOD, segments=10)
wheel.finish("Helm_Wheel", WHEEL_HUB, root)


# --- The levers (swing about local X) ---
def lever(name, side, knob):
    b = Builder([IRON, WOOD, BRASS, PAINT])
    b.rod((side * -0.03, 0, 0), (side * 0.05, 0, 0), 0.04, 0.04, BRASS, segments=12)
    b.rod((side * 0.03, 0, 0), (side * 0.03, 0.40, 0), 0.022, 0.018, IRON, segments=8)
    b.rod((side * 0.03, 0.40, 0), (side * 0.03, 0.52, 0), 0.028, 0.026, WOOD, segments=10)
    b.sphere((side * 0.03, 0.54, 0), 0.036, knob, segments=12)
    return b.finish(name, (side * LEVER_SIDE, LEVER_AXLE_HEIGHT, LEVER_AXLE_Z), root)


lever("Helm_ThrottleLever", 1.0, PAINT)
lever("Helm_PitchLever", -1.0, BRASS)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"EMPTY", "MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
print(f"[helm] exported {FBX_PATH}")

if PREVIEW_PATH:
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1000
    world = bpy.data.worlds.new("Sky")
    world.color = (0.62, 0.72, 0.78)
    scene.world = world
    # A stand-in for the navigation compass in its cup (not exported: Unity places the real one).
    stand_in = Builder([material("Preview_Glass", (0.75, 0.88, 0.95)), BRASS])
    stand_in.sphere(COMPASS_SEAT, COMPASS_RADIUS, stand_in.materials[0], segments=20)
    stand_in.finish("CompassStandIn", (0, 0, 0), None)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    # From behind the helmsman's right shoulder, then from the helmsman's eyes at the Seat, looking a little down.
    # The eye view looks level, with the game's first-person field of view (75 degrees tall).
    cam.data.sensor_fit = "VERTICAL"
    views = [(U(1.6, 2.4, -2.6), U(0.0, 0.95, 0.0), "", math.radians(39.6)), (U(0.0, 1.85, -1.2), U(0.0, 1.85, 2.0), "_eye", math.radians(75.0))]
    for location, target, suffix, fov in views:
        cam.data.angle = fov
        cam.location = location
        cam.rotation_euler = (target - location).to_track_quat("-Z", "Y").to_euler()
        path = PREVIEW_PATH.replace(".png", f"{suffix}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print(f"[helm] preview {path}")
