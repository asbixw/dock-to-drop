# Converts downloaded Poly Haven glTF models (CC0) into game-ready FBX props.
#   Blender -b -P build_props.py -- <polyhaven_download_dir> [only_name ...]
# Output: Models/Props/<Name>.fbx and Textures/Props/<material>_{diff,nor,ms}.*
# Foliage is thinned (random leaf islands removed, the rest enlarged so the
# canopy keeps its coverage) and everything is decimated to a triangle budget.
import bpy, bmesh, sys, os, glob, random, mathutils
import numpy as np

argv = sys.argv[sys.argv.index('--') + 1:]
SRC = argv[0]
ONLY = set(argv[1:])
HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.join(HERE, 'Models', 'Props'); TEX = os.path.join(HERE, 'Textures', 'Props')
os.makedirs(MOD, exist_ok=True); os.makedirs(TEX, exist_ok=True)

# name, asset, object filter, {material-substring: (keep_fraction, island_scale)}, tri budget
SPECS = [
    ('Tree_Oak_A', 'island_tree_01', None, {'leaves': (0.085, 3.4, 0.3)}, 62000),
    ('Tree_Oak_B', 'island_tree_02', None, {'leaves': (0.10, 3.3, 0.3)}, 56000),
    ('Tree_Small', 'tree_small_02', None, {'leaves': (0.04, 4.0, 0.3)}, 56000),
    ('Tree_Oak_A_LOD1', 'island_tree_01', None, {'leaves': (0.014, 7.0, 0.2)}, 6500),
    ('Tree_Oak_B_LOD1', 'island_tree_02', None, {'leaves': (0.018, 6.6, 0.2)}, 6000),
    ('Tree_Small_LOD1', 'tree_small_02', None, {'leaves': (0.007, 8.5, 0.2)}, 6000),
    ('Shrub_A', 'shrub_02', ['shrub_02_a'], {}, 5000),
    ('Shrub_B', 'shrub_02', ['shrub_02_c'], {}, 5000),
    ('Bush_A', 'searsia_lucida', ['searsia_lucida_a_LOD0'], {}, 12000),
    ('Bush_B', 'searsia_lucida', ['searsia_lucida_b_LOD0'], {}, 10000),
    ('PowerPole', 'modular_electricity_poles', 'preset_01', {}, 12000),
    ('JerseyBarrier', 'concrete_road_barrier', None, {}, 2500),
    ('Hydrant', 'fire_hydrant', ['fire_hydrant', 'fire_hydrant_cap_01', 'fire_hydrant_cap_02', 'fire_hydrant_cap_03', 'fire_hydrant_chain'], {}, 3000),
    ('TrashCan', 'metal_trash_can', ['metal_trash_can', 'metal_trash_can_handle_left', 'metal_trash_can_handle_right'], {}, 4000),
    ('UtilityBox', 'utility_box_01', None, {}, 4400),
    ('Tyre', 'old_tyre', None, {}, 2880),
    ('CardboardBox', 'cardboard_box_01', None, {}, 1200),
    ('CoveredCar', 'covered_car', None, {}, 12600),
    ('StreetLamp', 'street_lamp_01', None, {}, 6000),
    ('StreetLampB', 'street_lamp_02', None, {}, 6000),
    ('Barrel', 'barrel_03', None, {}, 2500),
    ('BarrierB', 'concrete_road_barrier_02', None, {}, 2500),
    ('SecurityLight', 'security_light', None, {}, 2000),
]


def tri_count(objs):
    n = 0
    for o in objs:
        o.data.calc_loop_triangles(); n += len(o.data.loop_triangles)
    return n


def thin_islands(obj, keep, scale, seed=7):
    me = obj.data
    nv = len(me.vertices)
    parent = np.arange(nv)

    def find(a):
        root = a
        while parent[root] != root:
            root = parent[root]
        while parent[a] != root:
            parent[a], a = root, parent[a]
        return root
    for p in me.polygons:
        vs = p.vertices
        r0 = find(vs[0])
        for v in vs[1:]:
            r = find(v)
            if r != r0:
                parent[r] = r0
    roots = np.array([find(i) for i in range(nv)])
    uniq, island = np.unique(roots, return_inverse=True)
    rng = np.random.default_rng(seed)
    keep_island = rng.random(len(uniq)) < keep
    co = np.zeros(nv * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    cnt = np.bincount(island, minlength=len(uniq)).astype(float)
    cen = np.stack([np.bincount(island, weights=co[:, k], minlength=len(uniq)) for k in range(3)], 1) / cnt[:, None]
    jitter = 1.0 + (rng.random(len(uniq)) - 0.5) * 0.35
    s = (scale * jitter)[island][:, None]
    co = cen[island] + (co - cen[island]) * s
    me.vertices.foreach_set('co', co.ravel())
    bm = bmesh.new(); bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    dead = [v for v in bm.verts if not keep_island[island[v.index]]]
    bmesh.ops.delete(bm, geom=dead, context='VERTS')
    bm.to_mesh(me); bm.free()
    print('  thinned', obj.name, 'islands', len(uniq), 'kept', int(keep_island.sum()))


def decimate(obj, target):
    n = tri_count([obj])
    if n <= target or n == 0:
        return
    m = obj.modifiers.new('dec', 'DECIMATE'); m.ratio = max(0.01, target / n)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=m.name)


