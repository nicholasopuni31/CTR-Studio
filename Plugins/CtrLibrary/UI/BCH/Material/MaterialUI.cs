﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using ImGuiNET;
using MapStudio.UI;
using OpenTK.Graphics.OpenGL;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrGfx.Model.Material;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Math3D;
using SPICA.PICA.Commands;
using Toolbox.Core.ViewModels;
using SPICA.Formats.CtrH3D.Model.Mesh;
using CtrLibrary.Rendering;
using CtrLibrary.Bch;

namespace CtrLibrary
{
    internal class MaterialUI
    {
        TabControl TabControl = new TabControl("material_menu1");

        MaterialLutUI LutGUI = new MaterialLutUI();

        MaterialRenderStateUI RenderStateUI = new MaterialRenderStateUI();
        MaterialTextureUI MaterialTextureUI = new MaterialTextureUI();
        MaterialCombinerUI MaterialCombinerUI = new MaterialCombinerUI();
        UserDataInfoEditor UserDataInfoEditor = new UserDataInfoEditor();

        H3DMaterial Material;
        private H3DModel GfxModel;

        private MaterialWrapper UINode;

        List<H3DMesh> MappedMeshes = new List<H3DMesh>();

        private int VertexProgramIndex;
        private string VertexShaderName;
        private string VertexProgramName;

        public void Init(MaterialWrapper wrapper, H3DModel model, H3DMaterial material)
        {
            UINode = wrapper;
            GfxModel = model;
            Material = material;
            PrepareTabs();

            string[] shader = material.MaterialParams.ShaderReference.Split('@');
            if (shader.Length == 2)
            {
                if (int.TryParse(shader[0], out int programID))
                    VertexProgramIndex = programID;
                else
                    VertexProgramName = shader[0];

                VertexShaderName = shader[1];
            }
            else
                VertexShaderName = shader.FirstOrDefault();

            MappedMeshes.Clear();
            foreach (var mesh in GfxModel.Meshes)
            {
                if (GfxModel.Materials[mesh.MaterialIndex].Name == Material.Name)
                    MappedMeshes.Add(mesh);
            }

            RenderStateUI.Material = material;
            RenderStateUI.MaterialWrapper = wrapper;
            LutGUI.Init(material, wrapper);
            MaterialCombinerUI.Init(wrapper, material);

            MaterialTextureUI.Init(UINode, model, material);
        }

