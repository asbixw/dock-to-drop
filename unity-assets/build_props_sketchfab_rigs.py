# Converts four Sketchfab glTF downloads (CC-BY-4.0, see THIRD_PARTY_NOTICES.md) into
# game FBX props: two parked tractors, the diesel canopy and the lot-light pole.
#   Blender -b -P build_props_sketchfab_rigs.py -- <download_dir> [--preview <png_dir>] [only_name ...]
# <download_dir>/<folder>/scene.gltf is one unzipped Sketchfab "glTF" download:
#   tractor_t680_scan  "18 Wheeler Truck /Tractor Trailer Photogrammetry" (Air Digital)
#   tractor_logistics  "Logistics Semi-Truck - 3D Model" (Nez1x75)
#   gas_station_props  "Gas station Props" (Elbolillo)
#   lot_light          "Walmart Parking Lot Light w/ Gardco GL18 LED" (streetlightshps)
# Output: Models/Props/<Name>.fbx and Textures/Props/SR_*_{diff,nor,ms}.* in the format
# PropLibrary.Remap reads (diff = sRGB colour, nor = OpenGL normal, ms = R metal, G AO, A smooth).
# Vehicles face Blender -Y (Unity +Z), are centred and sit on z = 0.
import bpy, bmesh, sys, os, math, mathutils
import numpy as np

argv = sys.argv[sys.argv.index('--') + 1:]
SRC = argv.pop(0)
PREVIEW = None
if '--preview' in argv:
    i = argv.index('--preview'); PREVIEW = argv[i + 1]; del argv[i:i + 2]
ONLY = set(argv)
HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.join(HERE, 'Models', 'Props'); TEX = os.path.join(HERE, 'Textures', 'Props')
os.makedirs(MOD, exist_ok=True); os.makedirs(TEX, exist_ok=True)


# ---------------------------------------------------------------- helpers
def load(folder):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, folder, 'scene.gltf'))
    ms = [o for o in bpy.data.objects if o.type == 'MESH']
    for o in ms:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    return ms


def join(objs, name):
    for o in bpy.data.objects:
        if o not in objs:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.make_single_user(object=True, obdata=True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    return o


def tris(o):
    o.data.calc_loop_triangles(); return len(o.data.loop_triangles)


def decimate(o, target):
    n = tris(o)
    if n <= target:
        return
    m = o.modifiers.new('dec', 'DECIMATE'); m.ratio = max(0.005, target / n)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)


def verts(o):
    co = np.empty(len(o.data.vertices) * 3); o.data.vertices.foreach_get('co', co)
    return co.reshape(-1, 3)


def poly_verts(me):
    ls = np.empty(len(me.polygons), dtype=np.int64); me.polygons.foreach_get('loop_start', ls)
    vi = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get('vertex_index', vi)
    return ls, vi


def delete_faces(o, mask):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in np.nonzero(mask)[0]], context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(o.data); bm.free(); o.data.update()


def islands(o):
    me = o.data; nv = len(me.vertices)
    parent = np.arange(nv)

    def find(a):
        r = a
        while parent[r] != r:
            r = parent[r]
        while parent[a] != r:
            parent[a], a = r, parent[a]
        return r
    for p in me.polygons:
        vs = p.vertices; r0 = find(vs[0])
        for v in vs[1:]:
            r = find(v)
            if r != r0:
                parent[r] = r0
    roots = np.array([find(i) for i in range(nv)])
    _, isl = np.unique(roots, return_inverse=True)
    return isl


def place(o, centre_xy=True, pivot=None):
    co = verts(o)
    mn, mx = co.min(0), co.max(0)
    if pivot is not None:
        c = mathutils.Vector((pivot[0], pivot[1], mn[2]))
    elif centre_xy:
        c = mathutils.Vector(((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]))
    else:
        c = mathutils.Vector((0, 0, mn[2]))
    o.data.transform(mathutils.Matrix.Translation(-c)); o.location = (0, 0, 0)


def scale_to(o, s):
    if isinstance(s, (int, float)):
        s = (s, s, s)
    o.data.transform(mathutils.Matrix.Diagonal((s[0], s[1], s[2], 1.0)))


def img_upstream(sock, depth=0):
    if sock is None or not sock.is_linked or depth > 6:
        return None
    n = sock.links[0].from_node
    if n.type == 'TEX_IMAGE':
        return n.image
    for i in n.inputs:
        r = img_upstream(i, depth + 1)
        if r:
            return r
    return None


def lin2srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def scaled_copy(img, size):
    im = img.copy()
    w, h = im.size
    s = size / max(w, h)
    if s < 1:
        im.scale(max(4, int(w * s)), max(4, int(h * s)))
    return im


def solid(name, rgb, path):
    im = bpy.data.images.new(name, 4, 4)
    im.pixels.foreach_set(np.tile(np.array([lin2srgb(rgb[0]), lin2srgb(rgb[1]), lin2srgb(rgb[2]), 1.0], np.float32), 16))
    im.filepath_raw = path; im.file_format = 'JPEG'; im.save()


