import bpy, bmesh, math, os, sys
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
    if parent:
        ob.parent = parent
    return ob


def body_ring(y, hwb, hwt, z0, z1, corner=.18, steps=5):
    """Rounded-rectangle cross-section with a little tumblehome.

    Bottom width is hwb, top width is hwt. The straight side between the two
    corner arcs gives the cab its inward lean instead of a slab-sided box.
    """
    pts = []
    c = corner
    for i in range(steps + 1):
        a = -math.pi / 2 + (math.pi / 2) * i / steps
        pts.append((hwb - c + math.cos(a) * c, y, z0 + c + math.sin(a) * c))
    for i in range(steps + 1):
        a = (math.pi / 2) * i / steps
        pts.append((hwt - c + math.cos(a) * c, y, z1 - c + math.sin(a) * c))
    for i in range(steps + 1):
        a = math.pi / 2 + (math.pi / 2) * i / steps
        pts.append((-hwt + c + math.cos(a) * c, y, z1 - c + math.sin(a) * c))
    for i in range(steps + 1):
        a = math.pi + (math.pi / 2) * i / steps
        pts.append((-hwb + c + math.cos(a) * c, y, z0 + c + math.sin(a) * c))
    return pts


def prism(name, profile, thickness, loc, material, rot=(0, 0, 0)):
    """Extrude a 2D XZ profile along Y. profile is [(x,z), ...]."""
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
    return ob


def cut(target, cutter):
    m = target.modifiers.new('cut', 'BOOLEAN')
    m.operation = 'DIFFERENCE'
    m.object = cutter
    m.solver = 'EXACT'
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
        'white': mat('Cab_White', (0.84, 0.85, 0.83), 0.18, 0.20),
        'pearl': mat('Cab_Pearl', (0.91, 0.92, 0.89), 0.22, 0.16),
        'chrome': mat('Chrome', (0.84, 0.86, 0.89), 1.0, 0.035),
        'alum': mat('Aluminum', (0.70, 0.72, 0.74), 1.0, 0.20),
        'dark': mat('DarkTrim', (0.018, 0.021, 0.025), 0.20, 0.38),
        'grey': mat('GreyPlastic', (0.13, 0.14, 0.16), 0.05, 0.62),
        'rubber': mat('Rubber', (0.008, 0.009, 0.010), 0.0, 0.93),
        'glass': mat('Glass', (0.018, 0.032, 0.045), 0.55, 0.035, 0.55),
        'wind': mat('WindshieldGlass', (0.025, 0.045, 0.060), 0.45, 0.02, 0.18),
        'blue': mat('BlueAccent', (0.020, 0.075, 0.145), 0.45, 0.24),
        'amber': mat('Amber', (1.0, 0.33, 0.02), 0.0, 0.24, 1.0, (1.0, 0.20, 0.01), 2.6),
        'red': mat('RedLamp', (0.62, 0.012, 0.008), 0.0, 0.24, 1.0, (0.75, 0.008, 0.0), 2.2),
        'head': mat('Headlamp', (0.94, 0.94, 0.90), 0.0, 0.06, 1.0, (0.90, 0.86, 0.70), 2.6),
        'interior': mat('Interior', (0.075, 0.080, 0.085), 0.05, 0.66),
        'fabric': mat('SeatFabric', (0.060, 0.068, 0.075), 0.0, 0.88),
        'stitch': mat('Stitch', (0.30, 0.31, 0.32), 0.0, 0.82),
        'headliner': mat('Headliner', (0.34, 0.35, 0.36), 0.0, 0.90),
        'plate': mat('Plate', (0.86, 0.86, 0.81), 0.0, 0.50),
    }


