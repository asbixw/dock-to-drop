import bpy, math, os
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))
FONT = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"


def apply(o):
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    for m in list(o.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except Exception:
            pass
    o.select_set(False)


def mat(name, color, metal=0, rough=.5, alpha=1, emit=None):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, alpha)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*color, alpha)
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1)
        b.inputs['Emission Strength'].default_value = 1.4
    return m


def box(name, sx, sy, sz, loc, material, bevel=.02, rot=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        md = o.modifiers.new('b', 'BEVEL')
        md.width = bevel
        md.segments = 2
        apply(o)
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def cyl(name, r, d, loc, material, rot=(0, 0, 0), verts=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=d, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def torus(name, major, minor, loc, material, rot=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=28,
                                     minor_segments=10, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def text(name, body, size, loc, material, rot=(0, 0, 0), extrude=.004, parent=None, normal=None, up=(0, 0, 1)):
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body = body
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.size = size
    cu.extrude = extrude
    if os.path.exists(FONT):
        cu.font = bpy.data.fonts.load(FONT, check_existing=True)
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    if normal is not None:
        from mathutils import Matrix
        n = Vector(normal).normalized()
        u = Vector(up).normalized()
        r = u.cross(n).normalized()
        ob.matrix_world = Matrix.Translation(Vector(loc)) @ Matrix((r, u, n)).transposed().to_4x4()
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


def make():
    root = bpy.data.objects.new('ContainerRoot', None)
    bpy.context.collection.objects.link(root)

    white = mat('Box_White', (0.70, 0.72, 0.73), 0.15, 0.48)
    rib = mat('Box_Rib', (0.62, 0.64, 0.65), 0.12, 0.52)
    navy = mat('Box_Navy', (0.020, 0.055, 0.10), 0.35, 0.34)
    blue = mat('Box_Blue', (0.035, 0.12, 0.22), 0.35, 0.32)
    steel = mat('Steel', (0.42, 0.45, 0.48), 0.85, 0.30)
    chrome = mat('Chrome', (0.76, 0.78, 0.80), 1.0, 0.12)
    dark = mat('Dark', (0.035, 0.040, 0.045), 0.2, 0.55)
    black = mat('Rubber', (0.010, 0.011, 0.012), 0, 0.92)
    red = mat('RedLamp', (0.70, 0.015, 0.008), 0, 0.3, 1, (0.7, 0.01, 0.0))
    amber = mat('Amber', (1.0, 0.34, 0.02), 0, 0.3, 1, (1.0, 0.2, 0.0))
    yellow = mat('SealYellow', (0.92, 0.62, 0.05), 0.2, 0.35)
    tape = mat('Reflective', (0.95, 0.95, 0.90), 0.1, 0.25, 1, (0.7, 0.7, 0.65))
    redtape = mat('ReflectiveRed', (0.75, 0.04, 0.02), 0.1, 0.3, 1, (0.55, 0.01, 0.0))

    # chassis
    for x in (-0.86, 0.86):
        box('ChassisRail', 0.16, 16.2, 0.24, (x, 0, 0.98), dark, .01)
    for y in range(-7, 8, 1):
        box('Crossmember', 1.9, 0.10, 0.14, (0, y, 0.98), dark, .005)
    box('KingpinPlate', 1.55, 0.85, 0.10, (0, 8.00, 1.12), steel, .02)
    cyl('Kingpin', 0.09, 0.30, (0, 8.00, 0.92), chrome, (0, 0, 0), 16, root)
    # gooseneck / front deck
    box('FrontDeck', 2.2, 1.4, 0.10, (0, 7.55, 1.14), dark, .02)

    # container shell
    L = 16.15
    W = 2.58
    H = 2.90
    Z = 2.72
    for s in (-1, 1):
        box('SideWall', 0.10, L, H, (s * W / 2, 0, Z), white, .01)
        box('SideRailTop', 0.17, L, 0.14, (s * W / 2, 0, Z + H / 2), rib, .01)
        box('SideRailBot', 0.17, L, 0.16, (s * W / 2, 0, Z - H / 2), navy, .01)
        # corrugation ribs
        for i in range(27):
            y = -L / 2 + 0.48 + i * 0.585
            box('Rib', 0.085, 0.075, H - 0.20, (s * (W / 2 + 0.082), y, Z), rib, .006)
        # corner posts
        for y in (-L / 2, L / 2):
            box('CornerPost', 0.17, 0.19, H, (s * W / 2, y, Z), navy, .01)
        # forklift placard
        box('Placard', 0.02, 0.30, 0.42, (s * (W / 2 + 0.13), -4.2, Z + 0.2), navy, .005)
    # roof / floor / front
    box('Roof', W, L, 0.12, (0, 0, Z + H / 2), white, .01)
    box('RoofRib1', W - 0.25, 0.10, 0.05, (0, -4.2, Z + H / 2 + 0.07), rib, .005)
    box('RoofRib2', W - 0.25, 0.10, 0.05, (0, 0.2, Z + H / 2 + 0.07), rib, .005)
    box('RoofRib3', W - 0.25, 0.10, 0.05, (0, 4.4, Z + H / 2 + 0.07), rib, .005)
    box('Floor', W, L, 0.18, (0, 0, Z - H / 2), dark, .01)
    box('FrontWall', W, H, 0.12, (0, L / 2, Z), white, .01)
    # rear frame
    for x in (-W / 2, W / 2):
        box('RearPost', 0.17, 0.19, H, (x, -L / 2, Z), navy, .01)
    box('RearHeader', W, 0.20, 0.18, (0, -L / 2, Z + H / 2), navy, .01)
    box('RearSill', W, 0.20, 0.18, (0, -L / 2, Z - H / 2), dark, .01)
    # rear doors
    door_w = 1.22
    door_h = 2.74
    for s in (-1, 1):
        hinge = (s * (W / 2 - 0.04), -L / 2 - 0.03, Z)
        door = box('Door_' + ('L' if s < 0 else 'R'), door_w, 0.12, door_h,
                   (hinge[0] - s * door_w / 2, hinge[1], hinge[2]), white, .01)
        bpy.context.scene.cursor.location = hinge
        bpy.context.view_layer.objects.active = door
        door.select_set(True)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        door.select_set(False)
        # door ribs
        for k in range(4):
            box('DoorRib', door_w - 0.18, 0.02, 0.23,
                (hinge[0] - s * door_w / 2, hinge[1] - 0.075, Z - 1.0 + k * 0.66), rib, .005)
        # lock rods
        for k in (-0.38, 0.30):
            xr = hinge[0] - s * door_w / 2 + s * k
            cyl('LockRod', 0.026, 2.46, (xr, hinge[1] - 0.12, hinge[2]), chrome, (0, 0, 0), 10, root)
            box('RodHandle', 0.20, 0.05, 0.06, (xr, hinge[1] - 0.17, hinge[2] - 0.52), chrome, .005)
            cyl('RodGuide', 0.04, 0.03, (xr, hinge[1] - 0.12, hinge[2] + 1.15), dark, (0, 0, 0), 10)
        for zz in (-0.85, 0.85):
            box('Hinge', 0.10, 0.18, 0.13, (s * (W / 2 + 0.02), -L / 2 - 0.08, hinge[2] + zz), steel, .005)
    # seal tag (right door)
    box('SealTag', 0.14, 0.035, 0.20, (0.60, -L / 2 - 0.20, Z - 0.06), yellow, .004)
    text('SealNumber', '482913', 0.085, (0.60, -L / 2 - 0.228, Z - 0.06), dark,
         parent=root, normal=(0, -1, 0), up=(0, 0, 1))
    # rear bumper / underride / lights
    box('Underride', 2.22, 0.12, 0.16, (0, -L / 2 - 0.62, 0.62), dark, .01)
    for x in (-1.0, 1.0):
        box('UnderridePost', 0.12, 0.12, 0.55, (x, -L / 2 - 0.58, 0.88), dark, .01)
        box('TailLightBezel', 0.22, 0.06, 0.18, (x, -L / 2 - 0.12, 0.86), dark, .005)
        box('TailLight', 0.14, 0.04, 0.11, (x, -L / 2 - 0.16, 0.86), red, .005)
        box('MarkerLamp', 0.10, 0.04, 0.06, (x, -L / 2 - 0.14, 2.02), amber, .005)
    box('LicensePlate', 0.42, 0.03, 0.20, (0, -L / 2 - 0.16, 1.20), white, .005)
    # reflective tape
    for s in (-1, 1):
        box('ReflectiveStrip', 0.01, L - 0.6, 0.10, (s * (W / 2 + 0.134), 0, Z - H / 2 + 0.22), tape, .002)
        for i in range(7):
            box('RedTape', 0.012, 0.44, 0.10, (s * (W / 2 + 0.142), -L / 2 + 1.0 + i * 2.1, Z + H / 2 - 0.22), redtape, .002)

    # landing gear
    for s in (-1, 1):
        box('LandingLeg', 0.14, 0.14, 2.02, (s * 0.82, 5.1, 1.10), steel, .01)
        box('LandingFoot', 0.52, 0.46, 0.10, (s * 0.82, 5.1, 0.10), dark, .01)
        box('LandingBrace', 0.06, 0.06, 0.60, (s * 0.82, 5.35, 0.52), steel, .005)
    box('LandingCross', 1.72, 0.12, 0.12, (0, 5.1, 2.02), steel, .01)
    box('LandingCrank', 0.40, 0.06, 0.06, (1.12, 5.1, 1.55), steel, .005)

    # wheels (rear axle)
    for y in (-5.15, -6.28):
        for s in (-1, 1):
            x = s * 1.02
            torus('Wheel', 0.40, 0.15, (x, y, 0.56), black, (0, math.pi / 2, 0), root)
            cyl('WheelRim', 0.29, 0.34, (x, y, 0.56), steel, (0, math.pi / 2, 0), 24, root)
            cyl('WheelHub', 0.175, 0.36, (x, y, 0.56), chrome, (0, math.pi / 2, 0), 20, root)
            for i in range(10):
                a = i * math.pi / 5
                cyl('Lug', 0.018, 0.38, (x, y + math.sin(a) * 0.225, 0.56 + math.cos(a) * 0.225),
                    dark, (0, math.pi / 2, 0), 6)
            box('MudFlap', 0.55, 0.04, 0.55, (x, y - 0.75, 0.32), black, .005)
    # axle & suspension
    for y in (-5.15, -6.28):
        cyl('Axle', 0.08, 2.0, (0, y, 0.56), dark, (0, math.pi / 2, 0), 16)
        box('Spring', 1.9, 0.08, 0.08, (0, y, 0.76), steel, .005)

    # A-14 plate on the nose
    box('UnitPlate', 0.42, 0.03, 0.20, (0.8, L / 2 + 0.07, 2.10), white, .005)
    text('UnitText', 'A-14', 0.13, (0.8, L / 2 + 0.09, 2.10), navy,
         parent=root, normal=(0, 1, 0), up=(0, 0, 1))

    return root


def preview():
    import bpy, math, os
    bpy.ops.object.empty_add(location=(0, 0, 2.2))
    target = bpy.context.object
    bpy.ops.object.camera_add(location=(9, -16, 7))
    cam = bpy.context.object
    con = cam.constraints.new('TRACK_TO')
    con.target = target
    con.track_axis = 'TRACK_NEGATIVE_Z'
    con.up_axis = 'UP_Y'
    cam.data.lens = 50
    bpy.context.scene.camera = cam
    bpy.ops.object.light_add(type='SUN', location=(4, -4, 10))
    sun = bpy.context.object
    sun.data.energy = 3.4
    sun.data.color = (1.0, 0.88, 0.75)
    sun.rotation_euler = (math.radians(50), math.radians(-10), math.radians(35))
    bpy.ops.object.light_add(type='AREA', location=(-6, -8, 7))
    area = bpy.context.object
    area.data.energy = 800
    area.data.size = 12
    bpy.ops.mesh.primitive_plane_add(size=50, location=(0, 0, 0))
    bpy.context.object.data.materials.append(mat('Ground', (0.10, 0.11, 0.12), 0, 0.85))
    bpy.context.scene.render.engine = 'BLENDER_EEVEE'
    bpy.context.scene.eevee.taa_render_samples = 32
    bpy.context.scene.render.resolution_x = 1200
    bpy.context.scene.render.resolution_y = 700
    if bpy.context.scene.world is None:
        bpy.context.scene.world = bpy.data.worlds.new('World')
    bpy.context.scene.world.use_nodes = True
    bg = bpy.context.scene.world.node_tree.nodes.get('Background')
    if bg:
        bg.inputs[0].default_value = (0.035, 0.045, 0.055, 1)
    bpy.context.scene.view_settings.look = 'AgX - Medium High Contrast'
    bpy.context.scene.render.filepath = os.path.join(OUT, 'Previews', 'container_v2_hero.png')
    bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make()
    preview()
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Models', 'Amazon_53ft_Container.fbx'),
                             use_selection=True, axis_forward='-Z', axis_up='Y',
                             global_scale=1.0, apply_scale_options='FBX_SCALE_ALL')
    print('EXPORTED CONTAINER V2')
