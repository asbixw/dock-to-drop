# Converts "FREIGHTLINER CASCADIA 2020" by liebherr996litronic (Sketchfab, CC-BY-4.0)
# into the game's truck FBX. Usage:
#   Blender -b -P build_truck_sketchfab.py -- <dir with scene.gltf> [dy]
# The original red paint (AZUL) is kept as Cab_Red. Game conventions kept: front toward +Y (Unity +Z after the 180 deg visual yaw),
# driver side -X, a separate DriverDoor with its pivot on the front hinge, the
# windshield named WindshieldGlass, a flipped Cab_Interior shell so the inside of
# the cab renders dark, and material names ApplyTruckMaterials understands.
import bpy, bmesh, sys, os, shutil, mathutils
from mathutils import Vector, Matrix

A = sys.argv[sys.argv.index('--') + 1:]
SRC = A[0]
DY = float(A[1]) if len(A) > 1 else 2.40
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'Models', 'Freightliner_Cascadia.fbx')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, 'scene.gltf'))
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for o in bpy.data.objects:
    o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = meshes[0]
# bake hierarchy transforms into the meshes, then drop the empties
for o in meshes:
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
bpy.ops.object.make_single_user(object=True, obdata=True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.data.objects):
    if o.type != 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
# face the game's way: rotate 180 about Z, slide so the door lines up with the seat
M = Matrix.Translation((0, DY, 0)) @ Matrix.Rotation(3.14159265, 4, 'Z')
for o in meshes:
    o.data.transform(M)

MAP = {'AZUL': 'Cab_Red', 'cromiun': 'Chrome', 'CROMO_W': 'Chrome', 'DARK': 'DarkTrim',
       'DD_OSCURO': 'DarkTrim', 'material_0': 'DarkTrim', 'plastick_gray': 'GreyPlastic',
       'glass1': 'Glass', 'Material_139': 'Amber', 'COLOR_YELLOW_SS': 'Aluminum',
       'AMARILLO_CAT': 'DarkTrim', 'radiator_cscd': 'Grille_Tex', 'Material_41': 'Rim_Tex',
       'Standardmaterial': 'Tire_Tex'}
TEXOUT = {'Grille_Tex': 'cascadia_grille', 'Rim_Tex': 'cascadia_rim', 'Tire_Tex': 'cascadia_tire'}
newmats = {}
for m in list(bpy.data.materials):
    tgt = MAP.get(m.name)
    if not tgt:
        continue
    if tgt in TEXOUT:
        img = next((n.image for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image), None)
        if img:
            dst = os.path.join(HERE, 'Textures', TEXOUT[tgt] + '.png')
            img.filepath_raw = dst; img.file_format = 'PNG'; img.save()
    if tgt not in newmats:
        newmats[tgt] = bpy.data.materials.new(tgt)
for o in meshes:
    for i, m in enumerate(o.data.materials):
        if m and m.name in MAP:
            o.data.materials[i] = newmats[MAP[m.name]]


def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)


def split_faces(obj, pred, name):
    """Move faces matching pred(face) into a new object; returns it or None."""
    bm = bmesh.new(); bm.from_mesh(obj.data)
    sel = [f for f in bm.faces if pred(f)]
    if not sel:
        bm.free(); return None
    new = obj.copy(); new.data = obj.data.copy(); new.name = name
    bpy.context.collection.objects.link(new)
    keep = set(f.index for f in sel)
    bm2 = bmesh.new(); bm2.from_mesh(new.data)
    bmesh.ops.delete(bm2, geom=[f for f in bm2.faces if f.index not in keep], context='FACES')
    bm2.to_mesh(new.data); bm2.free()
    bmesh.ops.delete(bm, geom=sel, context='FACES')
    bm.to_mesh(obj.data); bm.free()
    return new


# ---- windshield: forward-facing glass above the hood line
glass = [o for o in meshes if o.data.materials and o.data.materials[0].name == 'Glass']
ws_parts = []
for g in glass:
    p = split_faces(g, lambda f: f.normal.y > 0.45 and f.calc_center_median().z > 2.0, 'WindshieldGlass')
    if p:
        ws_parts.append(p)

# ---- interior shell: flipped copy of the painted cab, pushed 12 mm inward
shells = []
for o in meshes:
    if o.data.materials and o.data.materials[0].name == 'Cab_Red':
        s = o.copy(); s.data = o.data.copy(); s.name = 'CabInteriorShell'
        bpy.context.collection.objects.link(s)
        bm = bmesh.new(); bm.from_mesh(s.data)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z < 1.05], context='FACES')
        for v in bm.verts:
            v.co -= v.normal * 0.012
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        bm.to_mesh(s.data); bm.free()
        s.data.materials.clear(); s.data.materials.append(mat('Cab_Interior'))
        shells.append(s)