# ------------------------------------------------------------------ wheels
def define_wheels(root, M):
    positions = [(1.14, 2.90, 'FL'), (-1.14, 2.90, 'FR'),
                 (1.20, -1.45, 'RL1o'), (0.86, -1.45, 'RL1i'),
                 (-1.20, -1.45, 'RR1o'), (-0.86, -1.45, 'RR1i'),
                 (1.20, -2.75, 'RL2o'), (0.86, -2.75, 'RL2i'),
                 (-1.20, -2.75, 'RR2o'), (-0.86, -2.75, 'RR2i')]
    rubber, alum, chrome, dark = M['rubber'], M['alum'], M['chrome'], M['dark']
    for x, y, name in positions:
        # Flat tread band with rounded shoulders, then two sidewall tori.
        cyl('Tread_' + name, 0.532, 0.30, (x, y, 0.532), rubber, (0, math.pi / 2, 0), 40, root)
        for sx in (-1, 1):
            torus('Sidewall_%s_%d' % (name, sx), 0.40, 0.132,
                  (x + sx * 0.142, y, 0.532), rubber, (0, math.pi / 2, 0), root, 40, 12)
        # Tread blocks, rotated around the wheel.
        for i in range(20):
            a = i * math.pi / 10
            box('TreadBlock_%s_%d' % (name, i), 0.302, 0.060, 0.032,
                (x, y + math.sin(a) * 0.530, 0.532 + math.cos(a) * 0.530),
                dark, .004, (a, 0, 0), root)
        # Polished rim with a dished face.
        cyl('Rim_' + name, 0.300, 0.285, (x, y, 0.532), alum, (0, math.pi / 2, 0), 40, root, .006)
        cyl('RimFace_' + name, 0.255, 0.020, (x + 0.150, y, 0.532), chrome, (0, math.pi / 2, 0), 40, root)
        torus('RimLip_' + name, 0.292, 0.018, (x + 0.150, y, 0.532), chrome, (0, math.pi / 2, 0), root, 40, 8)
        cyl('Hub_' + name, 0.130, 0.310, (x + 0.140, y, 0.532), chrome, (0, math.pi / 2, 0), 24, root)
        cyl('HubCap_' + name, 0.070, 0.330, (x + 0.145, y, 0.532), chrome, (0, math.pi / 2, 0), 20, root)
        for i in range(10):
            a = i * math.pi / 5
            cyl('Lug_%s_%d' % (name, i), 0.019, 0.335,
                (x + 0.155, y + math.sin(a) * 0.205, 0.532 + math.cos(a) * 0.205),
                dark, (0, math.pi / 2, 0), 8, root)
        # Brake drum and a dark wheel-well shadow behind the rim.
        cyl('BrakeDrum_' + name, 0.205, 0.18, (x - 0.055, y, 0.532), dark, (0, math.pi / 2, 0), 24, root)


