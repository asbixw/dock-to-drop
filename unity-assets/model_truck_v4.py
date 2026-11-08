import bpy, bmesh, math, os
from mathutils import Vector, Euler, Matrix

OUT = os.path.dirname(os.path.abspath(__file__))
FONT_BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
FONT_REG = "/System/Library/Fonts/Supplemental/Arial.ttf"

# ------------------------------------------------------------------ helpers
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name, color, metallic=0.0, rough=0.5, alpha=1.0, emission=None, emission_strength=2.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, alpha)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*color, alpha)
    b.inputs['Metallic'].default_value = metallic
    b.inputs['Roughness'].default_value = rough
    if 'Coat Weight' in b.inputs:
        b.inputs['Coat Weight'].default_value = 0.55
        b.inputs['Coat Roughness'].default_value = 0.06
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    if emission is not None:
        if 'Emission Color' in b.inputs:
            b.inputs['Emission Color'].default_value = (*emission, 1)
            b.inputs['Emission Strength'].default_value = emission_strength
    return m


def apply_mods(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for m in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except Exception as e:
            print("modifier fail", m.name, e)
    obj.select_set(False)


def bevel(obj, width=.02, segments=3, angle=30):
    if width <= 0:
        return obj
    m = obj.modifiers.new('bevel', 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(angle)
    apply_mods(obj)
    return obj


def box(name, sx, sy, sz, loc, material, bev=.02, rot=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel(o, bev)
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def cyl(name, r, d, loc, material, rot=(0, 0, 0), verts=32, parent=None, bev=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    if bev:
        bevel(o, bev, 2)
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def cone(name, r1, r2, d, loc, material, rot=(0, 0, 0), verts=24, parent=None):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=d, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def torus(name, major, minor, loc, material, rot=(0, 0, 0), parent=None, mseg=32, nseg=12):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=mseg, minor_segments=nseg,
                                     location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def text(name, body, size, loc, material, rot=(0, 0, 0), extrude=.004, parent=None, font=FONT_BOLD,
         normal=None, up=(0, 0, 1)):
    cu = bpy.data.curves.new(name, type='FONT')
    cu.body = body
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.size = size
    cu.extrude = extrude
    if os.path.exists(font):
        cu.font = bpy.data.fonts.load(font, check_existing=True)
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    if normal is not None:
        n = Vector(normal).normalized()
        u = Vector(up).normalized()
        r = u.cross(n).normalized()
        R = Matrix((r, u, n)).transposed().to_4x4()
        ob.matrix_world = Matrix.Translation(Vector(loc)) @ R
    else:
        ob.rotation_euler = rot
    ob.data.materials.append(material)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob.select_set(False)
    if parent:
        ob.parent = parent
    return ob


def recalc(ob):
    """Force outward-facing normals; FBX/Unity cull backfaces, Blender does not."""
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    ob.select_set(False)


def loft(name, sections, material, parent=None, smooth=True, close_ends=True):
    verts = []
    for ring in sections:
        verts.extend(ring)
    n = len(sections[0])
    faces = []
    for i in range(len(sections) - 1):
        for j in range(n):
            a = i * n + j
            b = i * n + (j + 1) % n
            c = (i + 1) * n + (j + 1) % n
            d = (i + 1) * n + j
            faces.append((a, b, c, d))
    if close_ends:
        faces.append(tuple(range(n - 1, -1, -1)))
        faces.append(tuple(range((len(sections) - 1) * n, len(sections) * n)))
    me = bpy.data.meshes.new(name + "Mesh")
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    recalc(ob)
    if parent:
        ob.parent = parent
    return ob


def body_ring(y, hwb, hwt, z0, z1, corner=.18, crown=.05, steps=4, side_steps=3, top_steps=7, bottom_steps=3):
    """Rounded truck cross-section with tumblehome and a crowned roof."""
    pts = []
    c = corner
    # bottom-right corner
    for i in range(steps + 1):
        a = -math.pi / 2 + (math.pi / 2) * i / steps
        pts.append((hwb - c + math.cos(a) * c, y, z0 + c + math.sin(a) * c))
    # right side, leaning in toward the roof
    for i in range(1, side_steps + 1):
        t = i / (side_steps + 1.0)
        x = hwb + (hwt - hwb) * t
        z = z0 + c + (z1 - c - (z0 + c)) * t
        pts.append((x, y, z))
    # top-right corner
    for i in range(steps + 1):
        a = (math.pi / 2) * i / steps
        pts.append((hwt - c + math.cos(a) * c, y, z1 - c + math.sin(a) * c))
    # crowned top edge, from right to left
    x0, x1 = hwt - c, -hwt + c
    for i in range(1, top_steps):
        t = i / float(top_steps)
        x = x0 + (x1 - x0) * t
        z = z1 + crown * math.sin(math.pi * t)
        pts.append((x, y, z))
    # top-left corner
    for i in range(steps + 1):
        a = math.pi / 2 + (math.pi / 2) * i / steps
        pts.append((-hwt + c + math.cos(a) * c, y, z1 - c + math.sin(a) * c))
    # left side
    for i in range(1, side_steps + 1):
        t = i / (side_steps + 1.0)
        x = -hwt - (hwb - hwt) * t
        z = z1 - c - (z1 - c - (z0 + c)) * t
        pts.append((x, y, z))
    # bottom-left corner
    for i in range(steps + 1):
        a = math.pi + (math.pi / 2) * i / steps
        pts.append((-hwb + c + math.cos(a) * c, y, z0 + c + math.sin(a) * c))
    # flat bottom edge
    x0, x1 = -hwb + c, hwb - c
    for i in range(1, bottom_steps):
        t = i / float(bottom_steps)
        pts.append((x0 + (x1 - x0) * t, y, z0))
    return pts


def prism_y(name, profile, thickness, loc, material, rot=(0, 0, 0), parent=None):
    """Extrude a 2D XZ profile along Y."""
    t = thickness * 0.5
    verts = [(x, -t, z) for x, z in profile] + [(x, t, z) for x, z in profile]
    n = len(profile)
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    faces.append(tuple(range(n - 1, -1, -1)))
    faces.append(tuple(range(n, 2 * n)))
    me = bpy.data.meshes.new(name + 'Mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    ob.data.materials.append(material)
    for p in me.polygons:
        p.use_smooth = False
    recalc(ob)
    if parent:
        ob.parent = parent
    return ob


def prism_x(name, profile_yz, x0, x1, material, parent=None):
    """Extrude a 2D YZ profile along X."""
    verts = [(x0, y, z) for y, z in profile_yz] + [(x1, y, z) for y, z in profile_yz]
    n = len(profile_yz)
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    faces.append(tuple(range(n - 1, -1, -1)))
    faces.append(tuple(range(n, 2 * n)))
    me = bpy.data.meshes.new(name + 'Mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material)
    for p in me.polygons:
        p.use_smooth = False
    recalc(ob)
    if parent:
        ob.parent = parent
    return ob


def cut(target, cutter, keep_materials=False):
    m = target.modifiers.new('cut', 'BOOLEAN')
    m.operation = 'DIFFERENCE'
    m.object = cutter
    m.solver = 'EXACT'
    if keep_materials:
        # faces made by the cutter keep the cutter's material
        m.material_mode = 'TRANSFER'
    bpy.context.view_layer.objects.active = target
    target.select_set(True)
    bpy.ops.object.modifier_apply(modifier=m.name)
    target.select_set(False)
    bpy.data.objects.remove(cutter, do_unlink=True)


def cube_cut(name, size, loc, rot=(0, 0, 0), bev=.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bev > 0:
        bevel(o, bev, 3, 30)
    o.hide_render = True
    return o


def cyl_cut(name, r, d, loc, rot=(0, 0, 0), verts=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.hide_render = True
    return o


def tube_curve(name, pts, radius, material, parent=None, res=3):
    cu = bpy.data.curves.new(name, type='CURVE')
    cu.dimensions = '3D'
    cu.resolution_u = res
    cu.bevel_depth = radius
    cu.bevel_resolution = 3
    sp = cu.splines.new('POLY')
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        sp.points[i].co = (p[0], p[1], p[2], 1)
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob.select_set(False)
    if parent:
        ob.parent = parent
    return ob


def join(objs, name):
    objs = [o for o in objs if o is not None]
    if not objs:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.object
    ob.name = name
    return ob


# ------------------------------------------------------------------ materials
def materials():
    return {
        'white': mat('Cab_White', (0.82, 0.83, 0.81), 0.12, 0.20),
        'pearl': mat('Cab_Pearl', (0.90, 0.91, 0.88), 0.16, 0.16),
        'chrome': mat('Chrome', (0.86, 0.88, 0.91), 1.0, 0.035),
        'alum': mat('Aluminum', (0.68, 0.70, 0.72), 1.0, 0.20),
        'dark': mat('DarkTrim', (0.016, 0.019, 0.022), 0.18, 0.38),
        'grey': mat('GreyPlastic', (0.12, 0.13, 0.15), 0.05, 0.62),
        'rubber': mat('Rubber', (0.007, 0.008, 0.009), 0.0, 0.94),
        'glass': mat('Glass', (0.014, 0.026, 0.038), 0.55, 0.035, 0.52),
        'wind': mat('WindshieldGlass', (0.022, 0.040, 0.055), 0.45, 0.02, 0.14),
        'blue': mat('BlueAccent', (0.018, 0.065, 0.125), 0.45, 0.24),
        'amber': mat('Amber', (1.0, 0.33, 0.02), 0.0, 0.24, 1.0, (1.0, 0.20, 0.01), 2.6),
        'red': mat('RedLamp', (0.62, 0.012, 0.008), 0.0, 0.24, 1.0, (0.75, 0.008, 0.0), 2.2),
        'head': mat('Headlamp', (0.94, 0.94, 0.90), 0.0, 0.06, 1.0, (0.90, 0.86, 0.70), 2.6),
        'plate': mat('Plate', (0.86, 0.86, 0.81), 0.0, 0.50),
        # Charcoal trim for every surface that faces into the cab. Unity maps any
        # material with "interior" in its name to its own cab trim material.
        'interior': mat('Cab_Interior', (0.11, 0.115, 0.12), 0.0, 0.70),
    }


# ------------------------------------------------------------------ wheels
def tire(name, x, y, material, rim_material, hub_material, dark_material, root, r=0.535, width=0.30, rim_r=0.285):
    seg = 48
    # (radius, axial offset) profile of a steer tire: rounded shoulders,
    # a flat tread band, and a sidewall that tucks into the rim.
    prof = [(rim_r, -width * 0.5 + 0.012),
            (0.34, -width * 0.5 + 0.004),
            (0.43, -width * 0.5 + 0.002),
            (0.48, -width * 0.5 + 0.012),
            (0.510, -width * 0.5 + 0.040),
            (0.530, -width * 0.5 + 0.065),
            (0.535, -width * 0.5 + 0.100),
            (0.535, width * 0.5 - 0.100),
            (0.530, width * 0.5 - 0.065),
            (0.510, width * 0.5 - 0.040),
            (0.48, width * 0.5 - 0.012),
            (0.43, width * 0.5 - 0.002),
            (0.34, width * 0.5 - 0.004),
            (rim_r, width * 0.5 - 0.012)]
    verts = []
    for rr, ax in prof:
        for i in range(seg):
            a = 2 * math.pi * i / seg
            verts.append((x + ax, y + rr * math.cos(a), r + rr * math.sin(a)))
    faces = []
    n = len(prof)
    for k in range(n - 1):
        for i in range(seg):
            a = k * seg + i
            b = k * seg + (i + 1) % seg
            c = (k + 1) * seg + (i + 1) % seg
            d = (k + 1) * seg + i
            faces.append((a, b, c, d))
    me = bpy.data.meshes.new(name + 'Mesh')
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material)
    for p in me.polygons:
        p.use_smooth = True
    recalc(ob)
    ob.parent = root
    # longitudinal highway-tire grooves instead of knobby off-road blocks
    for off in (-0.075, -0.025, 0.025, 0.075):
        torus('TreadGroove_%s_%d' % (name, int(off * 1000)), r - 0.001, 0.004,
              (x + off, y, r), dark_material, (0, math.pi / 2, 0), root, 48, 6)
    # rim and hub
    cyl('Rim_' + name, rim_r + 0.006, width * 0.92, (x, y, r), rim_material, (0, math.pi / 2, 0), 40, root, .004)
    cyl('RimWell_' + name, rim_r - 0.004, 0.010, (x + width * 0.36, y, r), dark_material, (0, math.pi / 2, 0), 40, root)
    cyl('RimFace_' + name, rim_r - 0.024, 0.016, (x + width * 0.46, y, r), rim_material, (0, math.pi / 2, 0), 40, root, .002)
    torus('RimLip_' + name, rim_r - 0.006, 0.010, (x + width * 0.47, y, r), hub_material, (0, math.pi / 2, 0), root, 40, 8)
    cyl('Hub_' + name, 0.105, width * 0.98, (x + width * 0.47, y, r), hub_material, (0, math.pi / 2, 0), 24, root)
    cyl('HubCap_' + name, 0.055, 0.020, (x + width * 0.505, y, r), hub_material, (0, math.pi / 2, 0), 20, root)
    for i in range(10):
        a = 2 * math.pi * i / 10
        cyl('Lug_%s_%d' % (name, i), 0.016, 0.018,
            (x + width * 0.485, y + math.cos(a) * 0.185, r + math.sin(a) * 0.185),
            dark_material, (0, math.pi / 2, 0), 8, root)
    cyl('BrakeDrum_' + name, 0.195, 0.16, (x - width * 0.20, y, r), dark_material, (0, math.pi / 2, 0), 24, root)
    return ob


def define_wheels(root, M):
    rubber, alum, chrome, dark = M['rubber'], M['alum'], M['chrome'], M['dark']
    # front steer axle
    for s in (-1, 1):
        tire('Wheel_F%s' % ('L' if s < 0 else 'R'), s * 1.16, 2.95, rubber, alum, chrome, dark, root)
    # rear tandem, dual wheels
    for y in (-3.65, -4.95):
        for s in (-1, 1):
            tire('Wheel_R%sO_%d' % ('L' if s < 0 else 'R', int(-y * 10)), s * 1.18, y, rubber, alum, chrome, dark, root)
            tire('Wheel_R%sI_%d' % ('L' if s < 0 else 'R', int(-y * 10)), s * 0.84, y, rubber, alum, chrome, dark, root)


# ------------------------------------------------------------------ doors
def build_door(side, name, root, M, moving=False):
    white, dark, chrome, glass = M['white'], M['dark'], M['chrome'], M['glass']
    # Door skin sits only ~12 mm proud of the cab side so it reads as a shut
    # panel, not a slab bolted onto the outside.
    x = side * 1.268
    parts = []
    parts.append(box(name + '_Lower', 0.050, 1.88, 1.12, (x, 0.40, 1.86), white, .035))
    parts.append(box(name + '_FrameFront', 0.050, 0.032, 0.94, (x, 1.305, 2.87), white, .010))
    parts.append(box(name + '_FrameRear', 0.050, 0.032, 0.94, (x, -0.505, 2.87), white, .010))
    parts.append(box(name + '_FrameTop', 0.050, 1.88, 0.032, (x, 0.40, 3.325), white, .010))
    parts.append(box(name + '_FrameBottom', 0.050, 1.88, 0.032, (x, 0.40, 2.415), white, .010))
    # thin black rubber seal inside the body-colour frame
    parts.append(box(name + '_SealFront', 0.052, 0.014, 0.90, (x, 1.285, 2.87), dark, .004))
    parts.append(box(name + '_SealRear', 0.052, 0.014, 0.90, (x, -0.485, 2.87), dark, .004))
    parts.append(box(name + '_SealTop', 0.052, 1.80, 0.014, (x, 0.40, 3.305), dark, .004))
    parts.append(box(name + '_SealBottom', 0.052, 1.80, 0.014, (x, 0.40, 2.435), dark, .004))
    # Door card and window-frame liners on the cab side of the door. The door
    # boxes are solid and painted, so without these the driver looks straight at
    # a white panel. Inner face of the door skin is at |x| = 1.243.
    trim = M['interior']
    xi = side * 1.236
    parts.append(box(name + '_Card', 0.014, 1.84, 1.09, (xi, 0.40, 1.855), trim, .012))
    parts.append(box(name + '_LinerFront', 0.014, 0.040, 0.94, (xi, 1.305, 2.87), trim, .004))
    parts.append(box(name + '_LinerRear', 0.014, 0.040, 0.94, (xi, -0.505, 2.87), trim, .004))
    parts.append(box(name + '_LinerTop', 0.014, 1.88, 0.040, (xi, 0.40, 3.325), trim, .004))
    parts.append(box(name + '_LinerBottom', 0.014, 1.88, 0.040, (xi, 0.40, 2.415), trim, .004))
    parts.append(box(name + '_Glass', 0.016, 1.80, 0.86, (x + side * 0.010, 0.40, 2.87), glass, .006))
    parts.append(box(name + '_VentDivider', 0.026, 0.026, 0.90, (x + side * 0.018, 1.04, 2.87), chrome, .005))
    parts.append(box(name + '_VentGlass', 0.014, 0.22, 0.82, (x + side * 0.014, 1.17, 2.87), glass, .004))
    parts.append(box(name + '_Beltline', 0.016, 1.80, 0.020, (x + side * 0.030, 0.40, 2.40), chrome, .004))
    parts.append(box(name + '_TopTrim', 0.010, 1.80, 0.014, (x + side * 0.030, 0.40, 3.345), chrome, .003))
    parts.append(box(name + '_LowerTrim', 0.018, 1.60, 0.024, (x + side * 0.030, 0.40, 1.42), dark, .004))
    parts.append(box(name + '_Crease', 0.008, 1.70, 0.012, (x + side * 0.028, 0.40, 1.78), white, .002))
    parts.append(box(name + '_Handle', 0.028, 0.22, 0.048, (x + side * 0.030, 1.06, 2.27), chrome, .010))
    parts.append(box(name + '_Lock', 0.024, 0.032, 0.040, (x + side * 0.030, 1.18, 2.20), chrome, .005))
    parts.append(text(name + '_Word', 'CASCADIA', 0.105, (x + side * 0.030, 0.35, 2.12), chrome,
                      extrude=0.003, normal=(side, 0, 0), up=(0, 0, 1)))
    parts.append(text(name + '_Word2', 'FREIGHTLINER', 0.042, (x + side * 0.030, 0.35, 2.02), dark,
                      extrude=0.002, normal=(side, 0, 0), up=(0, 0, 1)))
    # thin black rubber shut line on the body around the opening
    for yy, zz, sy, sz in [(1.35, 2.31, 0.014, 2.06), (-0.55, 2.31, 0.014, 2.06),
                           (0.40, 3.34, 1.90, 0.014), (0.40, 1.28, 1.90, 0.014)]:
        box(name + '_Seal', 0.014, sy, sz, (side * 1.278, yy, zz), dark, .004, parent=root)
    door = join(parts, name)
    if moving:
        bpy.context.scene.cursor.location = (side * 1.268, 1.34, 2.31)
        bpy.context.view_layer.objects.active = door
        door.select_set(True)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        door.select_set(False)
    door.parent = root
    return door


# ------------------------------------------------------------------ truck
def make_truck():
    root = bpy.data.objects.new('TruckRoot', None)
    bpy.context.collection.objects.link(root)
    M = materials()
    white, pearl, chrome, alum = M['white'], M['pearl'], M['chrome'], M['alum']
    dark, grey, rubber, glass = M['dark'], M['grey'], M['rubber'], M['glass']
    wind, blue, amber, red = M['wind'], M['blue'], M['amber'], M['red']
    head, plate = M['head'], M['plate']

    # ---- chassis -------------------------------------------------
    for x in (-0.55, 0.55):
        box('FrameRail', 0.16, 7.55, 0.26, (x, -1.20, 0.98), dark, .012, parent=root)
    for y in (-5.30, -4.55, -3.85, -3.10, -2.40, -1.60, -0.80, 0.0, 0.80, 1.60, 2.40, 3.20):
        box('Crossmember', 1.18, 0.12, 0.16, (0, y, 0.98), dark, .005, parent=root)
    for y in (2.95, -3.65, -4.95):
        cyl('Axle', 0.075, 2.20, (0, y, 0.58), dark, (0, math.pi / 2, 0), 20, root)
        box('LeafSpring', 1.90, 0.08, 0.08, (0, y, 0.78), grey, .006, parent=root)
    box('Driveshaft', 0.10, 4.0, 0.10, (0, -1.8, 0.70), alum, .012, parent=root)
    cyl('Differential', 0.24, 0.40, (0, -3.65, 0.58), dark, (0, 0, 0), 20, root)

    # ---- cab + sleeper shell ------------------------------------
    # Cross-sections use a crowned roof and tumblehome so the cab reads as a
    # rounded casting rather than a white shipping crate.
    spec = [(1.55, 1.28, 1.20, 1.12, 3.55, 0.060),
            (1.35, 1.28, 1.20, 1.12, 3.56, 0.060),
            (1.05, 1.29, 1.21, 1.12, 3.58, 0.060),
            (0.60, 1.30, 1.22, 1.12, 3.61, 0.060),
            (0.10, 1.30, 1.22, 1.12, 3.64, 0.055),
            (-0.40, 1.29, 1.21, 1.12, 3.67, 0.050),
            (-0.90, 1.28, 1.20, 1.12, 3.68, 0.050),
            (-1.20, 1.27, 1.19, 1.12, 3.68, 0.050),
            (-1.60, 1.26, 1.18, 1.12, 3.67, 0.045),
            (-2.10, 1.25, 1.17, 1.12, 3.66, 0.045),
            (-2.50, 1.24, 1.16, 1.12, 3.64, 0.040),
            (-2.75, 1.22, 1.14, 1.12, 3.62, 0.040)]
    sections = [body_ring(y, hwb, hwt, z0, z1, .19, crown) for y, hwb, hwt, z0, z1, crown in spec]
    shell = loft('CabShell', sections, white, root, True, True)
    bevel(shell, .012, 2, 28)

    # Hollow the cab and sleeper. The loft above is a solid block; without this
    # the seated camera sits inside it and sees the cut faces of the door and
    # windshield openings as cream slabs. The cavity leaves a 45 mm skin, and its
    # faces take Cab_Interior, so the inside of the shell is trim, not paint.
    skin = 0.045
    cav_spec = [(1.45, 1.28, 1.20, 1.12, 3.55, 0.060)] + spec[1:-1] + [(-2.68, 1.22, 1.14, 1.12, 3.62, 0.040)]
    cav_sections = [body_ring(y, hwb - skin, hwt - skin, z0 + 0.025, z1 - skin, .19 - skin, crown)
                    for y, hwb, hwt, z0, z1, crown in cav_spec]
    cavity = loft('CabCavity', cav_sections, M['interior'], None, True, True)
    cavity.hide_render = True
    cut(shell, cavity, keep_materials=True)

    # Windshield opening: trapezoid, raked back 19.5 degrees.
    rake = math.radians(19.5)
    wprof = [(-1.06, -0.56), (1.06, -0.56), (0.99, 0.56), (-0.99, 0.56)]
    # Every opening's jamb faces take Cab_Interior: from the seat they read as
    # dark trim around the glass, never as a painted edge.
    wcut = prism_y('WSCut', wprof, 0.75, (0, 1.49, 2.83), M['interior'], (rake, 0, 0))
    wcut.hide_render = True
    cut(shell, wcut, keep_materials=True)

    def trim_cut(o):
        o.data.materials.append(M['interior'])
        cut(shell, o, keep_materials=True)
    # Door openings on both sides, sleeper windows, rear window.
    for s in (-1, 1):
        trim_cut(cube_cut('DoorCut', (.52, 1.90, 2.06), (s * 1.29, .40, 2.31), (0, 0, 0), .10))
    for s in (-1, 1):
        trim_cut(cube_cut('SleeperCut', (.52, .92, .44), (s * 1.245, -1.95, 3.08), (0, 0, 0), .07))
    trim_cut(cube_cut('RearCut', (1.62, .52, .38), (0, -2.75, 3.10), (0, 0, 0), .06))
    bevel(shell, .006, 2, 35)

    # Glass panels. Windshield is nearly clear; the rest is tinted.
    ws = prism_y('WindshieldGlass', [(-1.05, -0.575), (1.05, -0.575), (0.985, 0.575), (-0.985, 0.575)],
                 0.018, (0, 1.465, 2.83), wind, (rake, 0, 0))
    ws.parent = root
    # black gasket around the windshield
    for s in (-1, 1):
        box('WSGasketSide', 0.050, 0.045, 1.22, (s * 1.035, 1.455, 2.84), dark, .008,
            (rake, 0, math.radians(-4 * s)), root)
    box('WSGasketTop', 2.12, 0.045, 0.050, (0, 1.305, 3.405), dark, .008, (rake, 0, 0), root)
    box('WSGasketBottom', 2.22, 0.045, 0.050, (0, 1.605, 2.265), dark, .008, (rake, 0, 0), root)
    # subtle body-colour visor lip over the windshield
    # wiper arms lying on the lower windshield
    for s in (-1, 1):
        box('WiperArm', 0.62, 0.020, 0.016, (s * 0.38, 1.578, 2.34), dark, .004,
            (rake, 0, math.radians(7 * s)), root)
        box('WiperBlade', 0.56, 0.014, 0.024, (s * 0.38, 1.556, 2.39), dark, .004,
            (rake, 0, math.radians(7 * s)), root)

    for s in (-1, 1):
        box('SleeperGasket', 0.024, .94, .46, (s * 1.252, -1.95, 3.08), dark, .012, parent=root)
        box('SleeperGlass', 0.018, .86, .38, (s * 1.260, -1.95, 3.08), glass, .006, parent=root)
        box('SleeperTrim', 0.010, 1.00, 0.020, (s * 1.268, -1.95, 3.29), chrome, .002, parent=root)
        text('SleeperBadge', 'CASCADIA', 0.080, (s * 1.252, -1.55, 2.28), chrome,
             extrude=0.003, parent=root, normal=(s, 0, 0), up=(0, 0, 1))
        text('SleeperBadge2', '72" SLEEPER', 0.032, (s * 1.252, -1.55, 2.17), dark,
             extrude=0.002, parent=root, normal=(s, 0, 0), up=(0, 0, 1))
    box('RearGasket', 1.64, .024, .40, (0, -2.764, 3.10), dark, .012, parent=root)
    box('RearGlass', 1.54, .018, .32, (0, -2.772, 3.10), glass, .006, parent=root)

    # ---- aerodynamic roof fairing -------------------------------
    # The tall sleeper wedge is what makes a Cascadia read as a highway
    # tractor instead of a box van.
    fair_spec = [(1.45, 1.17, 1.10, 3.53, 3.58, 0.020),
                 (1.10, 1.17, 1.10, 3.53, 3.64, 0.030),
                 (0.60, 1.18, 1.11, 3.53, 3.74, 0.040),
                 (0.00, 1.18, 1.11, 3.53, 3.88, 0.050),
                 (-0.80, 1.18, 1.11, 3.53, 4.01, 0.050),
                 (-1.60, 1.17, 1.10, 3.53, 4.08, 0.050),
                 (-2.30, 1.16, 1.09, 3.53, 4.07, 0.050),
                 (-2.65, 1.14, 1.07, 3.53, 4.00, 0.040)]
    fair_sections = [body_ring(y, hwb, hwt, z0, z1, .14, crown) for y, hwb, hwt, z0, z1, crown in fair_spec]
    fairing = loft('RoofFairing', fair_sections, white, root, True, True)
    bevel(fairing, .010, 2, 30)

    # ---- doors --------------------------------------------------
    door = build_door(-1, 'DriverDoor', root, M, moving=True)
    build_door(1, 'PassengerDoor', root, M, moving=False)

    # ---- hood ---------------------------------------------------
    # Current-generation aero hood: it drops about 55 cm from the cowl to the
    # nose and pulls in hard at the top corners, so the front reads rounded and
    # sloped like a 2018+ Cascadia, not flat like a classic long-nose.
    hood_spec = [(1.55, 1.27, 1.20, 1.06, 2.33, 0.035),
                 (1.80, 1.27, 1.19, 1.05, 2.31, 0.040),
                 (2.15, 1.26, 1.16, 1.04, 2.26, 0.045),
                 (2.55, 1.24, 1.12, 1.03, 2.18, 0.050),
                 (2.95, 1.21, 1.08, 1.02, 2.09, 0.050),
                 (3.35, 1.17, 1.02, 1.02, 1.98, 0.050),
                 (3.70, 1.12, 0.96, 1.02, 1.87, 0.045),
                 (4.02, 1.06, 0.88, 1.02, 1.77, 0.040)]
    hood_sections = [body_ring(y, hwb, hwt, z0, z1, .24, crown) for y, hwb, hwt, z0, z1, crown in hood_spec]
    hood = loft('Hood', hood_sections, white, root, True, True)
    # muscular centre bulge over the engine
    bulge_spec = [(1.62, 0.62, 0.58, 2.31, 2.36, 0.015),
                  (2.20, 0.60, 0.56, 2.25, 2.30, 0.015),
                  (3.00, 0.56, 0.52, 2.09, 2.14, 0.015),
                  (3.70, 0.50, 0.46, 1.87, 1.92, 0.015),
                  (4.00, 0.46, 0.42, 1.77, 1.82, 0.012)]
    bulge_sections = [body_ring(y, hwb, hwt, z0, z1, .06, crown) for y, hwb, hwt, z0, z1, crown in bulge_spec]
    bulge = loft('HoodBulge', bulge_sections, white, root, True, True)
    bevel(bulge, .010, 2, 30)
    # cut real front wheel arches into the hood sides
    for s in (-1, 1):
        cut(hood, cyl_cut('ArchCut', 0.665, 0.52, (s * 1.29, 2.95, 0.55), (0, math.pi / 2, 0), 40))
    # grille recess
    # big trapezoid grille opening: 1.70 m wide at the bumper, 1.36 m at the top
    GPROF = [(-0.85, -0.36), (0.85, -0.36), (0.68, 0.32), (-0.68, 0.32)]
    gcut = prism_y('GrilleCut', GPROF, 0.42, (0, 4.02, 1.36), white)
    gcut.hide_render = True
    cut(hood, gcut)
    bevel(hood, .010, 2, 30)
    # inner fender liners and fender lips
    for s in (-1, 1):
        cyl('InnerFender', 0.645, 0.020, (s * 1.035, 2.95, 0.55), dark, (0, math.pi / 2, 0), 40, root)
        pts = []
        for i in range(19):
            a = math.radians(2 + i * 10.0)
            pts.append((s * 1.275, 2.95 + math.cos(a) * 0.675, 0.55 + math.sin(a) * 0.675))
        tube_curve('FenderLip', pts, 0.014, white, root, 3)
        pts2 = []
        for i in range(17):
            a = math.radians(8 + i * 10.6)
            pts2.append((s * 1.282, 2.95 + math.cos(a) * 0.625, 0.55 + math.sin(a) * 0.625))
        tube_curve('FenderTrim', pts2, 0.008, dark, root, 2)
    # cowl vent and hood centre seam
    box('CowlVent', 1.58, 0.08, 0.03, (0, 1.60, 2.325), dark, .006, (rake, 0, 0), root)
    for s in (-1, 1):
        for i in range(3):
            box('HoodVent', 0.02, 0.44, 0.030, (s * 1.185, 3.02, 1.62 + i * 0.052), dark, .004, parent=root)

    # ---- grille / front -----------------------------------------
    # Chrome surround sits on the cut opening; the dark mesh is recessed.
    box('GrilleTop', 1.42, 0.075, 0.065, (0, 4.045, 1.685), chrome, .020, parent=root)
    box('GrilleBottom', 1.76, 0.075, 0.065, (0, 4.045, 1.005), chrome, .020, parent=root)
    lean = math.atan2(0.17, 0.68)
    for s in (-1, 1):
        box('GrilleSide', 0.070, 0.075, 0.71, (s * 0.770, 4.045, 1.345), chrome, .020,
            (0, -lean * s, 0), root)
    prism_y('GrilleMesh', [(x * 0.95, z * 0.95) for x, z in GPROF], 0.025, (0, 4.005, 1.36), dark, parent=root)
    for i in range(5):
        z = 1.12 + i * 0.12
        w = 1.62 - (z - 1.0) / 0.68 * 0.34
        box('GrilleBar', w, 0.022, 0.034, (0, 4.025, z), chrome, .005, parent=root)
    text('FreightlinerBadge', 'FREIGHTLINER', 0.050, (0, 4.060, 1.685), dark,
         extrude=0.002, parent=root, normal=(0, 1, 0), up=(0, 0, 1))

    # Swept headlamps: long angular pods that start beside the grille and wrap
    # back around the hood corner, as on the 2018+ truck. Each part is placed in
    # lamp space and then yawed outward by `a` around the pod centre.
    hl = [(-0.30, -0.11), (0.26, -0.085), (0.30, 0.065), (-0.25, 0.13)]
    for s in (-1, 1):
        a = math.radians(-38 * s)
        cx, cy, cz = s * 0.935, 3.875, 1.535
        def at(lx, ly, lz):
            lx *= s
            return (cx + lx * math.cos(a) - ly * math.sin(a), cy + lx * math.sin(a) + ly * math.cos(a), cz + lz)
        prof = [(x * s, z) for x, z in hl]
        if s < 0:
            prof = prof[::-1]
        for nm, k, th, ly, mt in (('HeadlightBezel', 1.0, 0.11, 0.0, grey),
                                  ('HeadlightHousing', 0.92, 0.05, 0.065, dark),
                                  ('HeadlightLens', 0.86, 0.012, 0.094, wind)):
            o = prism_y(nm, [(x * k, z * k) for x, z in prof], th, at(0, ly, 0), mt, parent=root)
            o.rotation_euler = (0, 0, a)
        cyl('ProjectorRim', 0.062, 0.022, at(0.13, 0.086, 0.01), chrome, (math.pi / 2, 0, a), 28, root)
        cyl('ProjectorLens', 0.047, 0.014, at(0.13, 0.095, 0.01), head, (math.pi / 2, 0, a), 28, root)
        cyl('ReflectorRim', 0.052, 0.018, at(-0.04, 0.086, 0.02), chrome, (math.pi / 2, 0, a), 24, root)
        cyl('ReflectorLens', 0.038, 0.012, at(-0.04, 0.094, 0.02), head, (math.pi / 2, 0, a), 24, root)
        box('DRL', 0.46, 0.012, 0.016, at(0.0, 0.100, 0.085), head, .003, (0, 0, a), root)
        box('TurnSignal', 0.30, 0.012, 0.028, at(-0.10, 0.100, -0.075), amber, .005, (0, 0, a), root)

    # bumper, air dam, plate
    box('Bumper', 2.44, 0.28, 0.30, (0, 4.24, 0.80), chrome, .080, parent=root)
    for s in (-1, 1):
        box('BumperWrap', 0.12, 0.44, 0.30, (s * 1.20, 4.14, 0.80), chrome, .060,
            (0, 0, math.radians(-10 * s)), root)
    box('BumperTop', 2.46, 0.08, 0.04, (0, 4.23, 0.955), chrome, .018, parent=root)
    box('BumperStepInset', 0.72, 0.03, 0.17, (0, 4.392, 0.73), dark, .015, parent=root)
    box('AirDam', 2.18, 0.18, 0.14, (0, 4.28, 0.58), dark, .025, parent=root)
    for s in (-1, 1):
        box('FogLightBezel', 0.18, 0.04, 0.13, (s * 0.86, 4.385, 0.80), dark, .012, parent=root)
        box('FogLight', 0.12, 0.025, 0.075, (s * 0.86, 4.410, 0.80), head, .006, parent=root)
    box('LicensePlate', 0.46, 0.022, 0.20, (0, 4.405, 0.86), plate, .006, parent=root)
    text('PlateText', 'TEXAS', 0.075, (0, 4.420, 0.86), dark, extrude=.002,
         parent=root, normal=(0, 1, 0), up=(0, 0, 1))

    # ---- mirrors ------------------------------------------------
    for s in (-1, 1):
        tube_curve('MirrorArmTop',
                   [(s * 1.245, 1.32, 3.28), (s * 1.36, 1.36, 3.20), (s * 1.50, 1.40, 3.10)],
                   0.022, chrome, root, 3)
        tube_curve('MirrorArmBot',
                   [(s * 1.245, 1.32, 2.76), (s * 1.36, 1.36, 2.84), (s * 1.50, 1.40, 2.94)],
                   0.020, chrome, root, 3)
        box('MirrorHousing', 0.10, 0.24, 0.58, (s * 1.545, 1.40, 3.02), dark, .042, parent=root)
        box('MirrorBack', 0.018, 0.24, 0.58, (s * 1.492, 1.40, 3.02), chrome, .008, parent=root)
        box('MirrorGlass', 0.014, 0.20, 0.48, (s * 1.600, 1.40, 3.08), glass, .005, parent=root)
        box('MirrorConvex', 0.014, 0.16, 0.18, (s * 1.600, 1.40, 2.70), glass, .005, parent=root)
        box('MirrorTrim', 0.026, 0.25, 0.024, (s * 1.545, 1.40, 2.70), chrome, .005, parent=root)
        box('HoodMirrorArm', 0.026, 0.20, 0.026, (s * 1.04, 3.50, 1.97), dark, .004, parent=root)
        box('HoodMirror', 0.09, 0.16, 0.20, (s * 1.15, 3.56, 2.07), dark, .020, parent=root)
        box('HoodMirrorGlass', 0.014, 0.12, 0.14, (s * 1.201, 3.56, 2.08), glass, .004, parent=root)

    # ---- steps, tanks, battery ---------------------------------
    for s in (-1, 1):
        # open aluminium steps under the front of the door, on visible brackets
        for i in range(3):
            box('Step', 0.40, 0.42, 0.045, (s * 1.12, 0.95, 0.42 + i * 0.27), alum, .010, parent=root)
            box('StepTrim', 0.42, 0.44, 0.010, (s * 1.12, 0.95, 0.398 + i * 0.27), chrome, .003, parent=root)
            box('StepGrip', 0.34, 0.34, 0.008, (s * 1.12, 0.95, 0.445 + i * 0.27), dark, .002, parent=root)
        box('StepBracket', 0.06, 0.10, 0.78, (s * 0.90, 0.95, 0.72), dark, .005, parent=root)
        box('StepBracket2', 0.05, 0.08, 0.78, (s * 1.26, 0.95, 0.72), dark, .005, parent=root)
        box('CabSkirt', 0.035, 1.20, 0.14, (s * 1.272, 0.82, 1.02), white, .012, parent=root)
        box('GrabHandle', 0.030, 0.040, 0.58, (s * 1.255, 1.18, 2.60), chrome, .008, parent=root)
        box('GrabHandle2', 0.030, 0.040, 0.40, (s * 1.255, 1.18, 1.94), chrome, .008, parent=root)
    # fuel tank on the driver side
    cyl('FuelTank', 0.33, 1.55, (-0.92, -0.58, 0.80), alum, (math.pi / 2, 0, 0), 40, root, .008)
    for yy in (-1.25, -0.58, 0.09):
        box('TankStrap', 0.06, 0.10, 0.72, (-0.92, yy, 0.81), chrome, .005, parent=root)
    cyl('TankCap', 0.105, 0.055, (-0.92, -1.40, 0.94), chrome, (0, 0, 0), 20, root)
    # battery box and DEF tank on the passenger side, behind the steps
    box('BatteryBox', 0.44, 0.78, 0.48, (0.98, -0.55, 0.74), dark, .020, parent=root)
    box('BatteryLid', 0.46, 0.80, 0.04, (0.98, -0.55, 0.99), grey, .010, parent=root)
    box('BatteryLatch', 0.05, 0.08, 0.03, (0.98, -0.15, 0.94), chrome, .006, parent=root)
    cyl('DEFTank', 0.215, 0.78, (0.98, -1.55, 0.76), alum, (math.pi / 2, 0, 0), 32, root, .008)
    cyl('DEFCap', 0.085, 0.05, (0.98, -1.96, 0.92), dark, (0, 0, 0), 20, root)

    # ---- rear chassis, fifth wheel, fenders --------------------
    box('FifthWheelPlate', 1.10, 1.00, 0.10, (0, -4.30, 1.22), grey, .020, parent=root)
    box('FifthWheelRamp', 1.10, 0.22, 0.16, (0, -3.80, 1.22), grey, .020, parent=root)
    box('FifthWheelJaw', 0.20, 0.40, 0.16, (0, -4.60, 1.30), dark, .015, parent=root)
    box('FifthWheelHandle', 1.35, 0.07, 0.07, (0, -4.90, 1.15), chrome, .010, parent=root)
    box('FifthWheelBase', 0.80, 1.20, 0.12, (0, -4.30, 1.13), dark, .015, parent=root)
    # diamond-plate catwalk between the rails
    box('Catwalk', 1.28, 1.70, 0.035, (0, -4.30, 1.155), alum, .006, parent=root)
    for i in range(6):
        box('CatwalkRib', 1.20, 0.020, 0.010, (0, -5.00 + i * 0.26, 1.178), chrome, .002, parent=root)
    # tandem rear fenders: thin curved bands, not solid slabs
    outer = [(-3.05, 0.94), (-3.18, 1.17), (-3.50, 1.36), (-4.20, 1.44),
             (-4.90, 1.36), (-5.24, 1.17), (-5.42, 0.94)]
    inner = [(-5.40, 0.90), (-5.20, 1.12), (-4.88, 1.30), (-4.20, 1.38),
             (-3.52, 1.30), (-3.20, 1.12), (-3.08, 0.90)]
    fprof = outer + inner
    for s in (-1, 1):
        prism_x('RearFender', fprof, s * 0.70, s * 1.36, dark, root)
    # mud flaps and rear bumper
    for s in (-1, 1):
        box('MudFlap', 0.66, 0.05, 0.62, (s * 1.04, -5.52, 0.36), rubber, .006, parent=root)
        box('MudFlapBracket', 0.05, 0.05, 0.52, (s * 0.72, -5.50, 0.62), dark, .005, parent=root)
        box('TailLight', 0.16, 0.05, 0.14, (s * 0.90, -5.40, 0.96), red, .010, parent=root)
        box('TailSignal', 0.10, 0.05, 0.06, (s * 0.90, -5.40, 0.84), amber, .008, parent=root)
    box('RearBumper', 2.30, 0.14, 0.17, (0, -5.42, 0.62), alum, .020, parent=root)
    box('RearPlate', 0.50, 0.025, 0.22, (0, -5.50, 0.80), plate, .006, parent=root)
    text('RearPlateText', 'TEXAS', 0.085, (0, -5.515, 0.80), dark, extrude=.002,
         parent=root, normal=(0, -1, 0), up=(0, 0, 1))

    # ---- aero package -------------------------------------------
    # Fleet trucks on Relay loads run the full aero kit: white chassis fairings
    # from the cab steps to the drive axle (covering the tanks), and sleeper
    # side extenders that close the gap to the trailer. No chrome air cleaners
    # or stacks; the aftertreatment sits under the cab.
    for s in (-1, 1):
        box('ChassisFairing', 0.040, 3.36, 0.80, (s * 1.272, -1.20, 0.76), white, .018, parent=root)
        box('FairingLowerTrim', 0.046, 3.36, 0.050, (s * 1.274, -1.20, 0.385), dark, .006, parent=root)
        box('FairingSeam', 0.044, 0.012, 0.76, (s * 1.274, -0.62, 0.76), dark, .002, parent=root)
        box('SleeperExtender', 0.040, 0.40, 1.90, (s * 1.215, -2.96, 2.95), white, .015,
            (0, 0, math.radians(4 * s)), root)
        box('ExtenderEdge', 0.044, 0.020, 1.86, (s * 1.205, -3.155, 2.95), dark, .004,
            (0, 0, math.radians(4 * s)), root)
    # fuel door on the driver-side fairing, over the tank filler
    cyl('FuelDoor', 0.11, 0.012, (-1.294, -1.40, 0.98), alum, (0, math.pi / 2, 0), 24, root)
    # short horizontal exhaust outlet under the passenger fairing
    cyl('ExhaustTip', 0.06, 0.30, (1.18, -2.70, 0.40), chrome, (0, math.pi / 2, 0), 20, root)

    # coiled air lines from cab to fifth wheel
    for k, (col, off) in enumerate([(dark, 0.0), (red, 0.10)]):
        pts = []
        for i in range(30):
            t = i / 29.0
            pts.append((0.22 + off + 0.10 * math.sin(t * math.pi * 6),
                        -2.72 - 1.10 * t,
                        1.36 - 0.08 * t + 0.05 * math.cos(t * math.pi * 6)))
        tube_curve('AirLine%d' % k, pts, 0.014, col, root, 2)

    # ---- roof details ------------------------------------------
    box('RoofLightBar', 1.46, 0.10, 0.07, (0, 1.34, 3.63), dark, .010, parent=root)
    for i in range(5):
        box('ClearanceLamp', 0.10, 0.04, 0.05, (-0.48 + i * 0.24, 1.39, 3.67), amber, .005, parent=root)
    for s in (-1, 1):
        cyl('AirHorn', 0.070, 0.54, (s * 0.62, -1.25, 4.14), chrome, (math.pi / 2, 0, 0), 24, root)
        cyl('HornBracket', 0.030, 0.10, (s * 0.62, -1.25, 4.06), dark, (0, 0, 0), 10, root)
    cyl('Antenna', 0.011, 0.70, (-0.95, 0.55, 4.02), dark, (0, 0, 0), 8, root)

    # ---- side details ------------------------------------------
    for s in (-1, 1):
        box('BlueStripe', 0.012, 2.20, 0.045, (s * 1.275, -1.65, 1.36), blue, .002, parent=root)
        box('SleeperTrim', 0.012, 2.20, 0.018, (s * 1.272, -1.65, 1.31), chrome, .002, parent=root)

    # ---- wheels -------------------------------------------------
    define_wheels(root, M)

    return root


# ------------------------------------------------------------------ join / export
def finalize():
    """Join static meshes by material to keep Unity draw calls sane.

    The driver door and the clear windshield stay separate: the door has to
    pivot, and the material fixer identifies the windshield by object name.
    """
    root = bpy.data.objects.get('TruckRoot')
    exclude = {'DriverDoor', 'WindshieldGlass'}
    groups = {}
    for o in list(bpy.data.objects):
        if o.type != 'MESH' or o.name in exclude:
            continue
        if o.parent is not None and o.parent.name == 'DriverDoor':
            continue
        mat_name = o.data.materials[0].name if o.data.materials else 'NoMat'
        groups.setdefault(mat_name, []).append(o)
    for mat_name, objs in groups.items():
        objs = [o for o in objs if o.name not in exclude]
        if len(objs) < 2:
            continue
        joined = join(objs, 'Truck_' + mat_name.replace(' ', '_'))
        if joined and root is not None:
            joined.parent = root
    for o in bpy.data.objects:
        if o.name.startswith('WindshieldGlass'):
            o.name = 'WindshieldGlass'


def setup_render():
    bpy.ops.object.empty_add(location=(0, 0.3, 1.7))
    target = bpy.context.object
    target.name = 'CamTarget'
    bpy.ops.object.camera_add(location=(9.6, 13.0, 4.8))
    cam = bpy.context.object
    con = cam.constraints.new('TRACK_TO')
    con.target = target
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    bpy.context.scene.camera = cam
    cam.data.lens = 70
    bpy.ops.object.light_add(type='SUN', location=(4, -4, 10))
    sun = bpy.context.object
    sun.data.energy = 3.2
    sun.data.color = (1.0, 0.87, 0.72)
    sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(48), math.radians(-8), math.radians(38))
    bpy.ops.object.light_add(type='AREA', location=(-7, -8, 8))
    area = bpy.context.object
    area.data.energy = 1100
    area.data.size = 14
    area.data.color = (0.64, 0.78, 1.0)
    area.rotation_euler = (math.radians(45), 0, math.radians(-35))
    bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, -0.01))
    plane = bpy.context.object
    plane.name = 'Ground'
    gm = mat('Ground', (0.135, 0.145, 0.155), 0, 0.82)
    plane.data.materials.append(gm)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.taa_render_samples = 64
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new('World')
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get('Background')
    if bg:
        bg.inputs[0].default_value = (0.27, 0.37, 0.50, 1)
        bg.inputs[1].default_value = 1.0
    scene.view_settings.look = 'AgX - Medium High Contrast'
    os.makedirs(os.path.join(OUT, 'Previews'), exist_ok=True)

    views = [('hero', (9.6, 13.0, 4.8), (0, 0.3, 1.7)),
             ('side', (14.5, 0.5, 3.4), (0, -0.5, 1.7)),
             ('front', (0, 16.5, 3.0), (0, 2.0, 1.7))]
    for name, pos, tgt in views:
        cam.location = pos
        target.location = tgt
        scene.render.filepath = os.path.join(OUT, 'Previews', 'truck_v4_' + name + '.png')
        bpy.ops.render.render(write_still=True)
        print('RENDERED', scene.render.filepath)


def export():
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type == 'MESH' and o.name not in ('Ground',):
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Models', 'Freightliner_Cascadia.fbx'),
                             use_selection=True, axis_forward='-Z', axis_up='Y',
                             global_scale=1.0, apply_scale_options='FBX_SCALE_ALL')
    print('EXPORTED TRUCK V4')


if __name__ == '__main__':
    reset()
    make_truck()
    finalize()
    setup_render()
    export()
