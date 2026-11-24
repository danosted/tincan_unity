# The crew body (crew-animation S2): a chunky 2 m crewmate on a humanoid skeleton, with its movement clips.
# Builds the model and its clips from nothing and exports one FBX; with a second path (a folder), also renders a preview
# PNG of every clip there.
# Plan: .docs/plans/crew-animation.md; manifest beside the FBX; Unity side: DevTools/Editor/CrewBodyAssetBuilder.cs.
#
#   blender --background --factory-startup --python .tools/blender/crew_body.py -- <fbx path> [<preview folder>]
#
# Blender is Z-up and the body is built facing -Y. Unity reads the FBX's axis header, so for an armature the export
# axes cancel out and -Y lands on Unity's -Z: the armature object is therefore turned half round just before export,
# and the body faces +Z, the player's forward (CrewBodyModelTests checks it). One unit is one metre and
# the feet stand on the origin. The bones carry Unity's humanoid names (HumanTrait.BoneName), so the avatar maps by name
# and real art on the same skeleton can replace this placeholder. The mesh is one skinned mesh made of rounded parts,
# each rigidly on one bone (balls at the elbows and knees hide the joints).
#
# Clips are in place (no root motion), looping, keyed every frame at 60 fps:
#   Idle    3 s   breathing.
#   Run     0.6 s at the walk speed, 7 m/s.     Sprint 0.4 s at 12.6 m/s (walk speed x the sprint multiplier).
#   Jump    1 s   rising, knees tucked.          Fall   1 s    arms out, legs pedalling.
# A leg swings as theta = A sin(phase); its foot moves at about LEG * A * 2 pi / T at mid-stance, so A is chosen to
# match the clip's ground speed and the feet do not skate much.
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FBX_PATH = args[0] if args else "crew_body.fbx"
PREVIEW_DIR = args[1] if len(args) > 1 else None

FPS = 60
TAU = 2.0 * math.pi
LEG = 0.94  # hip joint to sole, metres
WALK_SPEED = 7.0  # NetworkPlayer.prefab HumanoidControllerView: walk speed and sprint multiplier
SPRINT_SPEED = 7.0 * 1.8

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = FPS


def srgb(hex_colour):
    def channel(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(channel(int(hex_colour[i:i + 2], 16)) for i in (1, 3, 5))


def material(name, hex_colour):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    rgb = srgb(hex_colour)
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1.0)
    mat.diffuse_color = (*rgb, 1.0)
    return mat


# The names are the contract with CrewBodyAssetBuilder, which swaps each for a shared library material.
MATS = {
    "Tunic": material("CrewBody_Tunic", "#E6DCC0"),        # canvas
    "Trousers": material("CrewBody_Trousers", "#3E5E5E"),  # iron teal
    "Skin": material("CrewBody_Skin", "#C9A473"),          # deck
    "Boots": material("CrewBody_Boots", "#9E4A28"),        # russet
    "Scarf": material("CrewBody_Scarf", "#B5402F"),        # lacquer red
    "Hair": material("CrewBody_Hair", "#5A3A24"),          # timber dark
}

# ---------------------------------------------------------------- skeleton (T-pose; name, head, tail, parent)
BONES = [
    ("Hips", (0, 0, 0.98), (0, 0, 1.08), None),
    ("Spine", (0, 0, 1.08), (0, 0, 1.30), "Hips"),
    ("Chest", (0, 0, 1.30), (0, 0, 1.54), "Spine"),
    ("Neck", (0, 0, 1.54), (0, 0, 1.65), "Chest"),
    ("Head", (0, 0, 1.65), (0, 0, 2.00), "Neck"),
]
for side, s in (("Left", 1), ("Right", -1)):
    BONES += [
        (f"{side}Shoulder", (s * 0.05, 0, 1.49), (s * 0.19, 0, 1.49), "Chest"),
        (f"{side}UpperArm", (s * 0.19, 0, 1.49), (s * 0.49, 0, 1.49), f"{side}Shoulder"),
        (f"{side}LowerArm", (s * 0.49, 0, 1.49), (s * 0.75, 0, 1.49), f"{side}UpperArm"),
        (f"{side}Hand", (s * 0.75, 0, 1.49), (s * 0.88, 0, 1.49), f"{side}LowerArm"),
        (f"{side}UpperLeg", (s * 0.11, 0, 0.94), (s * 0.11, 0, 0.52), "Hips"),
        (f"{side}LowerLeg", (s * 0.11, 0, 0.52), (s * 0.11, 0, 0.10), f"{side}UpperLeg"),
        (f"{side}Foot", (s * 0.11, 0, 0.10), (s * 0.11, -0.14, 0.03), f"{side}LowerLeg"),
        (f"{side}Toes", (s * 0.11, -0.14, 0.03), (s * 0.11, -0.24, 0.03), f"{side}Foot"),
    ]