# ------------------------------------------------------------------ truck
def make_truck():
    root = bpy.data.objects.new('TruckRoot', None)
    bpy.context.collection.objects.link(root)
    M = materials()
    white, pearl, chrome, alum = M['white'], M['pearl'], M['chrome'], M['alum']
    dark, grey, rubber, glass = M['dark'], M['grey'], M['rubber'], M['glass']
    wind, blue, amber, red = M['wind'], M['blue'], M['amber'], M['red']
    head, interior, fabric = M['head'], M['interior'], M['fabric']
    stitch, headliner, plate = M['stitch'], M['headliner'], M['plate']

    # ---- chassis -------------------------------------------------
    for x in (-0.55, 0.55):
        box('FrameRail', 0.16, 7.45, 0.26, (x, 0.35, 1.00), dark, .012)
    for y in (-3.15, -2.35, -1.55, -0.75, 0.05, 0.85, 1.65, 2.45, 3.25):
        box('Crossmember', 1.18, 0.12, 0.16, (0, y, 1.00), dark, .005)
    for y in (2.90, -1.45, -2.75):
        cyl('Axle', 0.075, 2.20, (0, y, 0.58), dark, (0, math.pi / 2, 0), 20)
        box('LeafSpring', 1.90, 0.08, 0.08, (0, y, 0.78), grey, .006)
    box('Driveshaft', 0.10, 3.8, 0.10, (0, 0.6, 0.70), alum, .012)

    # ---- cab + sleeper shell ------------------------------------
    # One continuous shell, tapered at the top. The front is cut for the
    # windshield; the left side is cut for the door; the right side and sleeper
    # get window openings. This gives real openings instead of glass decals.
    sections = []
    spec = [(1.45, 1.28, 1.24, 1.10, 3.58),
            (1.25, 1.29, 1.24, 1.10, 3.59),
            (1.00, 1.30, 1.24, 1.10, 3.60),
            (0.60, 1.30, 1.23, 1.10, 3.60),
            (0.10, 1.30, 1.23, 1.10, 3.60),
            (-0.40, 1.29, 1.22, 1.10, 3.59),
            (-0.85, 1.28, 1.21, 1.10, 3.58),
            (-1.12, 1.27, 1.20, 1.10, 3.57),
            (-1.50, 1.26, 1.19, 1.10, 3.55),
            (-2.00, 1.25, 1.18, 1.10, 3.53),
            (-2.50, 1.24, 1.17, 1.10, 3.51),
            (-3.02, 1.22, 1.15, 1.12, 3.49)]
    for y, hwb, hwt, z0, z1 in spec:
        sections.append(body_ring(y, hwb, hwt, z0, z1, .19, 5))
    shell = loft('CabShell', sections, white, root, True, True)
    bevel(shell, .012, 2, 28)

    # Windshield: a trapezoid cut on a 19.5 degree rake.
    rake = math.radians(19.5)
    wprof = [(-1.03, -0.58), (1.03, -0.58), (0.96, 0.58), (-0.96, 0.58)]
    wcut = prism('WSCut', wprof, 0.70, (0, 1.39, 2.90), white, (rake, 0, 0))
    wcut.hide_render = True
    cut(shell, wcut)
    # Door opening on the driver's side.
    cut(shell, cube_cut('DoorCut', (.52, 1.90, 2.06), (-1.29, .34, 2.34), (0, 0, 0), .085))
    # Passenger window.
    cut(shell, cube_cut('WinCut', (.52, 1.58, 0.96), (1.29, .34, 2.87), (0, 0, 0), .105))
    # Sleeper windows.
    for s in (-1, 1):
        cut(shell, cube_cut('SleeperCut', (.52, .88, .44), (s * 1.235, -2.05, 3.06), (0, 0, 0), .065))
    # Rear sleeper window.
    cut(shell, cube_cut('RearCut', (1.76, .52, .38), (0, -3.03, 3.14), (0, 0, 0), .055))
    bevel(shell, .006, 2, 35)

    # Glass in the openings. The windshield is clear so the driver can see out;
    # the side glass is smoked, like a real tinted cab.
    ws = prism('WindshieldGlass', [(-1.045, -0.585), (1.045, -0.585), (0.985, 0.585), (-0.985, 0.585)],
               0.018, (0, 1.375, 2.90), wind, (rake, 0, 0))
    ws.parent = root
    for s in (-1, 1):
        box('SleeperGlass', 0.018, .84, .40, (s * 1.246, -2.05, 3.06), glass, .006)
        box('SleeperGasket', 0.022, .92, .48, (s * 1.238, -2.05, 3.06), dark, .012)
    box('RearGlass', 1.70, .018, .34, (0, -3.045, 3.14), glass, .006)
    box('RearGasket', 1.80, .022, .42, (0, -3.035, 3.14), dark, .012)
    box('PassengerGlass', .018, 1.52, .90, (1.298, .34, 2.87), glass, .006)
    box('PassengerGasket', .022, 1.60, .98, (1.290, .34, 2.87), dark, .012)

    # Windshield surround: A-pillars, header, cowl and visor.
    for s in (-1, 1):
        box('APillar', 0.18, 0.22, 1.26, (s * 1.10, 1.38, 2.90), white, .035, (rake, 0, 0), root)
        box('APillarTrim', 0.07, 0.24, 1.20, (s * 1.045, 1.375, 2.90), dark, .012, (rake, 0, 0), root)
    box('WindshieldHeader', 2.30, 0.24, 0.14, (0, 1.17, 3.48), white, .03, (rake, 0, 0), root)
    box('WindshieldCowl', 2.30, 0.22, 0.11, (0, 1.57, 2.30), dark, .012, (rake, 0, 0), root)
    box('Visor', 2.42, 0.30, 0.07, (0, 1.31, 3.58), white, .02, (math.radians(8), 0, 0), root)
    for s in (-1, 1):
        box('Wiper', 0.74, 0.025, 0.025, (s * 0.52, 1.70, 2.36), dark, .003,
            (rake, 0, math.radians(-7 * s)), root)

    # ---- driver door --------------------------------------------
    hinge = (-1.30, 1.31, 2.34)
    parts = []
    parts.append(box('DoorPanel', 0.075, 1.86, 2.02, (-1.30, 0.34, 2.34), white, .045))
    parts.append(box('DoorWindowGasket', 0.025, 1.48, 0.94, (-1.337, 0.36, 2.89), dark, .018))
    parts.append(box('DoorWindowGlass', 0.018, 1.42, 0.86, (-1.350, 0.36, 2.89), glass, .006))
    parts.append(box('DoorBeltline', 0.028, 1.72, 0.035, (-1.342, 0.34, 2.39), dark, .004))
    parts.append(box('DoorLowerTrim', 0.026, 1.60, 0.028, (-1.342, 0.34, 1.42), dark, .004))
    parts.append(box('DoorHandle', 0.045, 0.20, 0.055, (-1.355, 0.98, 2.25), chrome, .012))
    parts.append(box('DoorBadge', 0.014, 0.62, 0.045, (-1.346, 0.34, 1.92), chrome, .003))
    parts.append(text('DoorWord', 'CASCADIA', 0.075, (-1.355, 0.34, 1.92), dark,
                      normal=(-1, 0, 0), up=(0, 0, 1)))
    # A black rubber seal around the door opening, so the cut edge is not a
    # bright white slab when the door is open.
    for yy, zz, sy, sz in [(1.30, 2.34, 0.045, 2.04), (-0.61, 2.34, 0.045, 2.04),
                           (0.34, 3.37, 1.90, 0.045), (0.34, 1.31, 1.90, 0.045)]:
        box('DoorSeal', 0.045, sy, sz, (-1.285, yy, zz), dark, .008, parent=root)
    door = join(parts, 'DriverDoor')
    bpy.context.scene.cursor.location = hinge
    bpy.context.view_layer.objects.active = door
    door.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    door.select_set(False)
    door.parent = root

    # ---- hood ---------------------------------------------------
    hood_sections = []
    hood_spec = [(1.40, 1.26, 1.23, 1.05, 2.34),
                 (1.75, 1.26, 1.23, 1.04, 2.32),
                 (2.20, 1.25, 1.22, 1.03, 2.28),
                 (2.70, 1.23, 1.20, 1.03, 2.23),
                 (3.20, 1.21, 1.18, 1.02, 2.18),
                 (3.70, 1.18, 1.15, 1.02, 2.12),
                 (4.05, 1.16, 1.13, 1.02, 2.08),
                 (4.18, 1.15, 1.12, 1.02, 2.06)]
    for y, hwb, hwt, z0, z1 in hood_spec:
        hood_sections.append(body_ring(y, hwb, hwt, z0, z1, .17, 4))
    hood = loft('Hood', hood_sections, white, root, True, True)
    bevel(hood, .016, 2, 30)
    # Cowl vent and hood centre seam.
    box('CowlVent', 1.55, 0.08, 0.03, (0, 1.51, 2.325), dark, .006, (rake, 0, 0), root)
    box('HoodSeam', 0.035, 2.40, 0.012, (0, 2.78, 2.205), dark, .003, (math.radians(-2.0), 0, 0), root)
    for s in (-1, 1):
        for i in range(3):
            box('HoodVent', 0.02, 0.50, 0.035, (s * 1.205, 3.00, 1.62 + i * 0.055), dark, .004)
        box('HoodChromeStrip', 0.012, 2.25, 0.022, (s * 1.235, 2.92, 1.40), chrome, .002)

    # ---- fenders / wheel arches ---------------------------------
    for s in (-1, 1):
        # Rounded white lip over the front tire.
        pts = []
        for i in range(17):
            a = math.radians(2 + i * 11.0)
            pts.append((s * 1.325, 2.90 + math.cos(a) * 0.645, 0.55 + math.sin(a) * 0.645))
        tube_curve('FenderLip', pts, 0.050, white, root, 3)
        pts2 = []
        for i in range(15):
            a = math.radians(8 + i * 11.7)
            pts2.append((s * 1.335, 2.90 + math.cos(a) * 0.585, 0.55 + math.sin(a) * 0.585))
        tube_curve('FenderTrim', pts2, 0.022, dark, root, 2)
        box('FenderSide', 0.075, 1.35, 0.36, (s * 1.295, 2.90, 1.22), white, .025)
        box('FenderLowerTrim', 0.085, 1.10, 0.035, (s * 1.30, 2.90, 1.055), dark, .004)
        box('FenderStep', 0.46, 0.40, 0.055, (s * 1.08, 3.58, 0.98), alum, .012)
        box('FenderStepTrim', 0.48, 0.42, 0.018, (s * 1.08, 3.58, 0.95), chrome, .003)
        box('FrontMudFlap', 0.66, 0.05, 0.56, (s * 1.08, 2.18, 0.38), rubber, .006)

    # ---- grille / front -----------------------------------------
    # Chrome surround.
    box('GrilleTop', 1.74, 0.12, 0.085, (0, 4.18, 2.12), chrome, .018)
    box('GrilleBottom', 1.74, 0.12, 0.085, (0, 4.18, 1.10), chrome, .018)
    for s in (-1, 1):
        box('GrilleSide', 0.085, 0.12, 1.10, (s * 0.87, 4.18, 1.61), chrome, .018)
    box('GrilleMesh', 1.63, 0.045, 0.96, (0, 4.19, 1.61), dark, .004)
    for i in range(7):
        box('GrilleBar', 1.64, 0.035, 0.028, (0, 4.215, 1.20 + i * 0.135), chrome, .003)
    for s in (-1, 1):
        box('GrilleDivider', 0.028, 0.035, 0.96, (s * 0.29, 4.215, 1.61), chrome, .003)
    text('FreightlinerBadge', 'FREIGHTLINER', 0.070, (0, 4.225, 2.22), chrome,
         extrude=0.003, parent=root, normal=(0, 1, 0), up=(0, 0, 1))

    # Headlight pods.
    for s in (-1, 1):
        box('HeadlightBezel', 0.44, 0.16, 0.34, (s * 1.02, 4.13, 1.42), chrome, .035,
            (0, 0, math.radians(-6 * s)), root)
        box('HeadlightHousing', 0.38, 0.10, 0.28, (s * 1.02, 4.155, 1.42), dark, .025,
            (0, 0, math.radians(-6 * s)), root)
        cyl('ProjectorRim', 0.105, 0.035, (s * 1.02, 4.22, 1.46), chrome,
            (math.pi / 2, 0, 0), 28, root)
        cyl('ProjectorLens', 0.085, 0.025, (s * 1.02, 4.235, 1.46), head,
            (math.pi / 2, 0, 0), 28, root)
        box('DRL', 0.30, 0.022, 0.038, (s * 1.02, 4.225, 1.31), head, .004,
            (0, 0, math.radians(-6 * s)), root)
        box('TurnSignal', 0.12, 0.030, 0.10, (s * 1.17, 4.18, 1.18), amber, .008,
            (0, 0, math.radians(-6 * s)), root)

    # Bumper.
    box('Bumper', 2.45, 0.30, 0.36, (0, 4.34, 0.82), chrome, .055)
    box('BumperTop', 2.47, 0.10, 0.06, (0, 4.33, 1.02), chrome, .018)
    box('AirDam', 2.24, 0.20, 0.16, (0, 4.36, 0.58), dark, .025)
    box('BumperStep', 0.52, 0.10, 0.06, (0, 4.40, 0.55), dark, .008)
    for s in (-1, 1):
        box('FogLightBezel', 0.18, 0.05, 0.13, (s * 0.82, 4.40, 0.75), dark, .012)
        box('FogLight', 0.12, 0.03, 0.08, (s * 0.82, 4.43, 0.75), head, .006)
        box('TowHook', 0.11, 0.10, 0.08, (s * 0.55, 4.42, 0.44), red, .01)
    box('LicensePlate', 0.50, 0.025, 0.22, (0, 4.45, 0.88), plate, .006)
    text('PlateText', 'TEXAS', 0.095, (0, 4.465, 0.88), dark, extrude=.002,
         parent=root, normal=(0, 1, 0), up=(0, 0, 1))

    # ---- mirrors ------------------------------------------------
    for s in (-1, 1):
        box('MirrorArm', 0.05, 0.06, 0.78, (s * 1.36, 1.52, 2.94), chrome, .012)
        box('MirrorArmTop', 0.34, 0.05, 0.05, (s * 1.52, 1.52, 3.30), chrome, .01)
        box('MirrorHousing', 0.19, 0.28, 0.68, (s * 1.66, 1.52, 2.96), dark, .055)
        box('MirrorGlass', 0.018, 0.22, 0.50, (s * 1.765, 1.52, 3.01), glass, .005)
        box('MirrorConvex', 0.018, 0.18, 0.22, (s * 1.765, 1.52, 2.62), glass, .005)
        box('MirrorTrim', 0.04, 0.29, 0.04, (s * 1.66, 1.52, 2.64), chrome, .006)
        box('HoodMirrorArm', 0.03, 0.22, 0.03, (s * 1.16, 3.64, 2.16), dark, .004)
        box('HoodMirror', 0.11, 0.18, 0.24, (s * 1.29, 3.72, 2.25), dark, .022)
        box('HoodMirrorGlass', 0.018, 0.13, 0.17, (s * 1.355, 3.72, 2.26), glass, .004)

    # ---- steps, tanks, battery ---------------------------------
    for s in (-1, 1):
        for i in range(3):
            box('Step', 0.60, 0.46, 0.055, (s * 1.10, 0.55, 0.46 + i * 0.30), alum, .012)
            box('StepTrim', 0.62, 0.48, 0.015, (s * 1.10, 0.55, 0.435 + i * 0.30), chrome, .003)
        box('StepBracket', 0.06, 0.10, 0.84, (s * 0.82, 0.55, 0.74), dark, .005)
        box('GrabHandle', 0.035, 0.045, 0.62, (s * 1.33, 1.12, 2.60), chrome, .008)
        cyl('FuelTank', 0.365, 1.90, (s * 1.20, -0.62, 0.86), alum, (math.pi / 2, 0, 0), 32, root, .008)
        for yy in (-1.35, -0.62, 0.11):
            box('TankStrap', 0.055, 0.09, 0.75, (s * 1.235, yy, 0.87), chrome, .005)
        cyl('TankCap', 0.115, 0.06, (s * 1.20, -1.60, 0.98), chrome, (0, 0, 0), 20)
    box('BatteryBox', 0.46, 0.80, 0.48, (-1.22, 0.76, 0.68), dark, .02)
    box('BatteryLid', 0.48, 0.82, 0.04, (-1.22, 0.76, 0.94), grey, .01)

    # ---- fifth wheel / rear -------------------------------------
    box('FifthWheelPlate', 1.14, 1.10, 0.10, (0, -1.55, 1.18), grey, .02)
    box('FifthWheelRamp', 1.14, 0.22, 0.16, (0, -1.02, 1.18), grey, .02)
    box('FifthWheelJaw', 0.20, 0.42, 0.16, (0, -1.85, 1.26), dark, .015)
    box('FifthWheelHandle', 1.35, 0.07, 0.07, (0, -2.18, 1.15), chrome, .01)
    for s in (-1, 1):
        box('RearLightBar', 0.16, 0.05, 0.14, (s * 0.90, -3.38, 0.96), red, .01)
        box('MudFlap', 0.68, 0.05, 0.66, (s * 1.05, -3.20, 0.36), rubber, .006)
        box('MudFlapBracket', 0.05, 0.05, 0.55, (s * 0.70, -3.18, 0.64), dark, .005)
    box('RearBumper', 2.30, 0.14, 0.16, (0, -3.45, 0.62), alum, .02)

    # ---- exhaust / roof ----------------------------------------
    for s in (-1, 1):
        cyl('Exhaust', 0.075, 1.85, (s * 1.13, -3.00, 2.20), chrome, (0, 0, 0), 20)
        box('ExhaustShield', 0.24, 0.30, 0.55, (s * 1.13, -3.00, 3.05), chrome, .03)
        cone('ExhaustTip', 0.08, 0.11, 0.12, (s * 1.13, -3.00, 3.18), dark, (0, 0, 0), 20)
    box('RoofLightBar', 1.42, 0.10, 0.07, (0, 1.40, 3.65), dark, .01)
    for i in range(5):
        box('ClearanceLamp', 0.10, 0.04, 0.05, (-0.48 + i * 0.24, 1.45, 3.68), amber, .005)
    for s in (-1, 1):
        cyl('AirHorn', 0.085, 0.62, (s * 0.78, -0.10, 3.78), chrome, (math.pi / 2, 0, 0), 24)
        cyl('HornBracket', 0.04, 0.12, (s * 0.78, 0.15, 3.68), dark, (0, 0, 0), 10)
    cyl('Antenna', 0.012, 0.70, (-0.95, 0.55, 4.10), dark, (0, 0, 0), 8)

    # ---- badges / accent ---------------------------------------
    for s in (-1, 1):
        text('SideBadge', 'CASCADIA', 0.095, (s * 1.315, 0.38, 1.98), chrome,
             extrude=0.003, parent=root, normal=(s, 0, 0), up=(0, 0, 1))
        box('BlueStripe', 0.012, 3.4, 0.055, (s * 1.285, -0.55, 1.28), blue, .002)
    text('HoodBadge', 'CASCADIA', 0.075, (0, 4.20, 2.03), chrome,
         extrude=0.003, parent=root, normal=(0, 1, 0), up=(0, 0, 1))

    # ---- wheels and interior -----------------------------------
    define_wheels(root, M)
    build_interior(root, interior, fabric, stitch, dark, grey, chrome, glass, amber, head, headliner)

    return root


