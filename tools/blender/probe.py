"""Proves this Blender can do the four things the content pipeline needs.

Run with:  make blender-probe

Every one of these was an open question before it was answered here, and three of them could only
be answered by running Blender in a container with no display and no GPU:

1. Python scripting at all, with `--background --factory-startup`.
2. Topology facts — triangle, boundary-edge and non-manifold-edge counts. The catalogue validator
   (C-01b) refuses an asset on these numbers, and they come from the exporter rather than being
   re-derived from the GLB afterwards.
3. glTF export to GLB, which is the format the import gatekeeper (I-01) reads and glTFast loads.
4. Rendering a thumbnail with no GPU and no display. The inventory browser needs one per item
   (C-01b), and EEVEE wants OpenGL, so this uses Cycles on the CPU.

It writes into a temporary directory and leaves nothing behind.
"""

import os
import sys
import tempfile

import bpy
import bmesh

EXPECTED_VERSION = (4, 5, 16)


def fail(message):
    print(f"PROBE FAILED: {message}")
    sys.exit(1)


def check_version():
    if bpy.app.version != EXPECTED_VERSION:
        fail(f"this is Blender {bpy.app.version}, the pin is {EXPECTED_VERSION}. "
             "tools/blender/install.sh and this file must agree.")
    print(f"version  {'.'.join(str(part) for part in bpy.app.version)}")


def check_scripting():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.mesh.primitive_cube_add(size=2)
    mesh = bpy.context.active_object.data
    if len(mesh.vertices) != 8 or len(mesh.polygons) != 6:
        fail(f"a cube came out as {len(mesh.vertices)} verts, {len(mesh.polygons)} faces")
    print("scripting  a cube is 8 verts and 6 faces")


def topology_of(obj):
    """The numbers a facts file carries. Counted the way C-01b's validator reads them."""
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    try:
        return {
            "triangles": sum(len(face.verts) - 2 for face in mesh.faces),
            "boundary": sum(1 for edge in mesh.edges if len(edge.link_faces) < 2),
            "nonmanifold": sum(1 for edge in mesh.edges if len(edge.link_faces) > 2),
        }
    finally:
        mesh.free()


def check_topology():
    # A closed cube: no boundary edges, no non-manifold edges. The validator refuses open geometry
    # that is not declared double-sided, so "closed" has to be measurable.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.mesh.primitive_cube_add(size=2)
    closed = topology_of(bpy.context.active_object)
    if closed != {"triangles": 12, "boundary": 0, "nonmanifold": 0}:
        fail(f"a closed cube measured {closed}")

    # The same cube with a face removed: open, and it must show as open.
    mesh = bmesh.new()
    mesh.from_mesh(bpy.context.active_object.data)
    mesh.faces.ensure_lookup_table()
    bmesh.ops.delete(mesh, geom=[mesh.faces[0]], context="FACES")
    mesh.to_mesh(bpy.context.active_object.data)
    mesh.free()

    opened = topology_of(bpy.context.active_object)
    if opened["boundary"] != 4:
        fail(f"a cube with one face removed has {opened['boundary']} boundary edges, expected 4")
    print(f"topology  closed {closed}, open {opened}")


def check_glb(out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.mesh.primitive_monkey_add(size=2)

    path = os.path.join(out, "probe.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=False)

    with open(path, "rb") as handle:
        magic = handle.read(4)
    if magic != b"glTF":
        fail(f"the exported file starts with {magic!r}, not a GLB header")

    size = os.path.getsize(path)
    if size < 1024:
        fail(f"the exported GLB is only {size} bytes")
    print(f"glb  {size:,} bytes, header is glTF")


def check_render(out):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"      # there is no GPU here, and there will not be one in CI
    scene.cycles.samples = 8
    scene.render.resolution_x = scene.render.resolution_y = 128
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.join(out, "probe.png")

    bpy.ops.object.camera_add(location=(4, -4, 3))
    scene.camera = bpy.context.active_object
    bpy.ops.render.render(write_still=True)

    with open(scene.render.filepath, "rb") as handle:
        magic = handle.read(8)
    if magic != b"\x89PNG\r\n\x1a\n":
        fail(f"the render starts with {magic!r}, not a PNG header")
    print(f"render  {os.path.getsize(scene.render.filepath):,} bytes, no GPU, no display")


def main():
    check_version()
    check_scripting()
    check_topology()
    with tempfile.TemporaryDirectory() as out:
        check_glb(out)
        check_render(out)
    print("PROBE OK")


main()
