import bpy, math, os
from mathutils import Vector

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)),'Models','Freightliner_Cascadia.fbx'))
# world
world=bpy.data.worlds.new('W'); bpy.context.scene.world=world; world.color=(0.04,0.05,0.06)
# floor
bpy.ops.mesh.primitive_plane_add(size=60, location=(0,0,0))
floor=bpy.context.object
m=bpy.data.materials.new('Ground'); m.diffuse_color=(0.07,0.075,0.08,1); floor.data.materials.append(m)
# target empty
bpy.ops.object.empty_add(location=(0,0.5,1.6)); target=bpy.context.object
# sun
bpy.ops.object.light_add(type='SUN', location=(5,5,10)); sun=bpy.context.object; sun.data.energy=3; sun.rotation_euler=(math.radians(30),math.radians(-20),math.radians(25))
bpy.ops.object.light_add(type='AREA', location=(-5,6,6)); area=bpy.context.object; area.data.energy=900; area.data.size=10
# camera
bpy.ops.object.camera_add(location=(9,12,5.5)); cam=bpy.context.object; bpy.context.scene.camera=cam; cam.data.lens=58
con=cam.constraints.new('TRACK_TO'); con.target=target; con.track_axis='TRACK_NEGATIVE_Z'; con.up_axis='UP_Y'
bpy.context.scene.render.engine='BLENDER_WORKBENCH'
bpy.context.scene.display.shading.light='STUDIO'
bpy.context.scene.display.shading.color_type='MATERIAL'
bpy.context.scene.display.shading.show_shadows=True
bpy.context.scene.display.shading.show_cavity=True
bpy.context.scene.display.shading.cavity_type='BOTH'
bpy.context.scene.render.resolution_x=1200; bpy.context.scene.render.resolution_y=800
for name,loc in [('front_left',(10,13,5.5)),('front_right',(-10,13,5.5)),('side',(14,0,4.5)),('rear',(7,-14,5.5))]:
    cam.location=loc
    bpy.context.scene.render.filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)),'Previews',f'truck_{name}.png')
    bpy.ops.render.render(write_still=True)
    print('rendered',name)