# ------------------------------------------------------------------ interior
def build_interior(root, interior, fabric, stitch, dark, grey, silver, glass, amber, head, headliner):
    box('CabFloor', 2.26, 2.62, 0.09, (0, 0.34, 1.30), interior, .01)
    box('Headliner', 2.30, 2.56, 0.05, (0, 0.34, 3.48), headliner, .01)
    box('RearBulkhead', 2.30, 0.08, 2.04, (0, -1.01, 2.42), interior, .01)
    # sleeper bunk + curtain
    box('BunkBase', 1.70, 1.84, 0.20, (0, -2.02, 1.58), dark, .02)
    box('Mattress', 1.62, 1.72, 0.22, (0, -2.02, 1.78), fabric, .04)
    box('Pillow', 1.10, 0.42, 0.18, (0, -2.62, 1.96), stitch, .06)
    box('SleeperCurtain', 2.08, 0.04, 1.88, (0, -1.03, 2.42), fabric, .01)
    for s in (-1, 1):
        box('CurtainTie', 0.06, 0.06, 0.40, (s * 1.06, -1.06, 2.42), dark, .005)
    # dash
    box('DashBody', 2.28, 0.62, 0.52, (0, 1.32, 1.82), interior, .03)
    box('DashTop', 2.30, 0.66, 0.09, (0, 1.32, 2.04), fabric, .03)
    box('DashLower', 2.26, 0.20, 0.42, (0, 1.10, 1.48), dark, .02)
    box('CenterConsole', 0.56, 0.36, 0.44, (0.28, 1.18, 1.82), grey, .02)
    # instrument binnacle
    box('ClusterHood', 0.64, 0.22, 0.16, (-0.62, 1.12, 2.22), dark, .03)
    box('ClusterFace', 0.54, 0.04, 0.30, (-0.62, 1.02, 2.12), dark, .01)
    for (gx, gz, r) in [(-0.83, 2.12, 0.105), (-0.62, 2.12, 0.115), (-0.41, 2.12, 0.095)]:
        cyl('GaugeBezel', r + 0.018, 0.02, (gx, 0.995, gz), silver, (math.pi / 2, 0, 0), 24)
        cyl('GaugeFace', r, 0.015, (gx, 0.985, gz), mat('GaugeFace', (0.015, 0.020, 0.026), 0, .35), (math.pi / 2, 0, 0), 24)
        cyl('GaugeTicks', r * 0.86, 0.005, (gx, 0.975, gz), mat('GaugeTick', (0.55, 0.55, 0.52), 0, .5), (math.pi / 2, 0, 0), 24)
        box('Needle', 0.012, 0.008, r * 0.82, (gx, 0.968, gz + r * 0.35),
            mat('Needle', (0.95, 0.30, 0.05), 0, .3), .001, (0, 0, math.radians(-25)))
    # steering column + wheel
    cyl('SteeringColumn', 0.05, 0.55, (-0.62, 1.30, 2.16), dark, (math.pi / 2, 0, 0), 16)
    torus('SteeringWheel', 0.30, 0.030, (-0.62, 1.00, 2.24), dark, (math.radians(105), 0, 0), root, 40, 10)
    torus('WheelRim', 0.30, 0.012, (-0.62, 1.005, 2.24), grey, (math.radians(105), 0, 0), root, 40, 8)
    cyl('WheelHub', 0.075, 0.06, (-0.62, 0.99, 2.24), silver, (math.radians(105), 0, 0), 20)
    for a in (0, 120, 240):
        box('Spoke', 0.055, 0.035, 0.25, (-0.62 + math.sin(math.radians(a)) * 0.12,
                                         1.00, 2.24 + math.cos(math.radians(a)) * 0.12),
            dark, .01, (math.radians(105), 0, math.radians(a)))
    # pedals
    box('ThrottlePedal', 0.14, 0.24, 0.03, (-0.78, 1.42, 1.52), dark, .005, (math.radians(20), 0, 0))
    box('BrakePedal', 0.16, 0.26, 0.04, (-1.02, 1.42, 1.54), grey, .005, (math.radians(20), 0, 0))
    # seats
    for s in (-1, 1):
        x = s * 0.62
        box('SeatBase', 0.66, 0.64, 0.20, (x, 0.46, 1.52), dark, .03)
        box('SeatCushion', 0.62, 0.60, 0.19, (x, 0.46, 1.65), fabric, .05)
        box('SeatBack', 0.62, 0.18, 1.05, (x, 0.07, 2.22), fabric, .06)
        box('SeatBolsterL', 0.10, 0.22, 0.82, (x - 0.28, 0.20, 2.22), fabric, .04)
        box('SeatBolsterR', 0.10, 0.22, 0.82, (x + 0.28, 0.20, 2.22), fabric, .04)
        box('Headrest', 0.40, 0.15, 0.30, (x, 0.02, 2.84), fabric, .05)
        box('SeatStitch', 0.015, 0.02, 0.72, (x, 0.165, 2.22), stitch, .002)
        box('SeatArm', 0.10, 0.34, 0.08, (x + s * 0.36, 0.38, 1.96), grey, .02)
    box('EngineHump', 0.46, 0.80, 0.24, (0, 0.72, 1.49), dark, .04)
    box('GearBase', 0.12, 0.14, 0.10, (0.42, 1.08, 1.86), grey, .02)
    cyl('GearStick', 0.02, 0.25, (0.42, 1.08, 2.02), dark, (0, 0, 0), 8)
    cyl('GearKnob', 0.055, 0.08, (0.42, 1.08, 2.16), dark, (0, 0, 0), 12)
    box('OverheadConsole', 0.86, 0.34, 0.16, (0, 0.92, 3.38), dark, .02)
    for i in range(4):
        box('Switch', 0.09, 0.05, 0.03, (-0.24 + i * 0.16, 0.73, 3.29), grey, .004)
    box('CBRadio', 0.30, 0.12, 0.12, (0.30, 1.12, 2.28), dark, .01)
    for s in (-1, 1):
        box('SunVisor', 0.62, 0.035, 0.22, (s * 0.60, 1.25, 3.46), headliner, .02,
            (math.radians(18), 0, 0))
    box('RearViewMirror', 0.30, 0.04, 0.13, (0, 1.12, 3.22), glass, .01)


