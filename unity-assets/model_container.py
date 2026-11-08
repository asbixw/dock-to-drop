import bpy, math, os
from mathutils import Vector

OUT=os.path.dirname(os.path.abspath(__file__))

def apply(o):
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    for m in list(o.modifiers):
        try: bpy.ops.object.modifier_apply(modifier=m.name)
        except Exception: pass
    o.select_set(False)

def mat(name,color,metal=0,rough=.5,alpha=1):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,alpha); m.use_nodes=True
    b=m.node_tree.nodes.get('Principled BSDF'); b.inputs['Base Color'].default_value=(*color,alpha)
    b.inputs['Metallic'].default_value=metal; b.inputs['Roughness'].default_value=rough
    if alpha<1: b.inputs['Alpha'].default_value=alpha; m.blend_method='BLEND'
    return m

def box(name,sx,sy,sz,loc,material,bevel=.02,rot=(0,0,0),parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc,rotation=rot)
    o=bpy.context.object; o.name=name; o.scale=(sx,sy,sz)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel>0:
        md=o.modifiers.new('b','BEVEL'); md.width=bevel; md.segments=3; apply(o)
    o.data.materials.append(material)
    if parent: o.parent=parent
    return o

def cyl(name,r,d,loc,material,rot=(0,0,0),verts=24,parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=d,location=loc,rotation=rot)
    o=bpy.context.object; o.name=name; o.data.materials.append(material)
    if parent: o.parent=parent
    return o

def text(name,body,size,loc,material,rot=(0,0,0),extrude=.004,parent=None,align='CENTER'):
    cu=bpy.data.curves.new(name,'FONT'); cu.body=body; cu.align_x=align; cu.align_y='CENTER'; cu.size=size; cu.extrude=extrude
    ob=bpy.data.objects.new(name,cu); bpy.context.collection.objects.link(ob); ob.location=loc; ob.rotation_euler=rot
    ob.data.materials.append(material); bpy.context.view_layer.objects.active=ob; ob.select_set(True)
    bpy.ops.object.convert(target='MESH'); ob.select_set(False)
    if parent: ob.parent=parent
    return ob

def mirror_x(obj):
    obj.scale.x *= -1
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)

