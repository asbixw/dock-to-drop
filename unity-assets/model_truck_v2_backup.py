import bpy, bmesh, math, os
from mathutils import Vector, Euler, Matrix

OUT = os.path.dirname(os.path.abspath(__file__))
FONT_BOLD = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
FONT_REG = "/System/Library/Fonts/Supplemental/Arial.ttf"

# ---------------------------------------------------------------- helpers
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name, color, metallic=0.0, rough=0.5, alpha=1.0, emission=None):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, alpha)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*color, alpha)
    b.inputs['Metallic'].default_value = metallic
    b.inputs['Roughness'].default_value = rough
    if 'Coat Weight' in b.inputs:
        b.inputs['Coat Weight'].default_value = 0.35
        b.inputs['Coat Roughness'].default_value = 0.08
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    if emission is not None:
        if 'Emission Color' in b.inputs:
            b.inputs['Emission Color'].default_value = (*emission, 1)
            b.inputs['Emission Strength'].default_value = 2.0
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


def bevel(obj, width=.02, segments=3):
    if width <= 0:
        return obj
    m = obj.modifiers.new('bevel', 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
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


def cyl(name, r, d, loc, material, rot=(0, 0, 0), verts=24, parent=None, bev=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    if bev:
        bevel(o, bev, 2)
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def cone(name, r1, r2, d, loc, material, rot=(0, 0, 0), verts=20, parent=None):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=d, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def torus(name, major, minor, loc, material, rot=(0, 0, 0), parent=None, mseg=24, nseg=8):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                     major_segments=mseg, minor_segments=nseg,
                                     location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def text(name, body, size, loc, material, rot=(0, 0, 0), extrude=.006, parent=None, font=FONT_BOLD,
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
    """sections: list of list of (x,y,z) rings, equal length, ordered."""
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


def rounded_ring(y, half_w, z0, z1, corner=.10, steps=3):
    """A rounded rectangle ring in XZ at given y."""
    c = corner
    pts = []
    # bottom right -> top right
    for i in range(steps + 1):
        a = -math.pi / 2 + (math.pi / 2) * i / steps
        pts.append((half_w - c + math.cos(a) * c, y, z0 + c + math.sin(a) * c))
    # top right -> top left
    for i in range(steps + 1):
        a = 0 + (math.pi / 2) * i / steps
        pts.append((half_w - c + math.cos(a) * c, y, z1 - c + math.sin(a) * c))
    # top left -> bottom left
    for i in range(steps + 1):
        a = math.pi / 2 + (math.pi / 2) * i / steps
        pts.append((-half_w + c + math.cos(a) * c, y, z1 - c + math.sin(a) * c))
    # bottom left -> bottom right
    for i in range(steps + 1):
        a = math.pi + (math.pi / 2) * i / steps
        pts.append((-half_w + c + math.cos(a) * c, y, z0 + c + math.sin(a) * c))
    return pts


def tube_curve(name, pts, radius, material, parent=None, res=3):
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    sp = cu.splines.new('POLY')
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        sp.points[i].co = (*p, 1)
    cu.bevel_depth = radius
    cu.bevel_resolution = res
    cu.resolution_u = 2
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(material)
    if parent:
        ob.parent = parent
    return ob


def bool_cut(target, cutters):
    for c in cutters:
        m = target.modifiers.new('cut', 'BOOLEAN')
        m.operation = 'DIFFERENCE'
        m.object = c
        m.solver = 'EXACT'
        apply_mods(target)
    for c in cutters:
        bpy.data.objects.remove(c, do_unlink=True)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.object
    o.name = name
    return o


# ---------------------------------------------------------------- truck
def make_truck():
    root = bpy.data.objects.new('TruckRoot', None)
    bpy.context.collection.objects.link(root)

    white = mat('Cab_White', (0.86, 0.87, 0.85), 0.25, 0.22)
    pearl = mat('Cab_Pearl', (0.93, 0.93, 0.90), 0.30, 0.18)
    silver = mat('Chrome', (0.78, 0.80, 0.82), 1.0, 0.10)
    alum = mat('Aluminum', (0.62, 0.65, 0.68), 0.95, 0.24)
    dark = mat('DarkTrim', (0.025, 0.030, 0.036), 0.25, 0.42)
    grey = mat('GreyPlastic', (0.15, 0.16, 0.18), 0.05, 0.62)
    black = mat('Rubber', (0.010, 0.011, 0.012), 0.0, 0.94)
    glass = mat('Glass', (0.012, 0.026, 0.040), 0.55, 0.06, 0.38)
    blue = mat('BlueAccent', (0.025, 0.085, 0.16), 0.45, 0.28)
    amber = mat('Amber', (1.0, 0.33, 0.02), 0.0, 0.26, 1.0, (1.0, 0.22, 0.01))
    red = mat('RedLamp', (0.62, 0.015, 0.01), 0.0, 0.26, 1.0, (0.75, 0.01, 0.0))
    head = mat('Headlamp', (0.92, 0.92, 0.85), 0.0, 0.08, 1.0, (0.85, 0.82, 0.66))
    interior = mat('Interior', (0.055, 0.060, 0.062), 0.05, 0.72)
    fabric = mat('SeatFabric', (0.075, 0.085, 0.095), 0.0, 0.86)
    stitch = mat('Stitch', (0.20, 0.22, 0.24), 0.0, 0.8)

    # ---- chassis
    for x in (-0.62, 0.62):
        box('FrameRail', 0.16, 7.45, 0.26, (x, 0.35, 0.92), dark, .01)
    for y in (-3.10, -2.35, -1.60, -0.85, -0.10, 0.65, 1.40, 2.15, 2.90, 3.65):
        box('Crossmember', 1.34, 0.12, 0.16, (0, y, 0.92), dark, .005)
    # axles & suspension
    for y in (2.86, -1.55, -2.78):
        cyl('Axle', 0.08, 2.1, (0, y, 0.56), dark, (0, math.pi / 2, 0), 16)
        box('LeafSpring', 1.9, 0.08, 0.08, (0, y, 0.74), grey, .005)
    box('Driveshaft', 0.10, 3.8, 0.10, (0, 0.6, 0.66), alum, .01)

    # ---- cab shell built from panels (clean window openings)
    # front wall with a big windshield opening
    box('CabFrontBottom', 2.54, 0.08, 0.98, (0, 1.79, 1.67), white, .02)
    box('CabFrontTop', 2.54, 0.08, 0.36, (0, 1.79, 3.52), white, .02)
    for s in (-1, 1):
        box('APillar', 0.20, 0.08, 1.30, (s * 1.17, 1.79, 2.75), white, .02)
    # roof and floor
    box('CabRoof', 2.54, 2.95, 0.08, (0, 0.32, 3.68), white, .05)
    box('CabFloorOuter', 2.54, 2.95, 0.08, (0, 0.32, 1.20), white, .03)
    # right side wall with window opening
    box('SideLowerR', 0.08, 2.95, 0.98, (1.27, 0.32, 1.67), white, .02)
    box('SideUpperR', 0.08, 2.95, 0.42, (1.27, 0.32, 3.47), white, .02)
    box('SideFrontPillarR', 0.08, 0.36, 1.30, (1.27, 1.61, 2.75), white, .02)
    box('SideRearPillarR', 0.08, 0.32, 1.30, (1.27, -1.00, 2.75), white, .02)
    box('SideGlassFrameR', 0.02, 2.34, 0.06, (1.295, 0.32, 2.13), dark, .005)
    box('SideGlassR', 0.02, 2.28, 1.18, (1.285, 0.32, 2.76), glass, 0, parent=root)
    # left side: door opening with frame
    box('SideLowerL', 0.08, 2.95, 0.98, (-1.27, 0.32, 1.67), white, .02)
    box('SideUpperL', 0.08, 2.95, 0.42, (-1.27, 0.32, 3.47), white, .02)
    box('SideFrontPillarL', 0.08, 0.36, 1.30, (-1.27, 1.61, 2.75), white, .02)
    box('SideRearPillarL', 0.08, 0.32, 1.30, (-1.27, -1.00, 2.75), white, .02)
    # cab rear bulkhead with a sleeper opening
    box('BulkheadL', 0.62, 0.08, 2.48, (-0.96, -1.13, 2.44), white, .02)
    box('BulkheadR', 0.62, 0.08, 2.48, (0.96, -1.13, 2.44), white, .02)
    box('BulkheadTop', 1.30, 0.08, 0.44, (0, -1.13, 3.46), white, .02)
    box('BulkheadInnerL', 0.62, 0.02, 2.30, (-0.96, -1.16, 2.44), interior, .005)
    box('BulkheadInnerR', 0.62, 0.02, 2.30, (0.96, -1.16, 2.44), interior, .005)
    box('BulkheadInnerTop', 1.30, 0.02, 0.44, (0, -1.16, 3.46), interior, .005)

    # sleeper box (solid; windows sit on the surface)
    box('Sleeper', 2.46, 2.10, 2.18, (0, -2.02, 2.38), white, .09)
    box('SleeperRoof', 2.46, 2.10, 0.08, (0, -2.02, 3.51), white, .03)
    for s in (-1, 1):
        box('SleeperGlassFrame', 0.02, 0.68, 0.64, (s * 1.235, -2.05, 3.02), dark, .005)
        box('SleeperGlass', 0.02, 0.60, 0.56, (s * 1.245, -2.05, 3.02), glass, 0, parent=root)
    box('SleeperRearGlassFrame', 1.9, 0.02, 0.38, (0, -3.08, 3.16), dark, .005)
    box('RearGlass', 1.82, 0.02, 0.30, (0, -3.09, 3.16), glass, 0, parent=root)

    # windshield + rubber gasket
    box('WindshieldGasket', 2.32, 0.04, 1.32, (0, 1.805, 2.75), dark, .015)
    box('Windshield', 2.20, 0.02, 1.20, (0, 1.815, 2.75), glass, 0, parent=root)
    # top visor
    box('Visor', 2.40, 0.26, 0.08, (0, 1.68, 3.42), white, .02)

    # ---- hood loft
    hood_sections = []
    for (y, hw, zb, zt) in [(1.70, 1.25, 1.06, 2.40), (2.15, 1.24, 1.04, 2.37),
                            (2.70, 1.20, 1.02, 2.30), (3.25, 1.13, 1.00, 2.20),
                            (3.80, 1.05, 1.00, 2.10), (4.08, 0.99, 1.02, 2.02)]:
        hood_sections.append(rounded_ring(y, hw, zb, zt, 0.11, 2))
    hood = loft('Hood', hood_sections, white, root)
    bevel(hood, .015, 2)

    # fenders / wheel arches
    for s in (-1, 1):
        # black wheel-arch trim
        pts = []
        for i in range(9):
            a = math.radians(12 + i * 19.5)
            pts.append((s * 1.285, 2.86 + math.cos(a) * 0.60, 0.57 + math.sin(a) * 0.60))
        tube_curve('FenderTrimArch', pts, 0.028, dark, root, 2)
        # fender skirt behind arch
        box('FenderSkirt', 0.06, 0.80, 0.62, (s * 1.27, 3.50, 1.18), white, .02)
        # fender step
        box('FenderStep', 0.44, 0.38, 0.05, (s * 1.08, 3.62, 0.98), dark, .01)
        # headlight
        box('HeadlightBezel', 0.48, 0.10, 0.36, (s * 0.78, 4.12, 1.23), dark, .03)
        box('HeadlightLens', 0.38, 0.04, 0.26, (s * 0.78, 4.17, 1.23), head, .02)
        box('HeadlightSplit', 0.02, 0.02, 0.26, (s * 0.78, 4.19, 1.23), dark, .001)
        box('TurnSignal', 0.12, 0.04, 0.16, (s * 1.04, 4.15, 1.16), amber, .01)
        # hood side vent
        box('HoodVent', 0.03, 0.55, 0.11, (s * 1.16, 3.15, 1.78), dark, .005)

    # grille
    box('GrilleFrame', 1.72, 0.12, 1.00, (0, 4.10, 1.58), silver, .025)
    box('GrilleInset', 1.56, 0.08, 0.86, (0, 4.17, 1.58), dark, .01)
    for i in range(9):
        box('GrilleBar', 0.06, 0.04, 0.78, (-0.64 + i * 0.16, 4.21, 1.58), silver, .002)
    # bumper
    box('Bumper', 2.44, 0.30, 0.38, (0, 4.22, 0.78), silver, .05)
    box('BumperLower', 2.20, 0.22, 0.20, (0, 4.25, 0.53), dark, .03)
    for s in (-1, 1):
        box('FogLightBezel', 0.20, 0.06, 0.14, (s * 0.82, 4.38, 0.58), dark, .01)
        box('FogLight', 0.13, 0.03, 0.09, (s * 0.82, 4.41, 0.58), head, .005)
        box('TowHook', 0.12, 0.12, 0.10, (s * 0.55, 4.36, 0.44), red, .01)
    box('LicensePlate', 0.48, 0.03, 0.22, (0, 4.39, 0.88), mat('Plate', (0.85, 0.85, 0.80), 0, .5), .005)
    text('PlateText', 'TEXAS', 0.10, (0, 4.425, 0.88), dark, parent=root, normal=(0, 1, 0), up=(0, 0, 1))

    # windshield glass + trim
    ws = box('Windshield', 2.16, 0.03, 1.18, (0, 1.57, 2.84), glass, 0)
    ws.rotation_euler = (math.radians(17), 0, 0)
    ws.parent = root
    # side glass
    box('SideGlassR', 0.02, 1.28, 1.06, (1.285, 0.42, 2.78), glass, 0, parent=root)
    box('SideGlassSleeperR', 0.02, 0.60, 0.56, (1.255, -2.05, 3.00), glass, 0, parent=root)
    box('SideGlassSleeperL', 0.02, 0.60, 0.56, (-1.255, -2.05, 3.00), glass, 0, parent=root)
    # rear sleeper window
    box('RearGlass', 2.05, 0.02, 0.28, (0, -3.07, 3.24), glass, 0, parent=root)

    # wipers
    for s in (-1, 1):
        box('Wiper', 0.72, 0.02, 0.025, (s * 0.52, 1.77, 2.30), black, .002,
            (0, 0, math.radians(-6 * s)), root)

    # ---- driver door (hinged at front)
    hinge = (-1.30, 1.30, 2.30)
    parts = []
    parts.append(box('DoorLower', 0.065, 1.80, 0.78, (-1.30, 0.40, 1.72), white, .025))
    parts.append(box('DoorTopRail', 0.065, 1.80, 0.09, (-1.30, 0.40, 3.30), white, .02))
    parts.append(box('DoorFrontStile', 0.065, 0.09, 1.30, (-1.30, 1.24, 2.64), white, .02))
    parts.append(box('DoorRearStile', 0.065, 0.09, 1.30, (-1.30, -0.44, 2.64), white, .02))
    parts.append(box('DoorWindowGlass', 0.02, 1.52, 1.10, (-1.325, 0.40, 2.68), glass, 0))
    parts.append(box('DoorWindowTrim', 0.02, 1.60, 0.05, (-1.33, 0.40, 3.23), dark, .005))
    parts.append(box('DoorCard', 0.025, 1.64, 0.72, (-1.255, 0.40, 1.78), interior, .015))
    parts.append(box('DoorArmrest', 0.10, 0.52, 0.06, (-1.21, 0.42, 1.88), fabric, .015))
    parts.append(box('DoorHandle', 0.07, 0.20, 0.055, (-1.345, 0.92, 2.28), silver, .01))
    parts.append(box('DoorBadge', 0.015, 0.78, 0.06, (-1.34, 0.40, 2.02), silver, .002))
    parts.append(text('DoorWord', 'CASCADIA', 0.11, (-1.35, 0.40, 2.02), dark, normal=(-1, 0, 0), up=(0, 0, 1)))
    door = join(parts, 'DriverDoor')
    # origin at hinge
    bpy.context.scene.cursor.location = hinge
    bpy.context.view_layer.objects.active = door
    door.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    door.select_set(False)
    door.parent = root

    # ---- mirrors (both sides)
    for s in (-1, 1):
        box('MirrorArm', 0.05, 0.06, 0.86, (s * 1.36, 1.52, 2.98), dark, .01)
        box('MirrorArm2', 0.35, 0.05, 0.05, (s * 1.52, 1.52, 3.36), dark, .01)
        box('MirrorHousing', 0.20, 0.30, 0.62, (s * 1.66, 1.54, 3.02), dark, .04)
        box('MirrorGlass', 0.02, 0.24, 0.52, (s * 1.77, 1.54, 3.02), glass, .005)
        # hood mirror
        box('HoodMirrorArm', 0.03, 0.22, 0.03, (s * 1.16, 3.72, 2.20), dark, .005)
        box('HoodMirror', 0.10, 0.16, 0.22, (s * 1.28, 3.78, 2.30), dark, .02)

    # ---- steps, tanks, battery, handles
    for s in (-1, 1):
        for i in range(3):
            box('Step', 0.60, 0.48, 0.06, (s * 1.10, 0.48, 0.48 + i * 0.30), alum, .015)
        box('StepBracket', 0.06, 0.10, 0.80, (s * 0.80, 0.48, 0.72), dark, .005)
        box('GrabHandle', 0.04, 0.05, 0.62, (s * 1.31, 1.10, 2.65), silver, .008)
        # fuel tank
        cyl('FuelTank', 0.37, 1.92, (s * 1.22, -0.62, 0.86), alum, (math.pi / 2, 0, 0), 28, root, .01)
        for yy in (-1.25, -0.65, -0.05):
            box('TankStrap', 0.06, 0.10, 0.76, (s * 1.27, yy, 0.88), dark, .005)
        cyl('TankCap', 0.12, 0.06, (s * 1.22, -1.62, 0.98), silver, (0, 0, 0), 12)
    # battery box (left)
    box('BatteryBox', 0.46, 0.78, 0.48, (-1.22, 0.72, 0.66), dark, .02)
    box('BatteryLid', 0.48, 0.80, 0.04, (-1.22, 0.72, 0.92), grey, .01)

    # ---- fifth wheel
    box('FifthWheelPlate', 1.14, 1.10, 0.10, (0, -1.55, 1.16), grey, .02)
    box('FifthWheelRamp', 1.14, 0.22, 0.16, (0, -1.02, 1.16), grey, .02)
    box('FifthWheelJaw', 0.20, 0.42, 0.16, (0, -1.85, 1.24), dark, .015)
    box('FifthWheelHandle', 1.35, 0.07, 0.07, (0, -2.18, 1.13), silver, .01)

    # ---- rear frame / lights
    for s in (-1, 1):
        box('RearLightBar', 0.16, 0.05, 0.14, (s * 0.90, -3.38, 0.96), red, .01)
        box('MudFlap', 0.68, 0.05, 0.64, (s * 1.04, -3.20, 0.35), black, .005)
        box('MudFlapBracket', 0.05, 0.05, 0.55, (s * 0.68, -3.18, 0.62), dark, .005)
    box('RearBumper', 2.30, 0.14, 0.16, (0, -3.43, 0.62), alum, .02)

    # ---- exhaust stacks
    for s in (-1, 1):
        cyl('Exhaust', 0.075, 1.85, (s * 1.14, -3.00, 2.20), silver, (0, 0, 0), 16)
        box('ExhaustShield', 0.24, 0.30, 0.55, (s * 1.14, -3.00, 3.05), silver, .03)
        cone('ExhaustTip', 0.08, 0.11, 0.12, (s * 1.14, -3.00, 3.18), dark, (0, 0, 0), 16)

    # ---- roof details
    box('RoofLightBar', 1.42, 0.10, 0.07, (0, 1.38, 3.96), dark, .01)
    for i in range(5):
        box('ClearanceLamp', 0.10, 0.04, 0.05, (-0.48 + i * 0.24, 1.43, 3.98), amber, .005)
    for s in (-1, 1):
        cyl('AirHorn', 0.09, 0.62, (s * 0.78, -0.10, 4.02), silver, (math.pi / 2, 0, 0), 18)
        cyl('HornBracket', 0.04, 0.12, (s * 0.78, 0.15, 3.92), dark, (0, 0, 0), 8)
    cyl('Antenna', 0.012, 0.70, (-0.95, 0.55, 4.30), dark, (0, 0, 0), 6)

    # ---- badges
    text('FreightlinerBadge', 'FREIGHTLINER', 0.095, (0, 4.205, 2.07), silver,
         extrude=0.003, parent=root, normal=(0, 1, 0), up=(0, 0, 1))
    text('SideBadgeL', 'CASCADIA', 0.10, (-1.345, 0.42, 2.06), silver,
         extrude=0.003, parent=root, normal=(-1, 0, 0), up=(0, 0, 1))
    text('SideBadgeR', 'CASCADIA', 0.10, (1.345, 0.42, 2.06), silver,
         extrude=0.003, parent=root, normal=(1, 0, 0), up=(0, 0, 1))
    # blue accent stripe along sleeper and cab
    for s in (-1, 1):
        box('BlueStripe', 0.015, 4.2, 0.10, (s * 1.272, -0.70, 1.42), blue, .002)

    # ---- wheels
    define_wheels(root, black, silver, dark, alum)

    # ---- cab interior
    build_interior(root, interior, fabric, stitch, dark, grey, silver, glass, amber, head)

    return root


def define_wheels(root, rubber, silver, dark, alum):
    positions = [(1.12, 2.86, 'FL'), (-1.12, 2.86, 'FR'),
                 (1.12, -1.55, 'RL1'), (-1.12, -1.55, 'RR1'),
                 (1.12, -2.78, 'RL2'), (-1.12, -2.78, 'RR2')]
    for x, y, name in positions:
        # tire (open torus so the rim is visible from the side)
        torus('Tire_' + name, 0.400, 0.150, (x, y, 0.56), rubber,
              (0, math.pi / 2, 0), root, 28, 10)
        # aluminum rim
        cyl('Rim_' + name, 0.295, 0.34, (x, y, 0.56), alum, (0, math.pi / 2, 0), 24, root, .01)
        cyl('RimLip_' + name, 0.305, 0.035, (x, y, 0.56), silver, (0, math.pi / 2, 0), 24)
        cyl('Hub_' + name, 0.185, 0.36, (x, y, 0.56), silver, (0, math.pi / 2, 0), 20, root)
        cyl('HubCap_' + name, 0.078, 0.385, (x, y, 0.56), silver, (0, math.pi / 2, 0), 16, root)
        for i in range(10):
            a = i * math.pi / 5
            cyl('Lug_%s_%d' % (name, i), 0.020, 0.385,
                (x, y + math.sin(a) * 0.235, 0.56 + math.cos(a) * 0.235),
                dark, (0, math.pi / 2, 0), 6)
        # subtle tread blocks
        for i in range(16):
            a = i * math.pi / 8
            box('Tread', 0.18, 0.035, 0.035,
                (x, y + math.sin(a) * 0.532, 0.56 + math.cos(a) * 0.532),
                dark, .003, (a, 0, 0))
        # brake drum behind the rim
        cyl('BrakeDrum_' + name, 0.205, 0.16, (x, y, 0.56), dark, (0, math.pi / 2, 0), 16)
        # fender trim
        box('FenderTrim', 0.06, 0.88, 0.06, (x * 1.19, y, 1.10), dark, .005)


def build_interior(root, interior, fabric, stitch, dark, grey, silver, glass, amber, head):
    # floor + headliner
    box('CabFloor', 2.26, 2.62, 0.09, (0, 0.34, 1.36), interior, .01)
    box('Headliner', 2.30, 2.56, 0.05, (0, 0.34, 3.46), fabric, .01)
    box('RearBulkhead', 2.30, 0.08, 2.04, (0, -0.99, 2.40), interior, .01)
    # sleeper bunk + curtain
    box('BunkBase', 1.70, 1.84, 0.20, (0, -2.02, 1.58), dark, .02)
    box('Mattress', 1.62, 1.72, 0.22, (0, -2.02, 1.78), fabric, .04)
    box('Pillow', 1.10, 0.42, 0.18, (0, -2.62, 1.96), stitch, .06)
    box('SleeperCurtain', 2.08, 0.04, 1.88, (0, -1.02, 2.42), fabric, .01)
    for s in (-1, 1):
        box('CurtainTie', 0.06, 0.06, 0.40, (s * 1.06, -1.05, 2.42), dark, .005)
    # dash
    box('DashBody', 2.28, 0.62, 0.52, (0, 1.32, 1.78), interior, .03)
    box('DashTop', 2.30, 0.66, 0.09, (0, 1.32, 1.98), fabric, .03)
    box('DashLower', 2.26, 0.20, 0.42, (0, 1.10, 1.44), dark, .02)
    box('CenterConsole', 0.56, 0.36, 0.44, (0.28, 1.18, 1.78), grey, .02)
    # instrument binnacle
    box('ClusterHood', 0.64, 0.22, 0.16, (-0.62, 1.12, 2.16), dark, .03)
    box('ClusterFace', 0.54, 0.04, 0.30, (-0.62, 1.02, 2.06), dark, .01)
    for (gx, gz, r) in [(-0.83, 2.06, 0.105), (-0.62, 2.06, 0.115), (-0.41, 2.06, 0.095)]:
        cyl('GaugeBezel', r + 0.018, 0.02, (gx, 0.995, gz), silver, (math.pi / 2, 0, 0), 24)
        cyl('GaugeFace', r, 0.015, (gx, 0.985, gz), mat('GaugeFace', (0.02, 0.025, 0.03), 0, .4), (math.pi / 2, 0, 0), 24)
        cyl('GaugeTicks', r * 0.86, 0.005, (gx, 0.975, gz), mat('GaugeTick', (0.55, 0.55, 0.52), 0, .5), (math.pi / 2, 0, 0), 24)
        # needle
        box('Needle', 0.012, 0.008, r * 0.82, (gx, 0.968, gz + r * 0.35), mat('Needle', (0.95, 0.30, 0.05), 0, .3), .001,
            (0, 0, math.radians(-25)))
    # steering column + wheel
    cyl('SteeringColumn', 0.05, 0.55, (-0.62, 1.30, 2.10), dark, (math.pi / 2, 0, 0), 12)
    wheel = torus('SteeringWheel', 0.30, 0.030, (-0.62, 1.00, 2.18), dark,
                  (math.radians(105), 0, 0), root, 32, 8)
    torus('WheelRim', 0.30, 0.012, (-0.62, 1.005, 2.18), grey,
          (math.radians(105), 0, 0), root, 32, 6)
    cyl('WheelHub', 0.075, 0.06, (-0.62, 0.99, 2.18), silver, (math.radians(105), 0, 0), 16)
    for a in (0, 120, 240):
        box('Spoke', 0.055, 0.035, 0.25, (-0.62 + math.sin(math.radians(a)) * 0.12,
                                         1.00, 2.18 + math.cos(math.radians(a)) * 0.12),
            dark, .01, (math.radians(105), 0, math.radians(a)))
    # pedals
    box('ThrottlePedal', 0.14, 0.24, 0.03, (-0.78, 1.42, 1.48), dark, .005, (math.radians(20), 0, 0))
    box('BrakePedal', 0.16, 0.26, 0.04, (-1.02, 1.42, 1.50), grey, .005, (math.radians(20), 0, 0))
    # seats
    for s in (-1, 1):
        x = s * 0.62
        box('SeatBase', 0.66, 0.64, 0.20, (x, 0.46, 1.48), dark, .03)
        box('SeatCushion', 0.62, 0.60, 0.19, (x, 0.46, 1.61), fabric, .05)
        box('SeatBack', 0.62, 0.18, 1.05, (x, 0.07, 2.18), fabric, .06)
        box('SeatBolsterL', 0.10, 0.22, 0.82, (x - 0.28, 0.20, 2.18), fabric, .04)
        box('SeatBolsterR', 0.10, 0.22, 0.82, (x + 0.28, 0.20, 2.18), fabric, .04)
        box('Headrest', 0.40, 0.15, 0.30, (x, 0.02, 2.80), fabric, .05)
        box('SeatStitch', 0.015, 0.02, 0.72, (x, 0.165, 2.18), stitch, .002)
        box('SeatArm', 0.10, 0.34, 0.08, (x + s * 0.36, 0.38, 1.92), grey, .02)
    # center hump
    box('EngineHump', 0.46, 0.80, 0.24, (0, 0.72, 1.45), dark, .04)
    # gear selector
    box('GearBase', 0.12, 0.14, 0.10, (0.42, 1.08, 1.82), grey, .02)
    cyl('GearStick', 0.02, 0.25, (0.42, 1.08, 1.98), dark, (0, 0, 0), 8)
    cyl('GearKnob', 0.055, 0.08, (0.42, 1.08, 2.12), dark, (0, 0, 0), 12)
    # overhead console
    box('OverheadConsole', 0.86, 0.34, 0.16, (0, 0.92, 3.36), dark, .02)
    for i in range(4):
        box('Switch', 0.09, 0.05, 0.03, (-0.24 + i * 0.16, 0.73, 3.27), grey, .004)
    # CB radio
    box('CBRadio', 0.30, 0.12, 0.12, (0.30, 1.12, 2.22), dark, .01)
    # sun visors
    for s in (-1, 1):
        box('SunVisor', 0.92, 0.05, 0.22, (s * 0.58, 1.28, 3.38), fabric, .02,
            (math.radians(-10), 0, 0))
    # mirror inside
    box('RearViewMirror', 0.30, 0.04, 0.13, (0, 1.12, 3.20), glass, .01)


# ---------------------------------------------------------------- render + export
def setup_render(prefix='truck_v2'):
    bpy.ops.object.empty_add(location=(0, 0.7, 1.9))
    target = bpy.context.object
    target.name = 'CamTarget'
    bpy.ops.object.camera_add(location=(8.5, 10.5, 5.6))
    cam = bpy.context.object
    con = cam.constraints.new('TRACK_TO')
    con.target = target
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    bpy.context.scene.camera = cam
    cam.data.lens = 58
    bpy.ops.object.light_add(type='SUN', location=(4, -4, 10))
    sun = bpy.context.object
    sun.data.energy = 3.6
    sun.data.color = (1.0, 0.86, 0.72)
    sun.rotation_euler = (math.radians(52), math.radians(-8), math.radians(38))
    bpy.ops.object.light_add(type='AREA', location=(-5, -6, 6))
    area = bpy.context.object
    area.data.energy = 900
    area.data.size = 10
    area.data.color = (0.75, 0.85, 1.0)
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
    plane = bpy.context.object
    plane.data.materials.append(mat('Ground', (0.11, 0.12, 0.13), 0, 0.8))
    bpy.context.scene.render.engine = 'BLENDER_EEVEE'
    bpy.context.scene.eevee.taa_render_samples = 48
    bpy.context.scene.render.resolution_x = 1200
    bpy.context.scene.render.resolution_y = 800
    bpy.context.scene.render.film_transparent = False
    if bpy.context.scene.world is None:
        bpy.context.scene.world = bpy.data.worlds.new('World')
    bpy.context.scene.world.use_nodes = True
    bg = bpy.context.scene.world.node_tree.nodes.get('Background')
    if bg:
        bg.inputs[0].default_value = (0.035, 0.045, 0.055, 1)
        bg.inputs[1].default_value = 1.0
    bpy.context.scene.view_settings.look = 'AgX - Medium High Contrast'
    bpy.context.scene.render.filepath = os.path.join(OUT, 'Previews', prefix + '_hero.png')
    bpy.ops.render.render(write_still=True)
    # rear three-quarter
    cam.location = (8.2, -10.0, 5.2)
    bpy.context.scene.render.filepath = os.path.join(OUT, 'Previews', prefix + '_rear.png')
    bpy.ops.render.render(write_still=True)
    # driver view
    cam.constraints.remove(con)
    cam.location = (-0.62, 0.40, 2.68)
    cam.rotation_euler = (math.radians(90), 0, 0)
    cam.data.lens = 24
    bpy.context.scene.render.filepath = os.path.join(OUT, 'Previews', prefix + '_cab.png')
    bpy.ops.render.render(write_still=True)
    # front view
    cam.location = (0, 11.5, 2.3)
    con = cam.constraints.new('TRACK_TO')
    con.target = target
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    cam.data.lens = 55
    bpy.context.scene.render.filepath = os.path.join(OUT, 'Previews', prefix + '_front.png')
    bpy.ops.render.render(write_still=True)


def export():
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type == 'MESH' and o.name not in ('Plane',):
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Models', 'Freightliner_Cascadia.fbx'),
                             use_selection=True, axis_forward='-Z', axis_up='Y',
                             global_scale=1.0, apply_scale_options='FBX_SCALE_ALL')
    print('EXPORTED TRUCK V2')


if __name__ == '__main__':
    reset()
    make_truck()
    setup_render()
    export()