# ------------------------------------------------------------------ join / export
def finalize():
    """Join static meshes by material to keep the Unity draw call count sane.

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
    # Keep the windshield named for ApplyTruckMaterials.
    for o in bpy.data.objects:
        if o.name.startswith('WindshieldGlass'):
            o.name = 'WindshieldGlass'


def setup_render(prefix='truck_v3'):
    bpy.ops.object.empty_add(location=(0, 0.7, 1.9))
    target = bpy.context.object
    target.name = 'CamTarget'
    bpy.ops.object.camera_add(location=(8.6, 10.8, 5.0))
    cam = bpy.context.object
    con = cam.constraints.new('TRACK_TO')
    con.target = target
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    bpy.context.scene.camera = cam
    cam.data.lens = 62
    bpy.ops.object.light_add(type='SUN', location=(4, -4, 10))
    sun = bpy.context.object
    sun.data.energy = 3.4
    sun.data.color = (1.0, 0.88, 0.74)
    sun.data.angle = math.radians(4)
    sun.rotation_euler = (math.radians(48), math.radians(-8), math.radians(38))
    bpy.ops.object.light_add(type='AREA', location=(-6, -7, 7))
    area = bpy.context.object
    area.data.energy = 900
    area.data.size = 12
    area.data.color = (0.66, 0.78, 1.0)
    area.rotation_euler = (math.radians(45), 0, math.radians(-35))
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, -0.01))
    plane = bpy.context.object
    plane.name = 'Ground'
    plane.data.materials.append(mat('Ground', (0.16, 0.17, 0.18), 0, 0.78))
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.taa_render_samples = 48
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 800
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new('World')
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get('Background')
    if bg:
        bg.inputs[0].default_value = (0.30, 0.40, 0.54, 1)
        bg.inputs[1].default_value = 1.0
    scene.view_settings.look = 'AgX - Medium High Contrast'
    os.makedirs(os.path.join(OUT, 'Previews'), exist_ok=True)
    scene.render.filepath = os.path.join(OUT, 'Previews', prefix + '_hero.png')
    bpy.ops.render.render(write_still=True)


def export():
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type == 'MESH' and o.name not in ('Ground',):
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Models', 'Freightliner_Cascadia.fbx'),
                             use_selection=True, axis_forward='-Z', axis_up='Y',
                             global_scale=1.0, apply_scale_options='FBX_SCALE_ALL')
    print('EXPORTED TRUCK V3')


if __name__ == '__main__':
    reset()
    make_truck()
    finalize()
    setup_render()
    export()
