# Builds unity-assets/Models/Driver.fbx from two CC0 Quaternius packs:
#   Universal Base Characters (Superhero_Male_FullBody + Hair_Buzzed + Hair_Beard)
#   Universal Animation Library (Idle, Walk, Jog, Driving, Interact, Sitting)
# The base body is "dressed" by splitting its faces into clothing materials
# by dominant bone, and nudging clothing verts outward so seams read as fabric.
# Usage: Blender -b -P build_driver.py -- <ubc_dir> <ual_dir> [preview_dir]
import bpy, sys, os, mathutils

argv = sys.argv[sys.argv.index('--') + 1:]
UBC, UAL = argv[0], argv[1]
PREV = argv[2] if len(argv) > 2 else None
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Models', 'Driver.fbx')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(UBC, 'Base Characters/Unity/Superhero_Male_FullBody.fbx'))
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.name = 'DriverRig'
body = bpy.data.objects['SuperHero_Male']

# ---- animations from the UAL pack --------------------------------------
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=os.path.join(UAL, 'Unity/UAL1_Standard.fbx'))
for o in set(bpy.data.objects) - before:
    bpy.data.objects.remove(o, do_unlink=True)
keep = {'Idle_Loop': 'Idle', 'Walk_Loop': 'Walk', 'Jog_Fwd_Loop': 'Jog', 'Driving_Loop': 'Drive',
        'Interact': 'Interact', 'Sitting_Idle_Loop': 'Sit'}
for a in list(bpy.data.actions):
    short = a.name.split('|')[-1]
    if short in keep:
        a.name = keep[short]
        a.use_fake_user = True
    else:
        bpy.data.actions.remove(a)
print('ACTIONS', [a.name for a in bpy.data.actions])

# ---- hair and beard, parented to the head bone ---------------------------
head_bone = arm.data.bones['Head']
head_world = arm.matrix_world @ head_bone.head_local
for f in ('Hair_Buzzed.fbx', 'Hair_Beard.fbx'):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(UBC, 'Hairstyles/Origin at 0/FBX (Unity)', f))
    for o in set(bpy.data.objects) - before:
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True); continue
        bb = [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
        cz = sum(v.z for v in bb) / 8
        print('HAIR', o.name, 'centre z', round(cz, 3), 'head z', round(head_world.z, 3))
        mw = o.matrix_world.copy()
        o.parent = arm; o.parent_type = 'BONE'; o.parent_bone = 'Head'
        o.matrix_world = mw
        for m in o.data.materials:
            pass
        o.data.materials.clear()
        o.data.materials.append(bpy.data.materials.get('Driver_Hair') or bpy.data.materials.new('Driver_Hair'))

# ---- clothing split by dominant bone -------------------------------------
def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)

zones = {
    'Driver_Skin':  ('Head', 'neck_01', 'hand', 'index', 'middle', 'pinky', 'ring', 'thumb'),
    'Driver_Boots': ('foot', 'ball'),
    'Driver_Jeans': ('pelvis', 'thigh', 'calf'),
    'Driver_Vest':  ('spine_01', 'spine_02', 'spine_03'),
    'Driver_Shirt': ('clavicle', 'upperarm', 'lowerarm'),
}
order = ['Driver_Skin', 'Driver_Shirt', 'Driver_Vest', 'Driver_Jeans', 'Driver_Boots']
body.data.materials.clear()
for n in order:
    body.data.materials.append(mat(n))
gname = {g.index: g.name for g in body.vertex_groups}

def zone_of_vertex(v):
    best, bw = None, 0
    for g in v.groups:
        if g.weight > bw:
            best, bw = gname[g.group], g.weight
    if best is None:
        return 'Driver_Skin'
    for z, keys in zones.items():
        if any(best == k or best.startswith(k) for k in keys):
            return z
    return 'Driver_Skin'

me = body.data
vz = [zone_of_vertex(v) for v in me.vertices]
# Sleeves stop short of the wrist: lower half of the forearm stays skin.
for v in me.vertices:
    co = v.co
