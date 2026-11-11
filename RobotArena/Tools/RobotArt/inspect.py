import bpy, json, os
from mathutils import Vector
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
report = {}
for name in ['BlueCombatRobot', 'RedLifterRobot']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(root,'Assets','Models','Robots',name+'.glb'))
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    report[name]=[]
    for o in objs:
        report[name].append({'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'bounds':[[min((o.matrix_world@v.co)[a] for v in o.data.vertices),max((o.matrix_world@v.co)[a] for v in o.data.vertices)] for a in range(3)]})
    scene=bpy.context.scene
    scene.render.engine='BLENDER_WORKBENCH'
    scene.display.shading.light='STUDIO'
    scene.display.shading.color_type='TEXTURE'
    scene.display.shading.show_shadows=True
    scene.display.shading.show_cavity=True
    scene.render.resolution_x=1000
    scene.render.resolution_y=1000
    scene.render.resolution_percentage=100
    camdata=bpy.data.cameras.new('Inspect'); cam=bpy.data.objects.new('Inspect',camdata);scene.collection.objects.link(cam);scene.camera=cam
    camdata.type='ORTHO';camdata.ortho_scale=2.25
    for view,pos in [('top',(0,0,4)),('front',(0,-4,0)),('side',(4,0,0))]:
        cam.location=pos;cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=os.path.join(root,'ArtSource',name+'_'+view+'.png')
        bpy.ops.render.render(write_still=True)
with open(os.path.join(root,'ArtSource','inspection.json'),'w') as f:json.dump(report,f,indent=2)
