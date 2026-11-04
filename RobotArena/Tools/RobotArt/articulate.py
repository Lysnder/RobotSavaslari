import bpy, numpy as np, os, json, math
from mathutils import Vector
ROOT=r'C:\Users\Burak\RobotSavaslari\RobotArena'
OUT=os.path.join(ROOT,'Assets','Models','Robots','Articulated')
os.makedirs(OUT,exist_ok=True)
report={}
for name in ['BlueCombatRobot','RedLifterRobot']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(ROOT,'Assets','Models','Robots',name+'.glb'))
    original=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bpy.context.view_layer.objects.active=original;original.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mesh=original.data
    xyz=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',xyz);xyz=xyz.reshape(-1,3)
    idx=np.empty(len(mesh.loops),dtype=np.int32);mesh.loops.foreach_get('vertex_index',idx);idx=idx.reshape(-1,3)
    uv=np.empty(len(mesh.loops)*2,dtype=np.float32);mesh.uv_layers.active.data.foreach_get('uv',uv);uv=uv.reshape(-1,3,2)
    center=xyz[idx].mean(axis=1);x,y,z=center.T
    texture=next(n.image for n in mesh.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB')
    pixels=np.empty(len(texture.pixels),dtype=np.float32);texture.pixels.foreach_get(pixels);pixels=pixels.reshape(texture.size[1],texture.size[0],4)
    face_uv=np.mod(uv.mean(axis=1),1.0)
    rgb=pixels[(face_uv[:,1]*(texture.size[1]-1)).astype(int),(face_uv[:,0]*(texture.size[0]-1)).astype(int),:3]
    labels=np.zeros(len(idx),dtype=np.int32)
    blue=name.startswith('Blue')
    # These volumes were measured against orthographic source-model views.
    # Axle and hinge cuts are placed inside the stationary housings.
    wheel_specs=([(-.56,-.63,-.235,.245),(.56,-.63,-.235,.245),(-.56,.63,-.235,.245),(.56,.63,-.235,.245)] if blue else
                 [(-.42,-.60,-.095,.275),(.51,-.60,-.095,.265),(-.42,.60,-.095,.275),(.51,.60,-.095,.265)])
    names=['Body','Wheel_FL','Wheel_RL','Wheel_FR','Wheel_RR','Spinner' if blue else 'Lifter']
    pivots=[(0,0,0)]
    for i,(cx,cy,cz,radius) in enumerate(wheel_specs,1):
        mask=((x-cx)**2+(z-cz)**2<(.285 if blue else .29)**2)&(y*np.sign(cy)>(.51 if blue else .505))&(z<(.025 if blue else .205))
        paint=(rgb[:,2]>rgb[:,0]*1.18)&(rgb[:,2]>.06) if blue else (rgb[:,0]>rgb[:,1]*1.35)&(rgb[:,0]>.08)
        mask &= ~paint
        labels[mask]=i
        points=xyz[idx[mask]].reshape(-1,3)
        pivots.append((cx,(points[:,1].min()+points[:,1].max())/2,cz))
    if blue:
        labels[z>.295]=5
        # Recover the actual circular disc center from its top-level geometry.
        p=xyz[idx[labels==5]].reshape(-1,3)
        pivots.append(((p[:,0].min()+p[:,0].max())/2,(p[:,1].min()+p[:,1].max())/2,.305))
    else:
        # Central lift beam and fork assembly. Lateral chassis rails remain fixed.
        arm=(x<.20)&(np.abs(y)<.215)&(z>.035)
        forks=(x<-.42)&(np.abs(y)<.235)&(z<.08)
        labels[arm|forks]=5
        pivots.append((.065,0,.285))
    mats=list(mesh.materials)
    parts=[];report[name]=[]
    for i,partname in enumerate(names):
        mask=labels==i
        source_faces=idx[mask]
        unique,remap=np.unique(source_faces,return_inverse=True)
        remap=remap.ravel().astype(np.int32)
        verts=xyz[unique]-np.asarray(pivots[i])
        data=bpy.data.meshes.new(partname)
        data.vertices.add(len(verts));data.vertices.foreach_set('co',verts.ravel())
        data.loops.add(len(remap));data.loops.foreach_set('vertex_index',remap)
        data.polygons.add(len(source_faces));data.polygons.foreach_set('loop_start',np.arange(len(source_faces),dtype=np.int32)*3)
        data.polygons.foreach_set('loop_total',np.full(len(source_faces),3,dtype=np.int32))
        data.polygons.foreach_set('use_smooth',np.ones(len(source_faces),dtype=bool))
        layer=data.uv_layers.new(name='UVMap');layer.data.foreach_set('uv',uv[mask].ravel())
        for mat in mats:data.materials.append(mat)
        data.update()
        obj=bpy.data.objects.new(partname,data);bpy.context.collection.objects.link(obj);obj.location=pivots[i];parts.append(obj)
        report[name].append({'name':partname,'faces':int(mask.sum()),'pivot':list(map(float,pivots[i]))})
    bpy.data.objects.remove(original,do_unlink=True)
    # Seal the hidden shaft/hinge connections with simple dark metal hardware.
    steel=bpy.data.materials.new('Joint Steel');steel.diffuse_color=(.06,.07,.08,1)
    for i,pivot in enumerate(pivots[1:5],1):
        bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.055,depth=.08,location=(pivot[0],.47*np.sign(pivot[1]),pivot[2]),rotation=(math.pi/2,0,0))
        axle=bpy.context.object;axle.name='Axle_'+str(i);axle.data.materials.append(steel)
    if blue:
        bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.15,depth=.025,location=pivots[5])
        hub=bpy.context.object;hub.name='Spinner Shaft Cap';hub.data.materials.append(steel)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource',name+'_Articulated.blend'))
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,name+'_Articulated.glb'),export_format='GLB',export_animations=False)
    scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.color_type='TEXTURE';scene.display.shading.light='STUDIO';scene.display.shading.show_cavity=True
    scene.render.resolution_x=1000;scene.render.resolution_y=850;scene.render.resolution_percentage=100
    camdata=bpy.data.cameras.new('Inspection');cam=bpy.data.objects.new('Inspection',camdata);scene.collection.objects.link(cam);scene.camera=cam;camdata.type='ORTHO';camdata.ortho_scale=2.6
    cam.location=(-2,-3,2.6);cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler()
    # Deliberately articulate all parts for QA, after exporting the resting pose.
    for wheel in parts[1:5]:wheel.rotation_euler.y=math.radians(45)
    parts[5].rotation_euler.z=math.radians(50) if blue else 0
    if not blue:parts[5].rotation_euler.y=math.radians(35)
    scene.render.filepath=os.path.join(ROOT,'ArtSource',name+'_motion_check.png');bpy.ops.render.render(write_still=True)
with open(os.path.join(ROOT,'ArtSource','articulation.json'),'w') as f:json.dump(report,f,indent=2)
