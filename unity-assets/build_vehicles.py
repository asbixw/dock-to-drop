# Converts downloaded Sketchfab glTF models (CC-BY, see THIRD_PARTY_NOTICES.md)
# into game-ready FBX props: parked cars, a 53 ft dry van and a fuel dispenser.
#   Blender -b -P build_vehicles.py -- <download_dir> [--preview <png_dir>] [only_name ...]
# <download_dir>/<asset>/scene.gltf is one unzipped Sketchfab "glTF" download.
# Output: Models/Props/<Name>.fbx and Textures/Props/<material>_{diff,nor,ms}.* plus
# <material>_pbr.txt ("r g b a metallic smoothness kind"), which PropLibrary reads
# for materials that are plain colors, glass or tintable paint.
# The model is scaled to its real length, turned so its front faces Blender -Y
# (Unity +Z), centred, put on the ground and decimated to a triangle budget.
import bpy, sys, os, glob, math, colorsys, mathutils
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
MAXTEX = 1024

# name, asset folder, real length in m (None keeps the size), extra yaw in degrees
# so the front ends up at -Y, triangle budget, paint material (tinted in game),
# material overrides {substring: (r, g, b, metallic, smoothness, kind)}, drop list (material name
# substrings, or 'obj:<substring>' for parts), options {'height': m, 'stretch': (lo, hi), 'width': m}: scale to a real
# height, then lengthen only the flat middle (lo..hi of the length from the front) to the real length
SPECS = [
    ('Car_Camry', 'toyota_camry_2020', 4.88, 0, 32000, 'Paint_Color', {}, []),
    ('Car_Tahoe', 'chevrolet_tahoe_2007', 5.13, 180, 30000, 'Paint',
     {'Chrome_2': (0.05, 0.05, 0.05, 0.0, 0.2, 'opaque'), 'Chrome': (0.75, 0.75, 0.75, 1.0, 0.85, 'opaque')}, []),
    ('Car_Fusion', 'ford_fusion_sport', 4.87, 0, 30000, 'material_0', {}, []),
    ('Car_Silverado', '2018_chevrolet_silverado_centennial_edition', 5.89, 0, 32000, None, {},
     ['obj:lights_position_front_and_back', 'obj:detach_wing']),
    ('Trailer53', 'truck_trailer_free', 16.15, 0, 36000, None,
     {'Trailer_White': (0.86, 0.87, 0.86, 0.15, 0.55, 'opaque'),
      'TIRE': (0.8, 0.8, 0.8, 0.0, 0.12, 'opaque'),
      'BLACK': (0.03, 0.03, 0.03, 0.2, 0.35, 'opaque'),
      'Black_material': (0.03, 0.03, 0.03, 0.2, 0.35, 'opaque'),
      'INTERIOR': (0.05, 0.05, 0.05, 0.3, 0.3, 'opaque'),
      'Metal': (0.25, 0.25, 0.26, 0.85, 0.45, 'opaque'),
      'Wheel': (0.55, 0.56, 0.58, 0.9, 0.6, 'opaque'),
      'RED_GLASS': (0.6, 0.02, 0.01, 0.0, 0.85, 'lamp')}, [], {'height': 4.1, 'stretch': (0.33, 0.74), 'width': 2.59}),
    ('FuelPump', 'gas_pump', None, 0, 9000, None, {}, []),
]


def tris(o):
    o.data.calc_loop_triangles(); return len(o.data.loop_triangles)


def upstream_image(sock, depth=0):
    """First image texture feeding a socket, walking back through any nodes."""
    if sock is None or not sock.is_linked or depth > 6:
        return None, None
    link = sock.links[0]; n = link.from_node
    if n.type == 'TEX_IMAGE':
        return n.image, link.from_socket.name
    for s in n.inputs:
        img, out = upstream_image(s, depth + 1)
        if img:
            # a Separate Color node in between tells which channel is used
            if n.type in ('SEPARATE_COLOR', 'SEPRGB', 'SEPARATE_RGB'):
                return img, link.from_socket.name
            return img, out
    return None, None