def make():
    root=bpy.data.objects.new('ContainerRoot',None); bpy.context.collection.objects.link(root)
    orange=mat('Container_Orange',(0.52,0.14,0.045),0.15,0.6)
    orange_dark=mat('Container_Rib',(0.38,0.09,0.03),0.12,0.68)
    white=mat('White',(0.82,0.80,0.73),0.0,0.55)
    black=mat('Rubber',(0.012,0.014,0.016),0,0.9)
    steel=mat('Steel',(0.45,0.48,0.52),0.8,0.3)
    chrome=mat('Chrome',(0.75,0.78,0.82),1,0.14)
    dark=mat('Dark',(0.04,0.045,0.05),0.2,0.55)
    glass=mat('Glass',(0.02,0.04,0.06),0.2,0.15,0.7)
    amber=mat('Amber',(1,0.35,0.03),0,0.3)
    red=mat('Red',(0.8,0.02,0.01),0,0.3)
    blue=mat('Blue',(0.02,0.12,0.24),0.4,0.3)
    smile=mat('AmazonOrange',(1.0,0.42,0.03),0.0,0.35)

    # Chassis
    for x in (-0.86,0.86):
        box('ChassisRail',0.16,16.0,0.22,(x,0,1.02),dark)
    for y in range(-7,8,1):
        box('Crossmember',1.9,0.12,0.14,(0,y,1.02),dark)
    box('KingpinPlate',1.5,0.8,0.1,(0,8.05,1.15),steel)
    cyl('Kingpin',0.09,0.28,(0,8.05,0.95),chrome,(0,0,0),16,root)

    # Container shell
    L=16.15; W=2.58; H=2.9; Z=2.72
    # side walls
    for s in (-1,1):
        box('SideWall',0.10,L,H,(s*W/2,0,Z),orange,.01)
        box('SideRailTop',0.16,L,0.14,(s*W/2,0,Z+H/2),orange_dark,.01)
        box('SideRailBot',0.16,L,0.16,(s*W/2,0,Z-H/2),dark,.01)
        # corrugation ribs
        for i in range(25):
            y=-L/2+0.5+i*0.63
            box('Rib',0.075,H-0.18,0.08,(s*(W/2+0.085),y,Z),orange_dark,.004)
        # corner posts
        for y in (-L/2,L/2):
            box('CornerPost',0.16,0.16,H,(s*W/2,y,Z),orange_dark,.01)
    # roof, floor, front
    box('Roof',W, L,0.12,(0,0,Z+H/2),orange,.01)
    box('Floor',W,L,0.16,(0,0,Z-H/2),dark,.01)
    box('FrontWall',W,H,0.12,(0,L/2,Z),orange,.01)
    # rear frame
    for x in (-W/2,W/2):
        box('RearPost',0.16,0.16,H,(x,-L/2,Z),orange_dark,.01)
    box('RearHeader',W,0.18,0.18,(0,-L/2,Z+H/2),orange_dark,.01)
    box('RearSill',W,0.18,0.18,(0,-L/2,Z-H/2),dark,.01)

    # Rear doors, separate objects with hinge origins
    door_w=1.22; door_h=2.74
    for s in (-1,1):
        hinge=(s*(W/2-0.04),-L/2-0.03,Z)
        door=box('Door_'+('L' if s<0 else 'R'),door_w,0.10,door_h,(hinge[0]-s*door_w/2, hinge[1], hinge[2]),orange_dark,.01)
        # set origin to hinge
        bpy.context.scene.cursor.location=hinge
        bpy.context.view_layer.objects.active=door; door.select_set(True)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR'); door.select_set(False)
        # rods
        for k in (-0.38,0.30):
            cyl('LockRod',0.025,2.45,(hinge[0]-s*door_w/2+s*k,hinge[1]-0.06,hinge[2]),chrome,(0,0,0),8,root)
            box('RodHandle',0.18,0.05,0.05,(hinge[0]-s*door_w/2+s*k,hinge[1]-0.10,hinge[2]-0.45),chrome,.005)
        # hinges
        for zz in (-0.75,0.75):
            box('Hinge',0.10,0.16,0.12,(s*(W/2+0.02),-L/2-0.06,hinge[2]+zz),steel,.005)
    # seal tag at right door
    tag=box('SealTag',0.13,0.03,0.19,(0.62,-L/2-0.12,Z-0.05),mat('SealYellow',(0.82,0.55,0.06),0.2,0.4),.004)
    text('SealNumber','482913',0.075,(0.62,-L/2-0.145,Z-0.05),dark,(math.pi/2,0,0),.002,root)
    # rear lights / underride
    box('Underride',2.2,0.12,0.14,(0,-L/2-0.55,0.62),dark,.01)
    for x in (-0.8,0,0.8):
        box('TailLight',0.18,0.04,0.12,(x,-L/2-0.12,0.82),red,.005)
    for s in (-1,1):
        box('Mudflap',0.55,0.04,0.5,(s*1.0,-L/2+0.35,0.32),black,.005)

    # Landing gear
    for s in (-1,1):
        box('LandingLeg',0.14,0.14,2.0,(s*0.82,5.2,1.15),steel,.01)
        box('LandingFoot',0.5,0.45,0.1,(s*0.82,5.2,0.15),dark,.01)
    box('LandingCross',1.7,0.12,0.12,(0,5.2,2.1),steel,.01)
    # wheels rear
    for y in (-5.1,-6.2):
        for s in (-1,1):
            cyl('Wheel',0.55,0.40,(s*1.02,y,0.55),black,(0,math.pi/2,0),24,root)
            cyl('Hub',0.23,0.42,(s*1.02,y,0.55),chrome,(0,math.pi/2,0),16,root)
            for i in range(6):
                a=i*math.pi/3
                cyl('Lug',0.03,0.44,(s*1.02,y+math.sin(a)*0.15,0.55+math.cos(a)*0.15),dark,(0,math.pi/2,0),6,root)

    # Branding: amazon + smile
    mirror_x(text('AmazonSideL','amazon',0.52,(-W/2-0.14,0.0,Z+0.28),white,(math.pi/2,0,math.pi/2),.004,root))
    mirror_x(text('AmazonSideR','amazon',0.52,(W/2+0.14,0.0,Z+0.28),white,(math.pi/2,0,-math.pi/2),.004,root))
    # smile arc under amazon on both sides (smooth curve)
    for s in (-1,1):
        cu=bpy.data.curves.new('SmileCurve','CURVE'); cu.dimensions='3D'
        sp=cu.splines.new('POLY'); n=17; sp.points.add(n-1)
        for i in range(n):
            t=i/(n-1); y=-1.0+2.0*t; z=Z+0.03 - 0.24*(1-(2*t-1)**2)
            sp.points[i].co=(s*(W/2+0.16),y,z,1)
        cu.bevel_depth=0.028; cu.bevel_resolution=2; cu.resolution_u=2
        ob=bpy.data.objects.new('SmileArc',cu); bpy.context.collection.objects.link(ob)
        ob.data.materials.append(smile); ob.parent=root
        # arrowhead at the end of the smile
        tip_y=1.04
        cone=bpy.data.meshes.new('Arrow'); cone.from_pydata([(0,0,0),(0.10,0.10,0),(0.10,-0.10,0)], [], [(0,1,2)]); cone.update()
        ar=bpy.data.objects.new('SmileArrow',cone); bpy.context.collection.objects.link(ar)
        ar.location=(s*(W/2+0.16), tip_y, Z+0.03-0.24); ar.scale=(1,1,1)
        ar.rotation_euler=(0,0,math.radians(90) if s>0 else math.radians(-90))
        ar.data.materials.append(smile); ar.parent=root
    mirror_x(text('SideSpecL',"53 FT  ·  MAX GROSS 67,200 LB",0.14,(-W/2-0.14,-4.4,Z-0.55),white,(math.pi/2,0,math.pi/2),.002,root))
    mirror_x(text('SideSpecR',"53 FT  ·  MAX GROSS 67,200 LB",0.14,(W/2+0.14,-4.4,Z-0.55),white,(math.pi/2,0,-math.pi/2),.002,root))
    # front markings
    text('FrontMark','AMAZON RELAY',0.20,(0,L/2+0.075,Z-0.3),white,(math.pi/2,0,math.pi),.002,root)

    # Interior cargo
    for i in range(6):
        box('Cargo',0.85,1.0,1.1,(-0.55+ (i%2)*1.1, -L/2+0.9+ (i//2)*1.15, Z-0.8),mat('Cardboard',(0.42,0.29,0.16),0,.9),.01,parent=root)

    return root

if __name__=='__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root=make()
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type=='MESH': o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Models','Amazon_53ft_Container.fbx'),use_selection=True,axis_forward='-Z',axis_up='Y',global_scale=1.0,apply_scale_options='FBX_SCALE_ALL')
    print('EXPORTED CONTAINER')