def export_textures(mat):
    imgs = {}
    for n in mat.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image:
            f = os.path.basename(n.image.filepath).lower()
            for k in ('diff', 'nor_gl', 'arm'):
                if '_' + k in f:
                    imgs[k] = n.image
    base = os.path.join(TEX, mat.name)
    if 'diff' in imgs:
        img = imgs['diff']; img.filepath_raw = base + '_diff.jpg'; img.file_format = 'JPEG'; img.save()
    if 'nor_gl' in imgs:
        img = imgs['nor_gl']; img.filepath_raw = base + '_nor.jpg'; img.file_format = 'JPEG'; img.save()
    if 'arm' in imgs:
        a = imgs['arm']; w, h = a.size
        px = np.zeros(w * h * 4, dtype=np.float32); a.pixels.foreach_get(px); px = px.reshape(-1, 4)
        out = np.zeros_like(px)
        out[:, 0] = px[:, 2]; out[:, 1] = px[:, 0]; out[:, 2] = 0; out[:, 3] = 1.0 - px[:, 1]  # metal, ao, -, smooth
        ms = bpy.data.images.new(mat.name + '_ms', w, h, alpha=True)
        ms.colorspace_settings.name = 'Non-Color'
        ms.pixels.foreach_set(out.ravel()); ms.filepath_raw = base + '_ms.png'; ms.file_format = 'PNG'; ms.save()


for name, asset, filt, thin, budget in SPECS:
    if ONLY and name not in ONLY:
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glob.glob(os.path.join(SRC, asset, '*.gltf'))[0])
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if isinstance(filt, list):
        sel = [o for o in meshes if o.name in filt]
    elif isinstance(filt, str):
        sel = [o for o in meshes if o.name.startswith(filt)]
    else:
        sel = meshes
    for o in meshes:
        if o not in sel:
            bpy.data.objects.remove(o, do_unlink=True)
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    for o in sel:
        o.parent = None
    bpy.ops.object.select_all(action='DESELECT')
    for o in sel:
        o.select_set(True)
    bpy.context.view_layer.objects.active = sel[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if len(sel) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    # material renames before splitting so file names are stable
    for m in obj.data.materials:
        if not m.name.startswith('PH_'):
            m.name = 'PH_' + m.name.replace('.', '_')
    before = tri_count([obj])
    # split by material, treat each part with its own budget share
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    parts = [o for o in bpy.data.objects if o.type == 'MESH']
    leafy = []
    for p in parts:
        mn = p.data.materials[0].name if p.data.materials else ''
        for key, (keep, sc, ratio) in thin.items():
            if key in mn:
                thin_islands(p, keep, sc)
                decimate(p, int(tri_count([p]) * ratio))
                leafy.append(p)
    rest = [p for p in parts if p not in leafy]
    left = max(2000, budget - tri_count(leafy))
    total = tri_count(rest)
    for p in rest:
        n = tri_count([p])
        decimate(p, max(300, int(left * n / max(1, total))))
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active; obj.name = name
    # base on the ground, centred
    pts = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
    c = mathutils.Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2,
                          (min(p.y for p in pts) + max(p.y for p in pts)) / 2, min(p.z for p in pts)))
    obj.data.transform(mathutils.Matrix.Translation(-c)); obj.location = (0, 0, 0)
    for m in obj.data.materials:
        export_textures(m)
    dims = tuple(round(x, 2) for x in obj.dimensions)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(MOD, name + '.fbx'), use_selection=True, axis_forward='-Z',
                             axis_up='Y', global_scale=1.0, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
    print('PROP', name, 'tris', before, '->', tri_count([obj]), 'size', dims, 'mats', [m.name for m in obj.data.materials])