arm_data = bpy.data.armatures.new("CrewBody")
arm = bpy.data.objects.new("CrewBody", arm_data)
scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
for name, head, tail, parent in BONES:
    bone = arm_data.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    bone.roll = 0.0
    if parent:
        bone.parent = arm_data.edit_bones[parent]
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------- mesh: rounded parts, each on one bone
parts = []


def part(name, bone, mat, build):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    build(bm)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.data.materials.append(MATS[mat])
    group = obj.vertex_groups.new(name=bone)
    group.add(range(len(mesh.vertices)), 1.0, "REPLACE")
    parts.append(obj)


def ellipsoid(centre, radii, segments=18, rings=12):
    def build(bm):
        verts = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)["verts"]
        bmesh.ops.scale(bm, vec=radii, verts=verts)
        bmesh.ops.translate(bm, vec=centre, verts=verts)
    return build


def capsule(a, b, ra, rb, segments=16, rings=12):
    """A capsule from a to b: a hemisphere of radius ra at a, of rb at b, a tapered tube between."""
    a, b = Vector(a), Vector(b)

    def build(bm):
        verts = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)["verts"]
        length = (b - a).length
        for v in verts:
            if v.co.z > 1e-4:
                v.co = Vector((v.co.x * rb, v.co.y * rb, v.co.z * rb + length))
            else:
                v.co = Vector((v.co.x * ra, v.co.y * ra, v.co.z * ra))
        rotation = Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=Matrix.Translation(a) @ rotation, verts=verts)
    return build


part("Pelvis", "Hips", "Trousers", ellipsoid((0, 0, 0.95), (0.20, 0.14, 0.13)))
part("Belt", "Hips", "Boots", ellipsoid((0, 0, 1.06), (0.205, 0.148, 0.045)))
part("Belly", "Spine", "Tunic", ellipsoid((0, 0, 1.18), (0.19, 0.14, 0.15)))
part("Chest", "Chest", "Tunic", ellipsoid((0, 0, 1.41), (0.235, 0.155, 0.17)))
part("Scarf", "Chest", "Scarf", ellipsoid((0, -0.005, 1.56), (0.13, 0.12, 0.05)))
part("ScarfTail", "Chest", "Scarf", capsule((0.05, 0.11, 1.55), (0.09, 0.17, 1.36), 0.04, 0.03))
part("Neck", "Neck", "Skin", capsule((0, 0, 1.52), (0, 0, 1.68), 0.06, 0.06))
part("Head", "Head", "Skin", ellipsoid((0, -0.01, 1.83), (0.165, 0.175, 0.185), 24, 16))
part("Hair", "Head", "Hair", ellipsoid((0, 0.02, 1.875), (0.175, 0.18, 0.16), 24, 16))
part("Nose", "Head", "Skin", ellipsoid((0, -0.185, 1.81), (0.03, 0.035, 0.035), 10, 8))
for side, s in (("Left", 1), ("Right", -1)):
    part(f"{side}Eye", "Head", "Hair", ellipsoid((s * 0.062, -0.165, 1.86), (0.022, 0.014, 0.03), 10, 8))
    part(f"{side}ShoulderBall", f"{side}UpperArm", "Tunic", ellipsoid((s * 0.20, 0, 1.47), (0.10, 0.10, 0.09)))
    part(f"{side}Sleeve", f"{side}UpperArm", "Tunic", capsule((s * 0.19, 0, 1.49), (s * 0.47, 0, 1.49), 0.075, 0.066))
    part(f"{side}Elbow", f"{side}LowerArm", "Tunic", ellipsoid((s * 0.49, 0, 1.49), (0.066, 0.066, 0.066)))
    part(f"{side}Forearm", f"{side}LowerArm", "Tunic", capsule((s * 0.49, 0, 1.49), (s * 0.72, 0, 1.49), 0.064, 0.056))
    part(f"{side}Hand", f"{side}Hand", "Skin", ellipsoid((s * 0.81, 0, 1.48), (0.075, 0.048, 0.062)))
    part(f"{side}Thigh", f"{side}UpperLeg", "Trousers", capsule((s * 0.11, 0, 0.94), (s * 0.11, 0, 0.54), 0.095, 0.08))
    part(f"{side}Knee", f"{side}LowerLeg", "Trousers", ellipsoid((s * 0.11, 0, 0.52), (0.079, 0.079, 0.079)))
    part(f"{side}Shin", f"{side}LowerLeg", "Trousers", capsule((s * 0.11, 0, 0.52), (s * 0.11, 0, 0.18), 0.075, 0.065))
    part(f"{side}BootCuff", f"{side}LowerLeg", "Boots", capsule((s * 0.11, 0, 0.25), (s * 0.11, 0, 0.11), 0.083, 0.083))
    part(f"{side}Boot", f"{side}Foot", "Boots", ellipsoid((s * 0.11, -0.06, 0.068), (0.086, 0.16, 0.07)))