def export_material(mat, size, tint=None):
    """Writes <mat>_diff/_nor/_ms for one glTF Principled material."""
    b = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat.node_tree else None
    base = os.path.join(TEX, mat.name)
    if b is None:  # unlit (KHR_materials_unlit) photoscan: its one image is the colour
        imgs = [n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image] if mat.node_tree else []
        if imgs:
            im = scaled_copy(max(imgs, key=lambda i: i.size[0]), size)
            im.filepath_raw = base + '_diff.jpg'; im.file_format = 'JPEG'; im.save()
        else:
            solid(mat.name + '_d', (0.5, 0.5, 0.5), base + '_diff.jpg')
        return
    col = img_upstream(b.inputs['Base Color'])
    if tint is not None:
        solid(mat.name + '_d', tint, base + '_diff.jpg')
    elif col is not None:
        im = scaled_copy(col, size); im.filepath_raw = base + '_diff.jpg'; im.file_format = 'JPEG'; im.save()
    else:
        solid(mat.name + '_d', b.inputs['Base Color'].default_value[:3], base + '_diff.jpg')
    nrm_node = b.inputs['Normal'].links[0].from_node if b.inputs['Normal'].is_linked else None
    nor = img_upstream(nrm_node.inputs['Color']) if nrm_node is not None and 'Color' in nrm_node.inputs else None
    if nor is not None:
        im = scaled_copy(nor, size); im.filepath_raw = base + '_nor.jpg'; im.file_format = 'JPEG'; im.save()
    mr = img_upstream(b.inputs['Metallic']) or img_upstream(b.inputs['Roughness'])
    metal_f = b.inputs['Metallic'].default_value; rough_f = b.inputs['Roughness'].default_value
    if mr is not None:
        im = scaled_copy(mr, size); w, h = im.size
        px = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(px); px = px.reshape(-1, 4)
        metal, rough = px[:, 2], px[:, 1]
    else:
        w = h = 4; metal = np.full(16, metal_f, np.float32); rough = np.full(16, rough_f, np.float32)
    out = np.stack([metal, np.ones_like(metal), np.zeros_like(metal), 1.0 - rough], 1).astype(np.float32)
    ms = bpy.data.images.new(mat.name + '_ms', w, h, alpha=True)
    ms.colorspace_settings.name = 'Non-Color'
    ms.pixels.foreach_set(out.ravel()); ms.filepath_raw = base + '_ms.png'; ms.file_format = 'PNG'; ms.save()


def finish(o, name, prefix, size, tints=None):
    for m in o.data.materials:
        if m and not m.name.startswith('SR_'):
            m.name = prefix + m.name.replace('.', '_').replace(' ', '_')
    for m in o.data.materials:
        if m:
            key = m.name[len(prefix):]
            export_material(m, size, (tints or {}).get(key))
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(MOD, name + '.fbx'), use_selection=True, axis_forward='-Z',
                             axis_up='Y', global_scale=1.0, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
    print('RIGPROP', name, 'tris', tris(o), 'size', tuple(round(x, 2) for x in o.dimensions),
          'mats', [m.name for m in o.data.materials if m])
    if PREVIEW:
        preview(o, name)


def preview(o, name):
    os.makedirs(PREVIEW, exist_ok=True)
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 400, 250
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    d = o.dimensions; c = mathutils.Vector((0, 0, d.z / 2)); r = d.length / 2
    v = mathutils.Vector((1.0, -1.0, 0.55)).normalized()
    cam.location = c + v * r * 2.4; cam.rotation_euler = (-v).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(PREVIEW, name + '.png'); bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------- models
