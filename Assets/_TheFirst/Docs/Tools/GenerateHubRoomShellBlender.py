import math
import os
import random

import bpy
from mathutils import Vector


PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../.."))
OUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "_TheFirst", "Art", "Models", "Hub", "RoomShell")
TEXTURE_DIR = os.path.join(OUT_DIR, "Textures")
BLEND_PATH = os.path.join(OUT_DIR, "HubRoomShell_ModularKit.blend")
GLB_PATH = os.path.join(OUT_DIR, "HubRoomShell_ModularKit.glb")
FBX_PATH = os.path.join(OUT_DIR, "HubRoomShell_ModularKit.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "HubRoomShell_ModularKit_Preview.png")


def ensure_dirs():
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(TEXTURE_DIR, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_texture(name, path, base_a, base_b, seam_color, seed):
    random.seed(seed)
    size = 512
    image = bpy.data.images.new(name, width=size, height=size, alpha=True)
    pixels = []
    for y in range(size):
        plank_band = (y // 64) % 2
        band_tint = 0.08 if plank_band else 0.0
        seam = min(y % 64, 63 - (y % 64)) < 2
        for x in range(size):
            grain = (
                math.sin((x + seed) * 0.055)
                + math.sin((x * 0.021) + (y * 0.013) + seed)
                + random.uniform(-0.13, 0.13)
            ) * 0.09
            t = max(0.0, min(1.0, 0.46 + grain + band_tint))
            if seam:
                color = seam_color
            else:
                color = tuple(base_a[i] * (1.0 - t) + base_b[i] * t for i in range(3))
            pixels.extend([color[0], color[1], color[2], 1.0])

    image.pixels = pixels
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return image


def material_with_texture(name, image, tint=(1.0, 1.0, 1.0), roughness=0.75):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = find_principled_bsdf(nodes)
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def simple_material(name, color, emission=None, strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = find_principled_bsdf(mat.node_tree.nodes)
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = 0.78
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = strength
    return mat


def find_principled_bsdf(nodes):
    for node in nodes:
        if node.type == "BSDF_PRINCIPLED":
            return node
    return nodes.new("ShaderNodeBsdfPrincipled")


def create_cube(name, loc, scale, mat, bevel=0.035, collection=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat is not None:
        obj.data.materials.append(mat)

    if bevel > 0.0:
        bevel_mod = obj.modifiers.new("small_rounded_bevel", "BEVEL")
        bevel_mod.width = bevel
        bevel_mod.segments = 3
        bevel_mod.affect = "EDGES"
        normal_mod = obj.modifiers.new("weighted_stylized_normals", "WEIGHTED_NORMAL")
        normal_mod.keep_sharp = True

    smart_uv(obj)

    if collection is not None:
        for c in obj.users_collection:
            c.objects.unlink(obj)
        collection.objects.link(obj)
    return obj


def smart_uv(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.025)
    bpy.ops.object.mode_set(mode="OBJECT")


def create_cylinder(name, loc, radius, depth, mat, vertices=16, bevel=0.02, collection=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc)
    obj = bpy.context.object
    obj.name = name
    if mat is not None:
        obj.data.materials.append(mat)
    if bevel > 0.0:
        bevel_mod = obj.modifiers.new("soft_edge_bevel", "BEVEL")
        bevel_mod.width = bevel
        bevel_mod.segments = 2
        normal_mod = obj.modifiers.new("weighted_normals", "WEIGHTED_NORMAL")
        normal_mod.keep_sharp = True
    smart_uv(obj)
    if collection is not None:
        for c in obj.users_collection:
            c.objects.unlink(obj)
        collection.objects.link(obj)
    return obj


def create_collection(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def make_floor_kit(materials):
    col = create_collection("A_Floor_Modular_Kit")
    warm = materials["wood_warm"]
    dark = materials["wood_dark"]
    seam = materials["seam"]

    x = -8.0
    create_cube("Floor_BaseTile_2x2", (x, 0, 0.0), (2.0, 2.0, 0.16), warm, 0.045, col)
    create_cube("Floor_Plank_Long_A", (x + 2.6, 0.4, 0.02), (2.2, 0.42, 0.18), warm, 0.04, col)
    create_cube("Floor_Plank_Long_B", (x + 2.6, -0.25, 0.02), (1.9, 0.42, 0.18), warm, 0.04, col)
    create_cube("Floor_Plank_Short", (x + 4.9, 0.32, 0.02), (1.1, 0.42, 0.18), warm, 0.04, col)
    create_cube("Floor_Plank_Cracked", (x + 4.9, -0.32, 0.02), (1.15, 0.42, 0.18), warm, 0.04, col)
    crack = create_cube("Floor_Plank_Crack_Inset", (x + 4.9, -0.32, 0.115), (0.66, 0.035, 0.012), seam, 0.004, col)
    crack.rotation_euler[2] = math.radians(9)
    create_cube("Floor_DarkSeam_Strip", (x + 6.5, 0.4, 0.02), (1.8, 0.07, 0.08), seam, 0.01, col)
    create_cube("Floor_EdgeTrim", (x + 6.5, -0.16, 0.08), (1.8, 0.24, 0.28), dark, 0.055, col)
    create_cube("Floor_CornerTrim_L", (x + 7.9, -0.16, 0.08), (0.34, 1.1, 0.28), dark, 0.055, col)

    nail_positions = [
        (x + 2.6, 0.23, 0.13),
        (x + 1.65, 0.23, 0.13),
        (x + 3.55, 0.23, 0.13),
        (x + 2.6, -0.43, 0.13),
    ]
    for i, pos in enumerate(nail_positions):
        create_cylinder(f"Floor_RoundNail_{i+1}", pos, 0.055, 0.035, materials["metal"], 12, 0.005, col)

    sample = create_collection("B_Floor_Assembled_Sample")
    start_x = -4.0
    start_y = -3.7
    for row in range(5):
        offset = 0.55 if row % 2 else 0.0
        for piece in range(4):
            length = 1.12 if (piece + row) % 3 == 0 else 1.75
            obj = create_cube(
                f"FloorSample_Plank_R{row+1}_{piece+1}",
                (start_x + piece * 1.35 + offset, start_y + row * 0.46, 0.0),
                (length, 0.42, 0.16),
                warm,
                0.035,
                sample,
            )
            obj.rotation_euler[2] = math.radians(random.uniform(-0.8, 0.8))


def make_wall_panel(name, loc, width, height, materials, collection, angled=0.0):
    warm = materials["wall_warm"]
    dark = materials["wood_dark"]
    post_w = 0.22
    panel = create_cube(f"{name}_InsetPlanks", loc, (width, 0.22, height), warm, 0.035, collection)
    panel.rotation_euler[2] = angled
    panel.location.z = loc[2]

    top_z = loc[2] + height * 0.5 + 0.14
    bottom_z = loc[2] - height * 0.5 - 0.08
    left_x = loc[0] - width * 0.5 - post_w * 0.5
    right_x = loc[0] + width * 0.5 + post_w * 0.5
    for suffix, x in (("LeftPost", left_x), ("RightPost", right_x)):
        obj = create_cube(f"{name}_{suffix}", (x, loc[1], loc[2]), (post_w, 0.36, height + 0.42), dark, 0.06, collection)
        obj.rotation_euler[2] = angled
    for suffix, z in (("TopRail", top_z), ("Baseboard", bottom_z)):
        obj = create_cube(f"{name}_{suffix}", (loc[0], loc[1], z), (width + post_w * 2.0, 0.38, 0.26), dark, 0.06, collection)
        obj.rotation_euler[2] = angled
    return panel


def make_wall_kit(materials):
    col = create_collection("C_Wall_Modular_Kit")
    dark = materials["wood_dark"]
    metal = materials["metal"]
    glow = materials["glow_orange"]

    make_wall_panel("WallPanel_Straight_3m", (-7.0, 3.5, 1.4), 3.0, 2.2, materials, col)
    angled_l = make_wall_panel("WallPanel_Angled_Left", (-3.1, 3.5, 1.4), 3.0, 2.2, materials, col, math.radians(0))
    angled_l.rotation_euler[2] = math.radians(0)
    make_wall_panel("WallPanel_Angled_Right", (0.8, 3.5, 1.4), 3.0, 2.2, materials, col)

    create_cube("Wall_Post_ThickCorner", (4.0, 3.45, 1.4), (0.46, 0.46, 2.8), dark, 0.08, col)
    create_cube("Wall_Post_VerticalSupport", (5.0, 3.45, 1.4), (0.32, 0.38, 2.55), dark, 0.065, col)
    create_cube("Wall_TopRail_Straight", (6.4, 3.45, 2.75), (2.2, 0.42, 0.32), dark, 0.065, col)
    create_cube("Wall_Baseboard_Trim", (6.4, 3.45, 0.16), (2.2, 0.34, 0.28), dark, 0.055, col)
    create_cube("Wall_CornerBlock_Cap", (7.9, 3.45, 2.78), (0.52, 0.52, 0.42), dark, 0.075, col)

    bracket = create_cube("Wall_LanternHook_Bracket", (5.05, 2.95, 2.15), (0.16, 0.75, 0.12), metal, 0.025, col)
    bracket.rotation_euler[0] = math.radians(0)
    create_cylinder("Wall_StringLight_Peg", (5.65, 2.96, 2.25), 0.07, 0.16, metal, 12, 0.01, col)
    create_cylinder("Wall_GlowBulb_Sample", (5.65, 2.84, 2.1), 0.12, 0.16, glow, 16, 0.01, col)
    create_cube("Wall_Banner_HangingBar", (6.75, 2.95, 2.05), (1.0, 0.12, 0.12), metal, 0.025, col)


def make_room_shell_sample(materials):
    col = create_collection("D_RoomShell_Assembled_Sample")
    floor_mat = materials["wood_warm"]
    dark = materials["wood_dark"]

    # Floor patch.
    for row in range(8):
        for piece in range(7):
            length = 1.5 if (piece + row) % 2 else 1.9
            obj = create_cube(
                f"RoomFloor_Plank_{row+1}_{piece+1}",
                (-4.8 + piece * 1.35 + (0.4 if row % 2 else 0.0), -2.9 + row * 0.42, -0.02),
                (length, 0.38, 0.14),
                floor_mat,
                0.03,
                col,
            )
            obj.rotation_euler[2] = math.radians(random.uniform(-0.6, 0.6))

    # Back and angled side walls.
    make_wall_panel("RoomBackWall_Left", (-3.4, 1.15, 1.25), 3.0, 2.0, materials, col)
    make_wall_panel("RoomBackWall_Center", (0.0, 1.15, 1.25), 3.0, 2.0, materials, col)
    make_wall_panel("RoomBackWall_Right", (3.4, 1.15, 1.25), 3.0, 2.0, materials, col)

    left = make_wall_panel("RoomSideWall_Left", (-5.45, -1.0, 1.25), 3.2, 2.0, materials, col)
    right = make_wall_panel("RoomSideWall_Right", (5.45, -1.0, 1.25), 3.2, 2.0, materials, col)
    for obj in col.objects:
        if obj.name.startswith("RoomSideWall_Left"):
            obj.rotation_euler[2] = math.radians(25)
        if obj.name.startswith("RoomSideWall_Right"):
            obj.rotation_euler[2] = math.radians(-25)

    # Front edge trim, open for the camera.
    create_cube("RoomShell_OpenFront_DarkFloorLip", (0, -3.3, 0.12), (9.8, 0.3, 0.28), dark, 0.06, col)


def add_lights_and_camera():
    bpy.ops.object.light_add(type="AREA", location=(0.0, -5.5, 7.5))
    light = bpy.context.object
    light.name = "Preview_AreaLight_Warm"
    light.data.energy = 550
    light.data.size = 7.0

    bpy.ops.object.camera_add(location=(0, -9.5, 7.2), rotation=(math.radians(60), 0, 0))
    cam = bpy.context.object
    bpy.context.scene.camera = cam
    cam.name = "Preview_OrthographicCamera"
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 10.5


def render_preview():
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.eevee.taa_render_samples = 48
    scene.view_settings.view_transform = "Filmic"
    scene.view_settings.look = "Medium High Contrast"
    scene.world.color = (0.78, 0.68, 0.55)
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)


def export_assets():
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    try:
        bpy.ops.preferences.addon_enable(module="io_scene_gltf2")
    except Exception:
        pass
    try:
        bpy.ops.preferences.addon_enable(module="io_scene_fbx")
    except Exception:
        pass

    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format="GLB", export_apply=True)
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=False, apply_unit_scale=True, bake_space_transform=False)


def main():
    ensure_dirs()
    clear_scene()

    warm_img = make_texture(
        "HubWood_Warm_Planks",
        os.path.join(TEXTURE_DIR, "HubWood_Warm_Planks.png"),
        (0.42, 0.20, 0.075),
        (0.86, 0.48, 0.20),
        (0.16, 0.075, 0.035),
        17,
    )
    dark_img = make_texture(
        "HubWood_Dark_Frame",
        os.path.join(TEXTURE_DIR, "HubWood_Dark_Frame.png"),
        (0.18, 0.075, 0.035),
        (0.42, 0.18, 0.075),
        (0.075, 0.03, 0.015),
        41,
    )
    wall_img = make_texture(
        "HubWall_Warm_Planks",
        os.path.join(TEXTURE_DIR, "HubWall_Warm_Planks.png"),
        (0.36, 0.17, 0.08),
        (0.72, 0.39, 0.19),
        (0.13, 0.055, 0.025),
        79,
    )

    materials = {
        "wood_warm": material_with_texture("M_Hub_WarmWood_UV", warm_img),
        "wood_dark": material_with_texture("M_Hub_DarkWood_UV", dark_img, (0.88, 0.78, 0.68)),
        "wall_warm": material_with_texture("M_Hub_WallPlanks_UV", wall_img, (1.0, 0.92, 0.82)),
        "seam": simple_material("M_Hub_DarkGroove", (0.07, 0.035, 0.018, 1.0)),
        "metal": simple_material("M_Hub_AgedGoldMetal", (0.86, 0.54, 0.18, 1.0)),
        "glow_orange": simple_material(
            "M_Hub_OrangeGlow",
            (1.0, 0.43, 0.06, 1.0),
            (1.0, 0.34, 0.02, 1.0),
            1.3,
        ),
    }

    make_floor_kit(materials)
    make_wall_kit(materials)
    make_room_shell_sample(materials)
    add_lights_and_camera()
    render_preview()
    export_assets()


if __name__ == "__main__":
    main()
