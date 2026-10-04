#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>Ship design tunables: the size limits and the designs that ship with the game.</summary>
    [CreateAssetMenu(fileName = "ShipDesignsConfig", menuName = "TinCan/Ship/Ship Designs Config")]
    public class ShipDesignsConfig : ScriptableObject
    {
        [Header("Limits")]
        public ShipDesignLimits Limits = ShipDesignLimits.Default;

        [Header("Flight")]
        [Tooltip("How a design's mass, lift and thrust become speed and turning (ShipStatsProcessor).")]
        public ShipStatsTuning Flight = ShipStatsTuning.Default;

        [Header("Designs")]
        [Tooltip("Read-only designs that ship with the game (*.ship.json TextAssets under Assets/Settings/ShipDesigns/).")]
        public List<TextAsset> BuiltInDesigns = new();
        [Tooltip("The design a ship is built from when nothing else is chosen.")]
        public string DefaultDesign = "Starter";
    }
}