def rig_t680():
    """Photoscan of a parked Kenworth T680 + 53 ft van: drop the scanned ground,
    level and straighten it, scale to a 13'6" trailer, decimate."""
    o = join(load('tractor_t680_scan'), 'Rig_T680')
    print('  scan tris', tris(o))
    decimate(o, 420000)
    co = verts(o)
    z0 = np.percentile(co[:, 2], 0.5)
    band = co[co[:, 2] < z0 + 0.8]
    for _ in range(6):
        A = np.c_[band[:, 0], band[:, 1], np.ones(len(band))]
        sol = np.linalg.lstsq(A, band[:, 2], rcond=None)[0]
        r = band[:, 2] - A @ sol
        band = band[np.abs(r) < max(0.04, 2.0 * r.std())]
    a, b, c = sol
    nz = mathutils.Vector((-a, -b, 1.0)).normalized()
    M = nz.rotation_difference(mathutils.Vector((0, 0, 1))).to_matrix().to_4x4() @ mathutils.Matrix.Translation((0, 0, -c))
    o.data.transform(M)
    co = verts(o); body = co[co[:, 2] > 0.6]
    m = body[:, :2].mean(0); w, v = np.linalg.eigh(np.cov((body[:, :2] - m).T)); ax = v[:, np.argmax(w)]
    ang = math.atan2(ax[0], ax[1])
    o.data.transform(mathutils.Matrix.Rotation(ang, 4, 'Z'))
    co = verts(o); body = co[co[:, 2] > 0.6]
    y0, y1 = body[:, 1].min(), body[:, 1].max()
    hf = body[body[:, 1] < y0 + 1.5][:, 2].max(); hr = body[body[:, 1] > y1 - 1.5][:, 2].max()
    if hf > hr:  # the low hood end is the front; put it at -Y
        o.data.transform(mathutils.Matrix.Rotation(math.pi, 4, 'Z'))
    co = verts(o); body = co[co[:, 2] > 0.6]
    xc = (np.percentile(body[:, 0], 1) + np.percentile(body[:, 0], 99)) / 2
    o.data.transform(mathutils.Matrix.Translation((-xc, 0, 0)))
    s = 4.11 / np.percentile(co[:, 2], 99.95)
    scale_to(o, s)
    print('  ground fit slope', round(a, 4), round(b, 4), 'scale', round(s, 3))
    co = verts(o); ls, vi = poly_verts(o.data)
    h, ax_ = co[vi, 2], np.abs(co[vi, 0])
    hmax = np.maximum.reduceat(h, ls); xmin = np.minimum.reduceat(ax_, ls)
    ground = (hmax < 0.10) | ((xmin > 1.45) & (hmax < 1.3)) | (np.minimum.reduceat(h, ls) < -0.3)
    print('  ground faces', int(ground.sum()), 'of', len(ground))
    delete_faces(o, ground)
    decimate(o, 150000)
    place(o)
    finish(o, 'Rig_T680', 'SR_T680_', 4096)


def rig_logistics():
    """Game-ready silver T680-style tractor; its trailer (Eagle Trans graphic) is cut off."""
    o = join(load('tractor_logistics'), 'Rig_Logistics')
    isl = islands(o); co = verts(o); k = isl.max() + 1
    ymin = np.full(k, 1e9); ymax = np.full(k, -1e9); zmin = np.full(k, 1e9)
    np.minimum.at(ymin, isl, co[:, 1]); np.maximum.at(ymax, isl, co[:, 1]); np.minimum.at(zmin, isl, co[:, 2])
    cy = np.bincount(isl, co[:, 1], k) / np.bincount(isl, minlength=k)
    box = int(np.argmax(ymax - ymin)); tf, floor = ymin[box], zmin[box]
    trailer = (ymin >= tf - 0.05) & ((zmin >= floor - 0.15) | (cy > tf + 2.5))
    trailer[box] = True
    ls, vi = poly_verts(o.data)
    pisl = isl[vi[ls]]
    print('  islands', k, 'trailer front', round(tf, 2), 'floor', round(floor, 2), 'trailer islands', int(trailer.sum()))
    delete_faces(o, trailer[pisl])
    co = verts(o)
    if co[:, 1].mean() > (co[:, 1].min() + co[:, 1].max()) / 2 + 2:  # cab must be the -Y end
        o.data.transform(mathutils.Matrix.Rotation(math.pi, 4, 'Z'))
    place(o)
    scale_to(o, 3.95 / o.dimensions.z)
    place(o)
    finish(o, 'Rig_Logistics', 'SR_Log_', 2048)


def canopy():
    """Roof, columns and lamps of the kit's fuel canopy, fitted to the game's 21 x 20 m slab at 8.1 m."""
    ms = load('gas_station_props')
    keep = [o for o in ms if o.name.startswith(('The_ceiling', 'Pillar'))]
    for o in ms:
        if o.name.startswith('Lamp'):
            z = max((o.matrix_world @ mathutils.Vector(c)).z for c in o.bound_box)
            if z > 6.0:
                keep.append(o)
    o = join(keep, 'Canopy')
    place(o)
    d = o.dimensions
    scale_to(o, (21.0 / d.x, 20.0 / d.y, 8.1 / d.z))
    place(o)
    finish(o, 'Canopy', 'SR_GS_', 1024)


def lot_light():
    """Gardco GL18 LED shoebox pole, 9 m tall, origin at the foot of the pole."""
    o = join(load('lot_light'), 'LotLight')
    co = verts(o); zr = co[:, 2].max() - co[:, 2].min()
    foot = co[co[:, 2] < co[:, 2].min() + 0.03 * zr]
    pv = ((foot[:, 0].min() + foot[:, 0].max()) / 2, (foot[:, 1].min() + foot[:, 1].max()) / 2)
    place(o, pivot=pv)
    scale_to(o, 9.0 / o.dimensions.z)
    # the orange lens reads as sodium; GL18s are white LED
    finish(o, 'LotLight', 'SR_Light_', 1024, tints={'Material_2': (0.92, 0.88, 0.78)})


JOBS = [('Rig_T680', rig_t680), ('Rig_Logistics', rig_logistics), ('Canopy', canopy), ('LotLight', lot_light)]
for name, fn in JOBS:
    if not ONLY or name in ONLY:
        fn()