        private void PrepareTabs()
        {
            TabControl.TabMode = TabControl.Mode.Horizontal_Tabs;
            TabControl.Pages.Clear();
            TabControl.Pages.Add(new TabPage($"   {'\uf0ad'}    Info", () =>
            {
                bool hasPolygonOffset = Material.MaterialParams.IsPolygonOffsetEnabled;
                bool update = false;

                if (ImGui.CollapsingHeader("Polygon Info", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    update |= ImguiCustomWidgets.ComboScrollable("Face Culling", Material.MaterialParams.FaceCulling.ToString(), ref Material.MaterialParams.FaceCulling);
                    if (ImGui.Checkbox("Polygon Offset", ref hasPolygonOffset))
                    {
                        if (hasPolygonOffset)
                            Material.MaterialParams.Flags |= H3DMaterialFlags.IsPolygonOffsetEnabled;
                        else
                            Material.MaterialParams.Flags &= ~H3DMaterialFlags.IsPolygonOffsetEnabled;
                    }

                    if (hasPolygonOffset)
                        ImGui.DragFloat("Polygon Offset Unit", ref Material.MaterialParams.PolygonOffsetUnit);
                }
                if (ImGui.CollapsingHeader("Fog", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGuiHelper.InputFromBoolean("Enable Fog", Material.MaterialParams, "IsFogEnabled");
                    if (Material.MaterialParams.IsFogEnabled)
                    {
                        int fogID = Material.MaterialParams.FogIndex;
                        if (ImGui.InputInt("", ref fogID))
                            Material.MaterialParams.FogIndex = (ushort)fogID;
                    }
                }
                if (ImGui.CollapsingHeader("Lights", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    int lightSetID = Material.MaterialParams.LightSetIndex;
                    if (ImGui.InputInt("", ref lightSetID))
                        Material.MaterialParams.LightSetIndex = (ushort)lightSetID;
                }
                if (ImGui.CollapsingHeader("User Data", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    //Check if bcres node
                    if (UINode is Bcres.MTOB)
                    {
                        var node = UINode as Bcres.MTOB;
                        Bcres.UserDataInfoEditor.Render(node.GfxMaterial.MetaData);
                    }
                    else
                        UserDataInfoEditor.Render(Material.MaterialParams.MetaData);
                }

                if (update)
                    GLFrameworkEngine.GLContext.ActiveContext.UpdateViewport = true;
            }));
            TabControl.Pages.Add(new TabPage($"   {'\uf302'}    Textures", () =>
            {
                MaterialTextureUI.Render();
            }));
            TabControl.Pages.Add(new TabPage($"   {'\uf53f'}    Colors", () =>
            {
                DrawColorPage();
            }));
            TabControl.Pages.Add(new TabPage($"   {'\uf06e'}    Rendering", () =>
            {
                RenderStateUI.Render();
            }));
            TabControl.Pages.Add(new TabPage($"   {'\uf201'}    LUTs", () =>
            {
                LutGUI.Render();
            }));
            TabControl.Pages.Add(new TabPage($"   {'\uf5fd'}    Combiners", () =>
            {
                MaterialCombinerUI.Render();
              //  DrawCombinersPage();
            }));
            TabControl.Pages.Add(new TabPage($"   {'\uf61f'}    Vertex Shader", () =>
            {
                bool updateShaders = false;

                updateShaders |= ImGuiHelper.InputFromBoolean("Enable Vertex Lighting", Material.MaterialParams, "IsVertexLightingEnabled");
                updateShaders |= ImGuiHelper.InputFromBoolean("Enable Hemisphere Lighting", Material.MaterialParams, "IsHemiSphereLightingEnabled");
                if (Material.MaterialParams.IsHemiSphereLightingEnabled)
                {
                    updateShaders |= ImGuiHelper.InputFromBoolean("Enable Hemisphere Occlusion", Material.MaterialParams, "IsHemiSphereOcclusionEnabled");
                }

                bool updateShaderName = false;

                updateShaderName |= ImGui.InputText("Shader", ref VertexShaderName, 0x200);
                if (!string.IsNullOrEmpty(VertexProgramName))
                {
                    updateShaderName |= ImGui.InputText("Program", ref VertexProgramName, 0x200);
                }

                updateShaderName |= ImGui.InputInt("Program Index", ref VertexProgramIndex);

                if (updateShaderName)
                {
                    if (string.IsNullOrEmpty(VertexProgramName))
                        Material.MaterialParams.ShaderReference = $"{VertexProgramIndex}@{VertexShaderName}";
                    else
                        Material.MaterialParams.ShaderReference = $"{VertexProgramName}@{VertexShaderName}";
                    updateShaders = true;
                }

                DrawBcresShaderParams(Material.BcresShaderParams);

                if (updateShaders)
                    UpdateShaders();
            }));
        }

        public void Render()
        {
            DrawInfoPanel();

            TabControl.Render();    
        }

        private bool keepTextures = true;
        private bool openPresetSelector;
        private string _savePresetName = "";

        MaterialPresetSelectionDialog Preset = new MaterialPresetSelectionDialog();

        void DrawInfoPanel()
        {
            string name = Material.Name;
            bool updateShaders = false;

            ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(
                  ThemeHandler.Theme.FrameBg.X,
                  ThemeHandler.Theme.FrameBg.Y,
                  ThemeHandler.Theme.FrameBg.Z, 255));

            if (ImGui.Button("Preset Selector"))
            {
                Preset.Output = "";
                Preset.LoadMaterialPresets();
                openPresetSelector = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Save As Preset"))
            {
                if (string.IsNullOrEmpty(_savePresetName))
                    TinyFileDialog.MessageBoxInfoOk("A preset name is required for saving!");
                else
                    UINode.ExportPreset(_savePresetName);
            }
            ImGui.SameLine();

            ImGui.PushItemWidth(250);
            if (ImGui.InputText("Preset Name", ref _savePresetName, 0x200))
            {
            }
            ImGui.PopItemWidth();



            if (openPresetSelector)
            {
                if (Preset.Render("", ref openPresetSelector))
                {
                    UINode.ImportPresetBatch(Preset.Output);
                }
            }

            ImGui.PopStyleColor();

            ImGuiHelper.Tooltip("In tool presets to define how you want the material to look like.");

            if (ImGui.InputText("Name", ref name, 0x200))
            {
                Material.Name = name;
                UINode.Header = name;
            }

            if (ImGuiHelper.InputFromBoolean("Enable Fragment Lighting", Material.MaterialParams, "IsFragmentLightingEnabled"))
            {
                UINode.UpdateShaders();
                GLFrameworkEngine.GLContext.ActiveContext.UpdateViewport = true;
            }
            foreach (var stage in Material.MaterialParams.TexEnvStages)
            {
                if (stage.IsColorPassThrough)
                    continue;

                if (stage.Source.Color[0] == PICATextureCombinerSource.FragmentPrimaryColor ||
                    stage.Source.Color[1] == PICATextureCombinerSource.FragmentPrimaryColor ||
                    stage.Source.Color[2] == PICATextureCombinerSource.FragmentPrimaryColor ||
                    stage.Source.Color[0] == PICATextureCombinerSource.FragmentSecondaryColor ||
                    stage.Source.Color[1] == PICATextureCombinerSource.FragmentSecondaryColor ||
                    stage.Source.Color[2] == PICATextureCombinerSource.FragmentSecondaryColor)
                {
                    if (!Material.MaterialParams.Flags.HasFlag(H3DMaterialFlags.IsFragmentLightingEnabled))
                    {
                        ImGuiHelper.BoldText($"Combiner stage has pixel/specular used but no fragment lighting enabled!");
                        break;
                    }
                }
            }
        }

        void DrawBcresShaderParams(List<GfxShaderParam> Params)
        {
            for (int i = 0; i < Params.Count; i++)
            {
                if (ImGui.CollapsingHeader($"Param {i}", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.InputText("Name", ref Params[i].Name, 0x200);

                    BcresUIHelper.DrawEnum("Type", ref Params[i].Type);

                    switch (Params[i].Type)
                    {
                        case GfxShaderParam.ParamType.Boolean:
                            bool b = ((bool[])Params[i].Value)[0];
                            ImGui.Checkbox($"##prmv{i}", ref b);
                            break;
                        case GfxShaderParam.ParamType.Float:
                        case GfxShaderParam.ParamType.Float2:
                        case GfxShaderParam.ParamType.Float3:
                        case GfxShaderParam.ParamType.Float4:
                            float[] f2 = ((float[])Params[i].Value);
                            BcresUIHelper.DrawFloatArray($"##prmv{i}", ref f2);
                            break;
                    }
                }
            }
        }

        void UpdateShaders()
        {
            UINode.UpdateShaders();
            GLFrameworkEngine.GLContext.ActiveContext.UpdateViewport = true;
        }

        void DrawColorPage()
        {
            if (ImGui.SliderFloat("Vertex Color Intensity", ref Material.MaterialParams.ColorScale, 0, 1))
            {
                BatchParamsEditField("ColorScale", Material.MaterialParams.ColorScale);
                UpdateShaders();
            }

            ImGui.Columns(3);

            DrawColorUI("Diffuse", "DiffuseColor"); ImGui.NextColumn();
            DrawColorUI("Emission", "EmissionColor"); ImGui.NextColumn();
            DrawColorUI("Ambient", "AmbientColor"); ImGui.NextColumn();
            DrawColorUI("Specular 0", "Specular0Color"); ImGui.NextColumn();
            DrawColorUI("Specular 1", "Specular1Color"); ImGui.NextColumn();
            DrawColorUI("Blend Color", "BlendColor"); ImGui.NextColumn();
            DrawColorUI("Constant 0", "Constant0Color"); ImGui.NextColumn();
            DrawColorUI("Constant 1", "Constant1Color"); ImGui.NextColumn();
            DrawColorUI("Constant 2", "Constant2Color"); ImGui.NextColumn();
            DrawColorUI("Constant 3", "Constant3Color"); ImGui.NextColumn();
            DrawColorUI("Constant 4", "Constant4Color"); ImGui.NextColumn();
            DrawColorUI("Constant 5", "Constant5Color"); ImGui.NextColumn();

            ImGui.Columns(1);
        }

        void DrawColorUI(string name, string fieldName)
        {
            var prop = Material.MaterialParams.GetType().GetField(fieldName);
            var rgba = (RGBA)prop.GetValue(Material.MaterialParams);

            var color = new Vector4(
                rgba.R / 255.0f,
                rgba.G / 255.0f,
                rgba.B / 255.0f,
                rgba.A / 255.0f);

            if (ImGui.ColorEdit4(name, ref color, ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoInputs))
            {
                rgba = new RGBA(
                    (byte)(color.X * 255),
                    (byte)(color.Y * 255),
                    (byte)(color.Z * 255),
                    (byte)(color.W * 255));
                BatchParamsEditField(fieldName, rgba);
                GLFrameworkEngine.GLContext.ActiveContext.UpdateViewport = true;
            }
        }

        void BatchParamsEditField(string fieldName, object value)
        {
            var parent = this.UINode.Parent;
            foreach (MaterialWrapper selected in parent.Children.Where(x => x.IsSelected))
            {
                var prop = selected.Material.MaterialParams.GetType().GetField(fieldName);
                prop.SetValue(selected.Material.MaterialParams, value);
            }
        }

        void UpdateUniforms()
        {
            Material.UpdateShaderUniforms?.Invoke(Material, EventArgs.Empty);
            GLFrameworkEngine.GLContext.ActiveContext.UpdateViewport = true;
        }
    }
}