bpy.ops.object.select_all(action="DESELECT")
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "CrewBody_Mesh"
body.data.name = "CrewBody_Mesh"
body.parent = arm
body.modifiers.new("Armature", "ARMATURE").object = arm

# ---------------------------------------------------------------- poses
# Each bone's rotation is given in armature axes, relative to its parent as at rest; to_local turns it into the pose
# bone's own rotation. +X is the body's left, -Y its front, +Z up.


def rx(a):
    return Quaternion((1, 0, 0), a)


def ry(a):
    return Quaternion((0, 1, 0), a)


def rz(a):
    return Quaternion((0, 0, 1), a)


REST = {b.name: b.matrix_local.to_quaternion() for b in arm_data.bones}


def to_local(name, rotation):
    rest = REST[name]
    return rest.inverted() @ rotation @ rest


def arms_down(pose, down, swing_left=0.0, swing_right=0.0, elbow=0.2):
    """Arms hanging `down` radians from the T-pose, swung forward by the swing angles, elbows bent forward."""
    for side, sign, swing in (("Left", 1, swing_left), ("Right", -1, swing_right)):
        pose[f"{side}UpperArm"] = rx(-swing) @ ry(sign * down)
        pose[f"{side}LowerArm"] = rz(-sign * elbow)


def leg(pose, side, theta, knee, foot=None):
    """Hip swung forward by theta, knee flexed by knee; the foot kept about flat unless given."""
    pose[f"{side}UpperLeg"] = rx(-theta)
    pose[f"{side}LowerLeg"] = rx(knee)
    pose[f"{side}Foot"] = rx(0.8 * (theta - knee) if foot is None else foot)


def locomotion(phase, amplitude, knee_stance, knee_swing, arm_swing, elbow, lean, bob):
    pose, s, c = {}, math.sin(TAU * phase), math.cos(TAU * phase)
    for side, sign in (("Left", 1), ("Right", -1)):
        theta = amplitude * s * sign  # the right leg is half a cycle behind the left
        swing = max(0.0, c * sign)    # the leg is moving forward
        knee = knee_stance + knee_swing * swing ** 1.5
        leg(pose, side, theta, knee, 0.8 * (theta - knee) + 0.3 * swing)
    arms_down(pose, math.radians(70), swing_left=-arm_swing * s, swing_right=arm_swing * s, elbow=elbow)
    pose["Hips"] = rz(-0.12 * s)
    pose["Spine"] = rx(lean)
    pose["Chest"] = rz(0.2 * s)
    pose["Head"] = rx(-lean * 0.7)
    lowest_at_mid_stance = -bob * math.cos(2 * TAU * phase)
    return pose, Vector((0, 0, lowest_at_mid_stance - 0.03))


def amplitude_for(speed, seconds):
    return speed * seconds / (LEG * TAU)


