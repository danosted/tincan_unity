#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.Features.Shipyard.UI
{
    /// <summary>
    /// One part in the shipyard's parts panel: its picture and name, outlined in the accent while selected
    /// (PartTile.uss, contributed by the shipyard installer).
    /// </summary>
    [UxmlElement]
    public partial class PartTile : Button
    {
        public static readonly string UssClassName = "tc-part-tile";
        public static readonly string SelectedUssClassName = UssClassName + "--selected";
        public static readonly string PictureUssClassName = UssClassName + "__picture";
        public static readonly string NameUssClassName = UssClassName + "__name";

        private readonly VisualElement _picture = new();
        private readonly Label _name = new();

        public PartTile() : this(string.Empty, null) { }

        public PartTile(string name, Action? clicked) : base(clicked)
        {
            AddToClassList(UssClassName);
            _picture.AddToClassList(PictureUssClassName);
            _name.AddToClassList(NameUssClassName);
            Add(_picture);
            Add(_name);
            PartName = name;
        }

        [UxmlAttribute]
        public string PartName
        {
            get => _name.text;
            set => _name.text = value;
        }

        [UxmlAttribute]
        public bool Selected
        {
            get => ClassListContains(SelectedUssClassName);
            set => EnableInClassList(SelectedUssClassName, value);
        }

        /// <summary>The part's rendered thumbnail (from data, so set in code).</summary>
        public void ShowPicture(RenderTexture texture) => _picture.style.backgroundImage = Background.FromRenderTexture(texture);
    }
}
