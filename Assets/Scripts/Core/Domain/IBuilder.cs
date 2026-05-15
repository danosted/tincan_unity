#nullable enable
using UnityEngine;
using TinCan.Core.Domain;
using System;

namespace TinCan.Features.Airship
{
    public interface IBuilder
    {
        GameObject? SelectedModulePrefab { get; set; }
    }
}