# ---- driver door (-X side). Region measured from a side render of the source.
D0, D1, Z0, Z1 = DY - 2.15, DY - 0.95, 1.22, 2.98
def in_door(f):
    c = f.calc_center_median()
    return c.x < -0.80 and D0 < c.y < D1 and Z0 < c.z < Z1
door_parts = []
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    p = split_faces(o, in_door, 'DoorPart')
    if p:
        door_parts.append(p)
bpy.ops.object.select_all(action='DESELECT')
for p in door_parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = door_parts[0]
bpy.ops.object.join()
door = bpy.context.view_layer.objects.active; door.name = 'DriverDoor'
hinge = Vector((-1.28, D1, (Z0 + Z1) / 2))
door.data.transform(Matrix.Translation(-hinge)); door.location = hinge

# ---- decimate the heavy parts (never the door, glass or windshield)
def tris(o):
    o.data.calc_loop_triangles(); return len(o.data.loop_triangles)
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    if o is door or o.name.startswith('Windshield') or (o.data.materials and o.data.materials[0].name == 'Glass'):
        continue
    n = tris(o)
    if n > 4000:
        m = o.modifiers.new('d', 'DECIMATE'); m.ratio = 0.08 if o.name.startswith('CabInteriorShell') else (0.35 if n < 40000 else 0.24)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=m.name)

# ---- tires and rims: the source is double-sided with mixed normals, and
# Unity culls back faces, so give these parts a flipped second skin.
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    names = [m.name for m in o.data.materials if m]
    if not any(n in ('Tire_Tex', 'Rim_Tex') for n in names):
        continue
    bm = bmesh.new(); bm.from_mesh(o.data)
    src = [f for f in bm.faces if o.data.materials[f.material_index].name in ('Tire_Tex', 'Rim_Tex')]
    dup = bmesh.ops.duplicate(bm, geom=src)
    bmesh.ops.reverse_faces(bm, faces=[g for g in dup['geom'] if isinstance(g, bmesh.types.BMFace)])
    bm.to_mesh(o.data); bm.free()

# ---- wheels: split tire and rim faces into one object per wheel position
# (named Wheel_<side>_<axle>, origin at the hub) so Unity can spin and steer them.
WHEEL_MATS = ('Tire_Tex', 'Rim_Tex')
parts = []
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    names = [m.name if m else '' for m in o.data.materials]
    if not any(n in WHEEL_MATS for n in names):
        continue
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    only_wheel = True
    for f in o.data.polygons:
        f.select = names[f.material_index] in WHEEL_MATS
        only_wheel = only_wheel and f.select
    before = set(bpy.data.objects)
    if not only_wheel:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
        new = [x for x in bpy.data.objects if x not in before]
    else:
        new = [o]
    for w in new:
        bpy.ops.object.select_all(action='DESELECT'); w.select_set(True); bpy.context.view_layer.objects.active = w
        b2 = set(bpy.data.objects)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.separate(type='LOOSE'); bpy.ops.object.mode_set(mode='OBJECT')
        parts += [w] + [x for x in bpy.data.objects if x not in b2]
def centre(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return Vector([(min(p[i] for p in pts) + max(p[i] for p in pts)) / 2 for i in range(3)])
# axle positions: 1-D clusters along the truck's long axis (Blender Y here)
cs = sorted(set(round(centre(o).y, 1) for o in parts))
axles = []
for y in cs:
    if axles and y - axles[-1][-1] < 0.7: axles[-1].append(y)
    else: axles.append([y])
axle_y = [sum(a) / len(a) for a in axles]
groups = {}
for o in parts:
    c = centre(o)
    ai = min(range(len(axle_y)), key=lambda i: abs(axle_y[i] - c.y))
    side = 'L' if c.x > 0 else 'R'
    groups.setdefault((side, ai), []).append(o)
for (side, ai), os_ in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in os_: o.select_set(True)
    bpy.context.view_layer.objects.active = os_[0]
    if len(os_) > 1: bpy.ops.object.join()
    w = bpy.context.view_layer.objects.active
    w.name = 'Wheel_%s_%d' % (side, ai)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    print('WHEEL', w.name, [round(v, 2) for v in w.location], 'dims', [round(v, 2) for v in w.dimensions])

objs = [o for o in bpy.data.objects if o.type == 'MESH']
def bb(os_):
    pts = [o.matrix_world @ Vector(c) for o in os_ for c in o.bound_box]
    return [round(min(p[i] for p in pts), 2) for i in range(3)], [round(max(p[i] for p in pts), 2) for i in range(3)]
print('TRUCK all', bb(objs), 'tris', sum(tris(o) for o in objs))
print('TRUCK door', bb([door]), 'windshield', bb(ws_parts) if ws_parts else None, 'shells', len(shells))
bpy.ops.object.select_all(action='DESELECT')
for o in objs:
    o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, axis_forward='-Z', axis_up='Y',
                         global_scale=1.0, apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='FACE',
                         path_mode='STRIP', embed_textures=False)
print('EXPORTED', OUT)