def pixels(img, w=None, h=None):
    if w and (img.size[0] != w or img.size[1] != h):
        img = img.copy(); img.scale(w, h)
    a = np.zeros(img.size[0] * img.size[1] * 4, dtype=np.float32); img.pixels.foreach_get(a)
    return a.reshape(-1, 4)


def fit(img):
    w, h = img.size; s = min(1.0, MAXTEX / max(w, h))
    return max(4, int(w * s)), max(4, int(h * s))


def save_px(px, w, h, path, fmt, linear=False):
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    if linear:
        im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(px.ravel()); im.filepath_raw = path; im.file_format = fmt; im.save()
    bpy.data.images.remove(im)


def export_material(mat, orig, paint, over):
    b = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat.use_nodes else None
    base = os.path.join(TEX, mat.name)
    col = [1.0, 1.0, 1.0, 1.0]; met = 0.0; smooth = 0.4; kind = 'opaque'
    if b:
        diff, _ = upstream_image(b.inputs['Base Color'])
        if diff:
            w, h = fit(diff); px = pixels(diff, w, h); px[:, 3] = 1.0
            save_px(px, w, h, base + '_diff.jpg', 'JPEG')
        else:
            col[:3] = list(b.inputs['Base Color'].default_value[:3])
        met = b.inputs['Metallic'].default_value
        smooth = 1.0 - b.inputs['Roughness'].default_value
        rimg, rch = upstream_image(b.inputs['Roughness'])
        mimg, mch = upstream_image(b.inputs['Metallic'])
        if rimg or mimg:
            src = rimg or mimg; w, h = fit(src); p = pixels(src, w, h)
            ch = {'Red': 0, 'Green': 1, 'Blue': 2, 'R': 0, 'G': 1, 'B': 2}
            out = np.zeros_like(p)
            out[:, 0] = p[:, ch.get(mch, 2)] if mimg else met
            out[:, 1] = 1.0
            out[:, 3] = 1.0 - p[:, ch.get(rch, 1)] if rimg else smooth
            save_px(out, w, h, base + '_ms.png', 'PNG', linear=True)
        nimg, _ = upstream_image(b.inputs['Normal'])
        if nimg:
            w, h = fit(nimg); px = pixels(nimg, w, h); px[:, 3] = 1.0
            save_px(px, w, h, base + '_nor.jpg', 'JPEG')
        alpha = b.inputs['Alpha'].default_value
        if alpha < 0.9 or b.inputs['Alpha'].is_linked:
            low = orig.lower()
            kind = 'lamp' if ('red' in low or 'light' in low or 'lamp' in low) else 'glass'
            col[3] = alpha
    if paint and orig == paint:
        kind = 'paint'
    for key, v in over.items():
        if key in orig:
            col[:3] = list(v[:3]); met, smooth, kind = v[3], v[4], v[5]
            break
    with open(base + '_pbr.txt', 'w') as f:
        f.write('%.3f %.3f %.3f %.3f %.3f %.3f %s\n' % (col[0], col[1], col[2], col[3], met, smooth, kind))


def decimate(o, target):
    n = tris(o)
    if n <= target or n == 0:
        return
    m = o.modifiers.new('dec', 'DECIMATE'); m.ratio = max(0.01, target / n); m.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)


def preview(obj, name):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 480, 270
    d = max(obj.dimensions)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = d * 1.15
    sc.camera = cam
    for tag, loc in (('side', (d * 2, 0, obj.dimensions.z * 0.5)), ('q', (d * 1.4, -d * 1.4, d * 0.9))):
        cam.location = loc
        direction = mathutils.Vector((0, 0, obj.dimensions.z * 0.4)) - cam.location
        cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(PREVIEW, name + '_' + tag + '.png')
        bpy.ops.render.render(write_still=True)


