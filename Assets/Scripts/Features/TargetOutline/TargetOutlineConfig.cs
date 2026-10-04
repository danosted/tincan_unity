#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.TargetOutline
{
    /// <summary>
    /// Tunables for the outline on the local player's Interact target. Shared by the installer (the presenter marks the
    /// target's renderers) and the URP renderer feature on _MainURPRenderer (it draws them), so both use one rendering
    /// layer. <see cref="Highlighted"/> is runtime state, not saved: the renderer feature skips its passes while it is 0.
    /// </summary>
    [CreateAssetMenu(fileName = "TargetOutlineConfig", menuName = "TinCan/Interaction/Target Outline Config")]
    public class TargetOutlineConfig : ScriptableObject
    {
        [Tooltip("Hidden/TinCan/TargetOutline: referenced here so builds include it.")]
        public Shader? Shader;

        public Color Color = new(1f, 0.85f, 0.35f, 1f);

        [Tooltip("Outline width in screen pixels.")]
        [Range(1f, 8f)] public float Width = 3f;

        [Tooltip("The rendering layer (0-31) a targeted renderer joins while outlined. Keep it free of other uses.")]
        [Range(0, 31)] public int RenderingLayer = 30;

        /// <summary>Runtime: how many renderers are outlined now (0 skips the renderer feature's passes).</summary>
        [NonSerialized] public int Highlighted;

        public uint LayerBit => 1u << RenderingLayer;
    }
}
