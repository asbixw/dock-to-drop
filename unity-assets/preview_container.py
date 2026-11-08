import bpy, math, os
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)),'Models','Amazon_53ft_Container.fbx'))
w=bpy.data.worlds.new('W'); bpy.context.scene.world=w; w.color=(0.04,0.05,0.06)
bpy.ops.mesh.primitive_plane_add(size=80, location=(0,0,0)); fl=bpy.context.object
m=bpy.data.materials.new('Ground'); m.diffuse_color=(0.07,0.075,0.08,1); fl.data.materials.append(m)
bpy.ops.object.empty_add(location=(0,0,2.2)); t=bpy.context.object
bpy.ops.object.light_add(type='SUN', location=(5,5,10)); s=bpy.context.object; s.data.energy=3; s.rotation_euler=(math.radians(30),math.radians(-20),math.radians(25))
bpy.ops.object.camera_add(location=(12,15,7)); c=bpy.context.object; bpy.context.scene.camera=c; c.data.lens=52
con=c.constraints.new('TRACK_TO'); con.target=t; con.track_axis='TRACK_NEGATIVE_Z'; con.up_axis='UP_Y'
bpy.context.scene.render.engine='BLENDER_WORKBENCH'
bpy.context.scene.display.shading.light='STUDIO'; bpy.context.scene.display.shading.color_type='MATERIAL'; bpy.context.scene.display.shading.show_shadows=True; bpy.context.scene.display.shading.show_cavity=True
bpy.context.scene.render.resolution_x=1200; bpy.context.scene.render.resolution_y=800
for name,loc in [('side',(13,5,7)),('rear',(9,-14,6)),('front',(-10,14,7))]:
    c.location=loc
    bpy.context.scene.render.filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)),'Previews',f'container_{name}.png')
    bpy.ops.render.render(write_still=True)
    print(name)
