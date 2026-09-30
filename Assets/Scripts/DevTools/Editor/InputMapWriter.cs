#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// TinCan > Dev > Input > Write Input Map: writes .docs/INPUT_MAP.md, the answer to "what listens to what", from
    /// the assets and the code itself: every context (when it is live, what it silences, which classes read it, who
    /// contributes it), its actions with their default keys, its routes with the handler that runs, and the ability-input
    /// bits. Readers are the classes whose constructor takes the context's type; handlers are found by command type.
    /// <c>InputMapTests</c> fails while the file is stale.
    /// </summary>
    public static class InputMapWriter
    {
        public const string DocPath = ".docs/INPUT_MAP.md";
        private const string ConfigPath = "Assets/Input/InputConfig.asset";

        [MenuItem("TinCan/Dev/Input/Write Input Map")]
        public static void Write()
        {
            File.WriteAllText(Path.Combine(ProjectRoot, DocPath), Build());
            Debug.Log($"[InputMapWriter] Wrote {DocPath}.");
        }

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)!;

        public static string Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<InputConfig>(ConfigPath) ?? throw new InvalidOperationException($"Missing {ConfigPath}.");
            var contexts = AssetDatabase.FindAssets("t:" + nameof(InputContext))
                .Select(guid => AssetDatabase.LoadAssetAtPath<InputContext>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(context => context != null)
                .OrderByDescending(context => context.Priority)
                .ThenBy(context => context.name, StringComparer.Ordinal)
                .ToList();
            var types = TinCanTypes();

            var text = new StringBuilder();
            text.AppendLine("# Input map");
            text.AppendLine();
            text.AppendLine("Generated from the input assets by **TinCan > Dev > Input > Write Input Map**. Do not edit it; rerun the menu");
            text.AppendLine("item after changing an action, a context, a route or a handler (`InputMapTests` fails while this file is stale).");
            text.AppendLine("The model is in [INPUT.md](INPUT.md).");
            text.AppendLine();
            text.AppendLine("## Contexts");
            text.AppendLine();
            text.AppendLine("Highest priority first. Every frame each context whose condition holds is live unless a live context above");
            text.AppendLine("silences it; an action outside every live context reads as released.");
            text.AppendLine();
            text.AppendLine("| Priority | Context | Live when | Silences | Read by | From |");
            text.AppendLine("|---|---|---|---|---|---|");
            foreach (var context in contexts)
            {
                text.AppendLine($"| {context.Priority} | {context.name} | {Condition(context)} | {Silences(context)} | {ReadBy(context, types)} | {Source(context, config)} |");
            }

            foreach (var context in contexts)
            {
                text.AppendLine();
                text.AppendLine($"### {context.name}");
                text.AppendLine();
                if (!string.IsNullOrEmpty(context.Description)) text.AppendLine(Flatten(context.Description)).AppendLine();

                var actions = context.Actions.Distinct().ToList();
                if (actions.Count > 0)
                {
                    text.AppendLine("| Action | Default keys | Meaning | Rebindable |");
                    text.AppendLine("|---|---|---|---|");
                    foreach (var action in actions)
                    {
                        text.AppendLine($"| {action.Path} | {DefaultKeys(config, action)} | {Flatten(action.Description)} | {(action.Rebindable ? "yes" : "no")} |");
                    }
                }

                var routes = context.Routes.Where(route => route.Action != null && route.Command != null).ToList();
                if (routes.Count > 0)
                {
                    text.AppendLine();
                    text.AppendLine("| Pressing | Runs | Handled by |");
                    text.AppendLine("|---|---|---|");
                    foreach (var route in routes)
                    {
                        text.AppendLine($"| {route.Action!.Path} | {route.Command!.name} | {HandlerOf(route.Command.GetType(), types)} |");
                    }
                }
            }

            text.AppendLine();
            text.AppendLine("## Ability inputs");
            text.AppendLine();
            text.AppendLine("Bits of the predicted input state, in the input config's order (the same on every peer). A bit is set while");
            text.AppendLine("any of its actions is pressed in a live context.");
            text.AppendLine();
            text.AppendLine("| Bit | Ability input | Pressed by |");
            text.AppendLine("|---|---|---|");
            for (int i = 0; i < config.GameplayInputs.Count; i++)
            {
                var input = config.GameplayInputs[i];
                if (input == null) continue;
                text.AppendLine($"| {i} | {input.name} | {string.Join(", ", input.Actions.Where(a => a != null).Select(a => a.Path))} |");
            }

            return text.ToString().Replace("\r\n", "\n");
        }

        private static string Condition(InputContext context) => context.Activation switch
        {
            InputContextActivation.Always => "always",
            InputContextActivation.WhilePossessing => $"possessing {context.PossessedKind}",
            InputContextActivation.WhilePossessedHasTag => $"possessed actor has {(context.Tag != null ? context.Tag.name : "?")}",
            InputContextActivation.WhileMenuOpen => "a menu is open",
            InputContextActivation.WhileRebinding => "waiting for a key",
            _ => context.Activation.ToString(),
        };

        private static string Silences(InputContext context)
        {
            var parts = new List<string>();
            if (context.BlocksAllLower) parts.Add("everything below");
            parts.AddRange(context.Blocks.Where(b => b != null).Select(b => b.name));
            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }

        private static string ReadBy(InputContext context, IReadOnlyList<Type> types)
        {
            var type = context.GetType();
            if (type == typeof(InputContext)) return "(routes only)";
            var readers = types
                .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == type)))
                .Select(t => t.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            return readers.Count == 0 ? "-" : string.Join(", ", readers);
        }

        private static string Source(InputContext context, InputConfig config)
        {
            if (config.Contexts.Contains(context)) return "core (InputConfig)";
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(FeatureInstaller)))
            {
                var installer = AssetDatabase.LoadAssetAtPath<FeatureInstaller>(AssetDatabase.GUIDToAssetPath(guid));
                if (installer is not FeatureInstaller.IExtension<InputContext>) continue;
                var field = new SerializedObject(installer).FindProperty("_controls");
                if (field != null && field.objectReferenceValue == context) return installer.name;
            }
            return "not loaded";
        }

        private static string HandlerOf(Type command, IReadOnlyList<Type> types)
        {
            var handler = types.FirstOrDefault(t => !t.IsAbstract && Inherits(t, typeof(InputCommandHandler<>).MakeGenericType(command)));
            return handler?.Name ?? "**none**";
        }

        private static bool Inherits(Type type, Type baseType)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current == baseType) return true;
            }
            return false;
        }

        private static string DefaultKeys(InputConfig config, InputActionId id)
        {
            if (config.Actions == null || !Guid.TryParse(id.ActionId, out var guid) || config.Actions.FindAction(guid) is not { } action) return "?";
            var keys = action.bindings.Where(b => !b.isComposite).Select(b => Key(b.path)).Distinct();
            return string.Join(" / ", keys);
        }

        private static string Key(string path) => "`" + path.Replace("<Keyboard>/", string.Empty).Replace("<Mouse>/", "mouse ") + "`";

        private static string Flatten(string text) => text.Replace("\r", string.Empty).Replace("\n", " ").Replace("|", "/").Trim();

        private static IReadOnlyList<Type> TinCanTypes() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("TinCan.", StringComparison.Ordinal) && !a.GetName().Name.StartsWith("TinCan.Tests", StringComparison.Ordinal))
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray()!; }
                })
                .Where(t => t.IsClass && !t.IsAbstract)
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();
    }
}