for i, v in enumerate(me.vertices):
    if vz[i] == 'Driver_Shirt':
        # forearm verts weighted mostly to lowerarm and far from the elbow -> skin
        w = {gname[g.group]: g.weight for g in v.groups}
        la = max(w.get('lowerarm_l', 0), w.get('lowerarm_r', 0))
        if la > 0.85:
            vz[i] = 'Driver_Skin'
    wz = (body.matrix_world @ v.co).z
    if vz[i] in ('Driver_Jeans', 'Driver_Skin') and wz < 0.17 and vz[i] != 'Driver_Skin' or (vz[i] == 'Driver_Jeans' and wz < 0.17):
        vz[i] = 'Driver_Boots'
    if vz[i] == 'Driver_Jeans' and wz > 1.02:
        vz[i] = 'Driver_Vest'
counts = {}
for p in me.polygons:
    zs = [vz[i] for i in p.vertices]
    z = max(set(zs), key=lambda k: (zs.count(k), -order.index(k)))
    p.material_index = order.index(z)
    counts[z] = counts.get(z, 0) + 1
print('ZONES', counts)

# push clothing out a little so it reads as fabric over the body
push = {'Driver_Shirt': 0.009, 'Driver_Vest': 0.017, 'Driver_Jeans': 0.013, 'Driver_Boots': 0.014}
for i, v in enumerate(me.vertices):
    d = push.get(vz[i])
    if d:
        v.co += v.normal * d

# preview colours only (Unity remaps by name)
cols = {'Driver_Skin': (0.80, 0.58, 0.45), 'Driver_Shirt': (0.12, 0.13, 0.15), 'Driver_Vest': (0.85, 0.95, 0.10),
        'Driver_Jeans': (0.10, 0.16, 0.28), 'Driver_Boots': (0.22, 0.14, 0.08), 'Driver_Hair': (0.09, 0.06, 0.04)}
for n, c in cols.items():
    m = mat(n); m.diffuse_color = (*c, 1)

# eyes / eyebrows keep their own materials; rename for the Unity remap
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name in ('Eyes', 'Eyebrows'):
        o.data.materials.clear()
        o.data.materials.append(mat('Driver_Eyes' if o.name == 'Eyes' else 'Driver_Hair'))

# report seated head height for the Unity seat offset
for an in ('Drive', 'Idle'):
    arm.animation_data_create(); arm.animation_data.action = bpy.data.actions[an]
    if hasattr(arm.animation_data, 'action_slot') and bpy.data.actions[an].slots:
        arm.animation_data.action_slot = bpy.data.actions[an].slots[0]
    bpy.context.scene.frame_set(1)
    pb = arm.pose.bones['Head']; pv = arm.pose.bones['pelvis']
    print('POSE', an, 'head', tuple(round(x, 3) for x in (arm.matrix_world @ pb.head)),
          'pelvis', tuple(round(x, 3) for x in (arm.matrix_world @ pv.head)),
          'hand_l', tuple(round(x, 3) for x in (arm.matrix_world @ arm.pose.bones['hand_l'].head)))

if PREV:
    os.makedirs(PREV, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.color_type = 'MATERIAL'
    sc.display.shading.light = 'STUDIO'
    sc.render.resolution_x, sc.render.resolution_y = 320, 400
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 50
    for an, fr in (('Idle', 1), ('Walk', 10), ('Drive', 1)):
        arm.animation_data.action = bpy.data.actions[an]
        if hasattr(arm.animation_data, 'action_slot') and bpy.data.actions[an].slots:
            arm.animation_data.action_slot = bpy.data.actions[an].slots[0]
        sc.frame_set(fr)
        for view, loc in (('front', (1.6, 4.2, 1.2)), ('side', (4.4, 0.4, 1.1))):
            cam.location = loc
            d = mathutils.Vector((0, 0, 0.9)) - cam.location
            cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = os.path.join(PREV, f'driver_{an}_{view}.png')
            bpy.ops.render.render(write_still=True)

arm.animation_data.action = None
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.type in ('ARMATURE', 'MESH'):
        o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, axis_forward='-Z', axis_up='Y',
                         global_scale=1.0, apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.5,
                         mesh_smooth_type='FACE', object_types={'ARMATURE', 'MESH'})
print('EXPORTED DRIVER', OUT)