for spec in SPECS:
    name, asset, length, yaw, budget, paint, over, drop = spec[:8]
    opts = spec[8] if len(spec) > 8 else {}
    if ONLY and name not in ONLY:
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glob.glob(os.path.join(SRC, asset, '*.gltf'))[0])
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and any(k.startswith('obj:') and k[4:] in o.name for k in drop):
            bpy.data.objects.remove(o, do_unlink=True)
    drop = [k for k in drop if not k.startswith('obj:')]
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.make_single_user(type='SELECTED_OBJECTS', object=True, obdata=True)
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active; obj.name = name
    # drop unwanted materials (ground planes, decals)
    if drop:
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bpy.ops.object.mode_set(mode='OBJECT')
        idx = {i for i, m in enumerate(obj.data.materials) if m and any(k in m.name for k in drop)}
        for p in obj.data.polygons:
            p.select = p.material_index in idx
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.delete(type='FACE'); bpy.ops.object.mode_set(mode='OBJECT')
    # orient: longest horizontal side along Y, then the per-model yaw
    dims = obj.dimensions
    rot = mathutils.Matrix.Rotation(math.radians(yaw + (90 if dims.x > dims.y else 0)), 4, 'Z')
    s = 1.0 if length is None else length / max(dims.x, dims.y)
    if 'height' in opts:
        s = opts['height'] / dims.z
    obj.data.transform(mathutils.Matrix.Scale(s, 4) @ rot)
    if 'stretch' in opts:
        co = np.zeros(len(obj.data.vertices) * 3); obj.data.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
        y0, y1 = co[:, 1].min(), co[:, 1].max(); L = y1 - y0; add = length - L
        a, b = y0 + opts['stretch'][0] * L, y0 + opts['stretch'][1] * L
        y = co[:, 1]
        co[:, 1] = np.where(y <= a, y, np.where(y >= b, y + add, a + (y - a) * (1 + add / (b - a))))
        obj.data.vertices.foreach_set('co', co.ravel()); obj.data.update()
    if 'width' in opts:
        xs = [v.co.x for v in obj.data.vertices]
        obj.data.transform(mathutils.Matrix.Diagonal((opts['width'] / (max(xs) - min(xs)), 1, 1, 1)))
    # weld coincident vertices so decimation does not crack the shell
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode='OBJECT')
    before = tris(obj)
    origs = [m.name if m else '' for m in obj.data.materials]
    for i, m in enumerate(obj.data.materials):
        if m:
            m['orig'] = m.name
            m.name = 'SF_%s_%02d' % (name, i)
    # split by material; each part gets a share of the budget
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    parts = [o for o in bpy.data.objects if o.type == 'MESH']
    total = sum(tris(p) for p in parts)
    for p in parts:
        decimate(p, max(120, int(budget * tris(p) / max(1, total))))
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active; obj.name = name
    # fresh normals: smooth shading split at 35 degrees
    me = obj.data
    if me.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    if hasattr(bpy.ops.object, 'shade_smooth_by_angle'):
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    else:
        bpy.ops.object.shade_smooth(); me.use_auto_smooth = True; me.auto_smooth_angle = math.radians(35)
    # centre on X/Y, base on the ground
    pts = [mathutils.Vector(c) for c in obj.bound_box]
    c = mathutils.Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2,
                          (min(p.y for p in pts) + max(p.y for p in pts)) / 2, min(p.z for p in pts)))
    me.transform(mathutils.Matrix.Translation(-c)); obj.location = (0, 0, 0)
    for m in me.materials:
        if m:
            export_material(m, m.get('orig', m.name), paint, over)
    size = tuple(round(x, 2) for x in obj.dimensions)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(MOD, name + '.fbx'), use_selection=True, axis_forward='-Z',
                             axis_up='Y', global_scale=1.0, apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
    print('VEHICLE', name, 'tris', before, '->', tris(obj), 'size', size, 'mats', len(me.materials))
    if PREVIEW:
        os.makedirs(PREVIEW, exist_ok=True); preview(obj, name)
