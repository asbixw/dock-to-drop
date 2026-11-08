import bpy, bmesh, math, os
from mathutils import Vector

OUT = os.path.dirname(os.path.abspath(__file__))

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(name, color, metallic=0.0, rough=0.5, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, alpha)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, alpha)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = rough
    if alpha < 1:
        bsdf.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    return m

def apply_mods(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for m in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except Exception:
            pass
    obj.select_set(False)

def box(name, sx, sy, sz, loc, material, bevel=0.02, rot=(0,0,0), parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = (sx, sy, sz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        b = o.modifiers.new('bevel', 'BEVEL')
        b.width = bevel
        b.segments = 3
        b.limit_method = 'ANGLE'
        apply_mods(o)
    o.data.materials.append(material)
    if parent: o.parent = parent
    return o

def cyl(name, radius, depth, loc, material, rot=(0,0,0), verts=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent: o.parent = parent
    return o

def sphere(name, radius, loc, material, parent=None, seg=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=8, radius=radius, location=loc)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if parent: o.parent = parent
    return o

def text(name, body, size, loc, material, rot=(0,0,0), extrude=0.006, parent=None):
    cu = bpy.data.curves.new(name, type='FONT')
    cu.body = body
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.size = size
    cu.extrude = extrude
    ob = bpy.data.objects.new(name, cu)
    bpy.context.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    ob.data.materials.append(material)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob.select_set(False)
    if parent: ob.parent = parent
    return ob

def make_truck():
    root = bpy.data.objects.new('TruckRoot', None)
    bpy.context.collection.objects.link(root)
    white = mat('Cab_White', (0.82,0.84,0.86), 0.15, 0.28)
    silver = mat('Chrome', (0.72,0.76,0.80), 1.0, 0.14)
    dark = mat('DarkTrim', (0.035,0.045,0.055), 0.2, 0.5)
    black = mat('Rubber', (0.012,0.014,0.016), 0.0, 0.92)
    glass = mat('Glass', (0.02,0.045,0.065), 0.4, 0.08, 0.72)
    blue = mat('BlueAccent', (0.04,0.12,0.22), 0.5, 0.3)
    amber = mat('Amber', (1.0,0.35,0.03), 0.0, 0.3)
    red = mat('RedLamp', (0.8,0.02,0.01), 0.0, 0.3)
    head = mat('Headlamp', (1.0,0.95,0.78), 0.0, 0.15)
    grey = mat('GreyPlastic', (0.18,0.2,0.22), 0.1, 0.6)

    # Chassis
    for x in (-0.62, 0.62):
        box('FrameRail', 0.16, 7.0, 0.24, (x, 0.0, 0.92), dark)
    for y in (-2.8,-1.8,-0.8,0.2,1.2,2.2,3.2,4.0):
        box('Crossmember', 1.35, 0.12, 0.14, (0, y, 0.92), dark)

    # Cab / sleeper
    box('Cab', 2.42, 2.1, 2.12, (0, 0.75, 2.18), white, 0.10)
    box('Sleeper', 2.35, 1.65, 1.98, (0, -0.95, 2.16), white, 0.10)
    box('RoofFairing', 2.3, 1.7, 0.34, (0, 0.65, 3.38), white, 0.08)
    box('SleeperRoof', 2.22, 1.55, 0.22, (0, -0.95, 3.28), white, 0.08)

    # Hood: tapered loft
    verts=[]; faces=[]
    sections = [(2.05, 2.42, 1.06, 2.55), (3.05, 2.28, 1.02, 2.26), (3.95, 2.05, 0.98, 1.95), (4.22, 1.92, 0.96, 1.82)]
    # 8 points beveled rectangle per section: (x,z)
    for y,w,zb,zt in sections:
        h=zt-zb; cx=w/2
        # points around cross-section (x,z), starting bottom-left
        pts=[(-cx+0.06,zb),(-cx,zb+0.08),(cx-0.06,zb),(cx,zb+0.08),(cx,zt-0.10),(cx-0.08,zt),(cx-0.06,zt),(cx,zt-0.10)]
        # use simpler 8 ordered
        pts=[(-cx+0.05,zb), (cx-0.05,zb), (cx,zb+0.08), (cx,zt-0.10), (cx-0.06,zt), (-cx+0.06,zt), (-cx,zt-0.10), (-cx,zb+0.08)]
        for x,z in pts:
            verts.append((x,y,z))
    n=8
    for i in range(len(sections)-1):
        for j in range(n):
            a=i*n+j; b=i*n+(j+1)%n; c=(i+1)*n+(j+1)%n; d=(i+1)*n+j
            faces.append((a,b,c,d))
    faces.append(tuple(range(n-1,-1,-1)))
    faces.append(tuple(range((len(sections)-1)*n, len(sections)*n)))
    me=bpy.data.meshes.new('HoodMesh'); me.from_pydata(verts, [], faces); me.update()
    hood=bpy.data.objects.new('Hood', me); bpy.context.collection.objects.link(hood)
    hood.data.materials.append(white); hood.parent=root
    bpy.context.view_layer.objects.active=hood; hood.select_set(True)
    bpy.ops.object.shade_smooth(); hood.select_set(False)

    # Fenders and bumper
    for s in (-1,1):
        box('FrontFender', 0.55, 1.55, 0.5, (s*1.13, 2.85, 1.18), white, 0.08)
        box('FenderTrim', 0.12, 1.4, 0.12, (s*1.42, 2.85, 1.02), dark, 0.02)
    box('Bumper', 2.35, 0.28, 0.34, (0, 4.31, 0.78), silver, 0.03)
    box('BumperLower', 2.0, 0.18, 0.18, (0, 4.34, 0.55), dark, 0.02)

    # Grille
    box('GrilleFrame', 1.55, 0.10, 0.92, (0, 4.26, 1.55), silver, 0.02)
    box('GrilleInset', 1.4, 0.08, 0.78, (0, 4.32, 1.55), dark, 0.01)
    for i in range(7):
        box('GrilleBar', 0.07, 0.05, 0.7, (-0.54+i*0.18, 4.37, 1.55), silver, 0.005)

    # Headlights and marker lights
    for s in (-1,1):
        box('Headlight', 0.40, 0.08, 0.24, (s*0.78, 4.28, 1.16), head, 0.02)
        box('HeadlightTrim', 0.46, 0.06, 0.30, (s*0.78, 4.31, 1.16), dark, 0.01)
        for i in range(3):
            box('RoofMarker', 0.12, 0.05, 0.06, (s*0.22 + (i-1)*0.26, 1.76, 3.50), amber, 0.01)
    box('Visor', 2.25, 0.3, 0.07, (0, 1.78, 3.39), white, 0.02)

    # Windshield / side glass
    box('Windshield', 2.04, 0.055, 0.78, (0, 1.835, 2.82), glass, 0.008)
    box('WindshieldTrim', 2.12, 0.04, 0.88, (0, 1.81, 2.82), dark, 0.005)
    for s2 in (-0.55, 0.35):
        box('Wiper', 0.62, 0.025, 0.025, (s2, 1.80, 2.47), black, 0.002, (0,0,math.radians(-14)))
    for s in (-1,1):
        box('SideWindow', 0.04, 0.95, 0.78, (s*1.20, 0.82, 2.82), glass, 0.01)
        box('SleeperWindow', 0.04, 0.55, 0.55, (s*1.18, -0.85, 2.9), glass, 0.01)
        box('DoorSeam', 0.02, 0.03, 1.6, (s*1.225, 1.78, 1.95), dark, 0.002)
        box('BlueStripe', 0.015, 3.2, 0.16, (s*1.235, 0.2, 1.45), blue, 0.003)
        box('DoorHandle', 0.05, 0.18, 0.05, (s*1.24, 1.45, 2.18), silver, 0.005)
        # mirrors
        box('MirrorArm', 0.06, 0.06, 0.65, (s*1.42, 2.0, 2.66), dark, 0.01)
        box('MirrorHousing', 0.16, 0.32, 0.55, (s*1.47, 2.0, 2.98), dark, 0.03)
        box('MirrorGlass', 0.02, 0.26, 0.46, (s*1.54, 2.0, 2.98), glass, 0.005)
        # steps
        for k in range(2):
            box('Step', 0.58, 0.55, 0.07, (s*1.09, 1.12, 0.65+k*0.32), silver, 0.02)
        # fuel tank
        cyl('FuelTank', 0.38, 1.7, (s*1.30, 0.05, 0.97), silver, (math.pi/2,0,0), 24)
        box('TankStrap', 0.05, 0.1, 0.72, (s*1.34, -0.2, 1.0), dark, 0.01)
        box('TankStrap2', 0.05, 0.1, 0.72, (s*1.34, 0.35, 1.0), dark, 0.01)
        # exhaust
        cyl('Exhaust', 0.075, 2.2, (s*1.18, -1.45, 2.28), silver, (0,0,0), 16)
        box('ExhaustShield', 0.24, 0.28, 0.48, (s*1.15, -1.45, 3.3), silver, 0.03)
        # rear lights
        box('RearLamp', 0.16, 0.04, 0.12, (s*0.9, -3.32, 0.95), red, 0.01)
        box('MudFlap', 0.62, 0.05, 0.62, (s*1.06, -3.12, 0.38), black, 0.01)

    # Driver door as a separate hinged object (left side)
    hinge = (-1.245, 1.78, 2.12)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(hinge[0], 0.90, hinge[2]))
    door = bpy.context.object; door.name = 'DriverDoor'
    door.scale = (0.055, 1.72, 2.08)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    md = door.modifiers.new('b','BEVEL'); md.width=0.025; md.segments=3; apply_mods(door)
    door.data.materials.append(white)
    bpy.context.scene.cursor.location = hinge
    bpy.context.view_layer.objects.active = door; door.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); door.select_set(False)
    door.parent = root
    # door window
    box('DoorWindow', 0.025, 1.38, 0.72, (-1.278, 0.92, 2.55), glass, 0.01)
    box('DoorWindowTrim', 0.02, 1.46, 0.80, (-1.272, 0.92, 2.55), dark, 0.005)
    box('DoorHandle', 0.06, 0.20, 0.06, (-1.29, 1.30, 2.08), silver, 0.005)
    # black interior panel behind the door, to read as an opening
    box('CabOpening', 0.02, 1.55, 1.95, (-1.205, 0.92, 2.12), dark, 0.005)

    # Fifth wheel
    box('FifthWheel', 1.18, 1.05, 0.12, (0, -1.78, 1.16), grey, 0.03)
    box('FifthWheelJaw', 0.22, 0.45, 0.18, (0, -1.55, 1.27), dark, 0.02)
    box('FifthWheelHandle', 1.5, 0.08, 0.08, (0, -2.18, 1.12), silver, 0.01)

    # Cab roof/air horns
    for s in (-1,1):
        box('AirHorn', 0.12, 0.5, 0.12, (s*0.72, 0.2, 3.60), silver, 0.02)
    box('RoofLightBar', 1.5, 0.08, 0.06, (0, 1.55, 3.56), dark, 0.01)

    # Badging
    text('FreightlinerBadge', 'FREIGHTLINER', 0.105, (0, 3.79, 1.935), silver, (math.pi/2,0,0), 0.003)
    text('CascadiaBadge', 'CASCADIA', 0.085, (0, 3.55, 1.97), silver, (math.pi/2,0,0), 0.002)
    for s in (-1,1):
        text('DoorBadge', 'FREIGHTLINER', 0.075, (s*1.242, 0.35, 2.20), silver, (0, math.pi/2*s, 0), 0.002)

    # Wheels
    wheel_positions = [(1.13,2.85,'FL',1.0),(-1.13,2.85,'FR',1.0),(1.13,-1.45,'RL1',1.0),(-1.13,-1.45,'RR1',1.0),(1.13,-2.55,'RL2',1.0),(-1.13,-2.55,'RR2',1.0)]
    for x,y,name,scale in wheel_positions:
        tire = cyl('Wheel_'+name, 0.55, 0.42, (x,y,0.55), black, (0,math.pi/2,0), 24, root)
        hub = cyl('Hub_'+name, 0.24, 0.44, (x,y,0.55), silver, (0,math.pi/2,0), 16, root)
        for i in range(6):
            a=i*math.pi/3
            cyl('Lug_%s_%d'%(name,i), 0.035, 0.47, (x,y+math.sin(a)*0.16,0.55+math.cos(a)*0.16), dark, (0,math.pi/2,0), 6, root)
        box('Fender_'+(name), 0.08, 0.9, 0.08, (x*1.18,y,1.18), dark, 0.01)

    # Apply object transforms for export
    for o in bpy.context.scene.objects:
        if o.type=='MESH':
            o.select_set(False)
    return root

def setup_render():
    bpy.ops.object.camera_add(location=(8.5,-11.5,6.2), rotation=(math.radians(67),0,math.radians(36)))
    cam=bpy.context.object
    bpy.context.scene.camera=cam
    cam.data.lens=55
    bpy.ops.object.light_add(type='SUN', location=(4,-4,10))
    sun=bpy.context.object; sun.data.energy=3.0; sun.rotation_euler=(math.radians(35),math.radians(-15),math.radians(35))
    bpy.ops.object.light_add(type='AREA', location=(-4,-2,5))
    area=bpy.context.object; area.data.energy=600; area.data.size=8
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0,0,0))
    plane=bpy.context.object; plane.data.materials.append(mat('Ground',(0.12,0.13,0.14),0,0.9))
    bpy.context.scene.render.engine='BLENDER_WORKBENCH'
    bpy.context.scene.display.shading.light='STUDIO'
    bpy.context.scene.display.shading.color_type='MATERIAL'
    bpy.context.scene.display.shading.show_shadows=True
    bpy.context.scene.display.shading.show_cavity=True
    bpy.context.scene.render.resolution_x=1100
    bpy.context.scene.render.resolution_y=700
    bpy.context.scene.render.film_transparent=False
    if bpy.context.scene.world is None:
        bpy.context.scene.world = bpy.data.worlds.new('World')
    bpy.context.scene.world.color=(0.03,0.04,0.05)
    bpy.context.scene.render.filepath=os.path.join(OUT,'Previews','truck_preview.png')
    bpy.ops.render.render(write_still=True)

if __name__ == '__main__':
    reset()
    root=make_truck()
    setup_render()
    # Select all truck meshes and export
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type=='MESH' and o.name not in ('Plane',):
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Models','Freightliner_Cascadia.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y', global_scale=1.0, apply_scale_options='FBX_SCALE_ALL')
    print('EXPORTED')
