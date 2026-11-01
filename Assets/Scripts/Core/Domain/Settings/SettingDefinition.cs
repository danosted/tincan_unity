#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain.Settings
{
    /// <summary>
    /// One player setting: its key, how it is edited and its default. Core settings are listed on the settings installer;
    /// a feature contributes its own through <c>FeatureInstaller.IExtension&lt;SettingDefinition&gt;</c>, so a setting
    /// exists only where its feature is loaded. Read values through <see cref="IPlayerSettings"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Setting_New", menuName = "TinCan/Settings/Setting Definition")]
    public sealed class SettingDefinition : ScriptableObject
    {
        [Tooltip("Stable id, lower case with dots: look.sensitivity. Saved values are keyed by it.")]
        [SerializeField] private string _key = string.Empty;
        [SerializeField] private string _label = string.Empty;
        [SerializeField] private SettingGroup _group;
        [SerializeField] private SettingKind _kind;
        [Tooltip("Slider: the range and the step (0 for none).")]
        [SerializeField] private float _min;
        [SerializeField] private float _max = 1f;
        [SerializeField] private float _step;
        [Tooltip("Choice: the options, as they are shown and saved.")]
        [SerializeField] private string[] _options = Array.Empty<string>();
        [Tooltip("Text: the longest value kept.")]
        [SerializeField] private int _maxLength = 32;
        [Tooltip("As text: 1.0, True, an option, or the text itself.")]
        [SerializeField] private string _default = string.Empty;

        public string Key => _key;
        public string Label => string.IsNullOrEmpty(_label) ? _key : _label;
        public SettingGroup Group => _group;
        public SettingKind Kind => _kind;
        public float Min => _min;
        public float Max => _max;
        public float Step => _step;
        public IReadOnlyList<string> Options => _options;
        public int MaxLength => _maxLength;
        public string Default => _default;

        /// <summary>Test and tooling helper: a slider in code.</summary>
        public static SettingDefinition Slider(string key, string label, SettingGroup group, float min, float max, float step, float defaultValue) =>
            Create(key, label, group, SettingKind.Slider, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture), d =>
            {
                d._min = min;
                d._max = max;
                d._step = step;
            });

        public static SettingDefinition Toggle(string key, string label, SettingGroup group, bool defaultValue) =>
            Create(key, label, group, SettingKind.Toggle, defaultValue ? bool.TrueString : bool.FalseString, _ => { });

        public static SettingDefinition Choice(string key, string label, SettingGroup group, string defaultValue, params string[] options) =>
            Create(key, label, group, SettingKind.Choice, defaultValue, d => d._options = options);

        public static SettingDefinition Text(string key, string label, SettingGroup group, string defaultValue, int maxLength) =>
            Create(key, label, group, SettingKind.Text, defaultValue, d => d._maxLength = maxLength);

        private static SettingDefinition Create(string key, string label, SettingGroup group, SettingKind kind, string defaultValue, Action<SettingDefinition> shape)
        {
            var definition = CreateInstance<SettingDefinition>();
            definition.name = key;
            definition._key = key;
            definition._label = label;
            definition._group = group;
            definition._kind = kind;
            definition._default = defaultValue;
            shape(definition);
            return definition;
        }
    }
}
