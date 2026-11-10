# Executed inside sandboxed Blender CLI:
# blender --background --factory-startup -noaudio --python blender_safe_convert.py -- <input_path> <output_glb_path> <unit_scale> <center_bottom_pivot>
import bpy
import sys
import os
import mathutils

def main():
    # Hard security check: disable any user scripts or addon auto-execution
    bpy.context.preferences.filepaths.use_scripts_auto_execute = False

    argv = sys.argv
    if "--" not in argv:
        sys.exit(1)
    args = argv[argv.index("--") + 1:]
    input_path = args[0]
    output_glb = args[1]
    scale_factor = float(args[2])
    recenter_pivot = args[3].lower() == "true"

    # Clear factory startup scene
    bpy.ops.wm.read_factory_settings(use_empty=True)

    ext = os.path.splitext(input_path)[1].lower()
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=input_path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=input_path)
    elif ext == ".stl":
        bpy.ops.wm.stl_import(filepath=input_path)
    elif ext == ".dae":
        bpy.ops.wm.collada_import(filepath=input_path)
    elif ext == ".blend":
        # Append only Mesh and Object data-blocks; NEVER load texts/scripts from untrusted .blend files!
        with bpy.data.libraries.load(input_path, link=False) as (data_from, data_to):
            data_to.objects = [name for name in data_from.objects]
        for obj in data_to.objects:
            if obj is not None and obj.type == 'MESH':
                bpy.context.collection.objects.link(obj)
    else:
        sys.exit(2)

    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if not mesh_objects:
        sys.exit(3)

    # Compute combined world-space bounding box to normalize Bottom-Center Pivot & Unit Scale
    min_v = mathutils.Vector((1e9, 1e9, 1e9))
    max_v = mathutils.Vector((-1e9, -1e9, -1e9))

    for obj in mesh_objects:
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ mathutils.Vector(corner)
            min_v.x = min(min_v.x, world_corner.x)
            min_v.y = min(min_v.y, world_corner.y)
            min_v.z = min(min_v.z, world_corner.z)
            max_v.x = max(max_v.x, world_corner.x)
            max_v.y = max(max_v.y, world_corner.y)
            max_v.z = max(max_v.z, world_corner.z)

    # In Blender Z-up coordinates, bottom-center is ((min_x+max_x)/2, (min_y+max_y)/2, min_z)
    bottom_center = mathutils.Vector(((min_v.x + max_v.x) * 0.5, (min_v.y + max_v.y) * 0.5, min_v.z))

    for obj in mesh_objects:
        if recenter_pivot:
            obj.location -= bottom_center
        obj.scale *= scale_factor

    bpy.ops.export_scene.gltf(
        filepath=output_glb,
        export_format='GLB',
        export_apply=True,
        export_yup=True,
        export_Lights=False,
        export_cameras=False
    )

if __name__ == "__main__":
    main()