def idle(phase):
    pose, breath = {}, math.sin(TAU * phase)
    arms_down(pose, math.radians(80), 0.03 * breath, 0.03 * breath, elbow=0.2)
    for side in ("Left", "Right"):
        leg(pose, side, 0.03, 0.06)
    pose["Spine"] = rx(0.02 * breath)
    pose["Chest"] = rx(-0.025 * breath)
    pose["Head"] = rz(0.06 * math.sin(TAU * phase + 1.0)) @ rx(0.015 * breath)
    return pose, Vector((0, 0, -0.012 + 0.006 * breath))


def jump(phase):
    pose, w = {}, math.sin(TAU * phase)
    leg(pose, "Left", 0.85 + 0.05 * w, 1.5)
    leg(pose, "Right", 0.15 - 0.05 * w, 0.7)
    arms_down(pose, math.radians(40), 0.9, 0.6, elbow=0.7)
    pose["Spine"] = rx(0.12)
    pose["Head"] = rx(-0.15)
    return pose, Vector((0, 0, 0.0))


def fall(phase):
    pose, w = {}, math.sin(TAU * phase)
    leg(pose, "Left", 0.35 + 0.2 * w, 0.6 + 0.2 * w)
    leg(pose, "Right", -0.05 - 0.2 * w, 0.8 - 0.2 * w)
    arms_down(pose, math.radians(15), 0.3 * w, -0.3 * w, elbow=0.35)
    pose["Spine"] = rx(-0.06)
    pose["Head"] = rx(0.1)
    return pose, Vector((0, 0, 0.0))


RUN_SECONDS, SPRINT_SECONDS = 0.6, 0.4
CLIPS = [
    ("Idle", 3.0, idle),
    ("Run", RUN_SECONDS, lambda p: locomotion(p, amplitude_for(WALK_SPEED, RUN_SECONDS), 0.25, 1.4, 0.7, 1.3, 0.14, 0.045)),
    ("Sprint", SPRINT_SECONDS, lambda p: locomotion(p, amplitude_for(SPRINT_SPEED, SPRINT_SECONDS), 0.3, 1.7, 1.0, 1.5, 0.3, 0.06)),
    ("Jump", 1.0, jump),
    ("Fall", 1.0, fall),
]

# ---------------------------------------------------------------- clips
arm.animation_data_create()
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"

actions = []
for clip, seconds, sample in CLIPS:
    action = bpy.data.actions.new(clip)
    action.use_fake_user = True
    arm.animation_data.action = action
    frames = round(seconds * FPS)
    previous = {}
    for frame in range(frames + 1):
        pose, hips_offset = sample((frame % frames) / frames)
        for pb in arm.pose.bones:
            q = to_local(pb.name, pose.get(pb.name, Quaternion()))
            if pb.name in previous:
                q.make_compatible(previous[pb.name])
            previous[pb.name] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
        hips = arm.pose.bones["Hips"]
        hips.location = REST["Hips"].inverted() @ hips_offset
        hips.keyframe_insert("location", frame=frame, group="Hips")
    action.use_frame_range = True
    action.frame_start, action.frame_end = 0, frames
    actions.append((clip, action, frames))

# ---------------------------------------------------------------- previews: one frame of every clip
if PREVIEW_DIR:
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = 700
    scene.render.resolution_y = 900
    world = bpy.data.worlds.new("Sky")
    world.color = (0.62, 0.72, 0.78)
    scene.world = world
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    cam.location = (3.4, -4.2, 1.9)
    cam.rotation_euler = (Vector((0.0, 0.0, 1.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    for clip, action, frames in actions:
        arm.animation_data.action = action
        for label, frame in (("a", 0), ("b", frames // 4)):
            scene.frame_set(frame)
            scene.render.filepath = os.path.join(PREVIEW_DIR, f"crew_body_{clip.lower()}_{label}.png")
            bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print(f"[crew_body] previews in {PREVIEW_DIR}")

# ---------------------------------------------------------------- export: the rest pose is the bind pose
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.rotation_quaternion = Quaternion()
    pb.location = Vector()
scene.frame_set(0)

# Face Unity's +Z (see the header): the turn sits on the armature object, the root node, so bones and clips are untouched.
arm.rotation_euler = (0.0, 0.0, math.pi)

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
body.select_set(True)
bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"ARMATURE", "MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", add_leaf_bones=False, use_armature_deform_only=True,
                         armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_actions=True,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                         bake_anim_simplify_factor=0.0)
print(f"[crew_body] exported {FBX_PATH}: {', '.join(c for c, _, _ in actions)}")
