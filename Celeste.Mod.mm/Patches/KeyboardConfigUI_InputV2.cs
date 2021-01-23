﻿#pragma warning disable CS0169 // The field is never used
#pragma warning disable CS0414 // The field is assigned to, but never used
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using MonoMod;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste {
    [MonoModIfFlag("V2:Input")]
    [MonoModPatch("KeyboardConfigUI")]
    public class patch_KeyboardConfigUI_InputV2 : KeyboardConfigUI {

        [MonoModIgnore]
        private bool remapping;
        [MonoModIgnore]
        private float remappingEase;
        [MonoModIgnore]
        private Binding remappingBinding;
        [MonoModIgnore]
        private string remappingText;
        [MonoModIgnore]
        private float inputDelay;
        [MonoModIgnore]
        private float timeout;
        [MonoModIgnore]
        private bool closing;
        [MonoModIgnore]
        private float closingDelay;
        [MonoModIgnore]
        private bool resetting;

        private int currentlyRemapping;

#pragma warning disable CS0626 // method is external and has no attribute
        public extern void orig_ctor();
#pragma warning restore CS0626

        [MonoModConstructor]
        public void ctor() {
            orig_ctor();
            Reload();
        }

        /// <summary>
        /// Gets the label to display on-screen for a mapping.
        /// </summary>
        /// <param name="mapping">The mapping index</param>
        /// <returns>The key name to display</returns>
        [Obsolete("This method exists so that older mods can still be loaded but is no longer used.")]
        protected virtual string GetLabel(int mapping) {
            return "KEY_CONFIG_USING_OBSOLETE_API";
        }

        /// <summary>
        /// Adds a key mapping to the keyboard config screen.
        /// </summary>
        /// <param name="key">The mapping index</param>
        /// <param name="list">The list of keys currently mapped to it</param>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected void AddKeyConfigLine(int key, List<Keys> list) {
            Add(new patch_TextMenu.Setting(GetLabel(key), list).Pressed(() => Remap(key)));
        }

        /// <summary>
        /// Adds a key mapping to the keyboard config screen.
        /// </summary>
        /// <param name="key">The mapping (should be an enum value)</param>
        /// <param name="list">The list of keys currently mapped to it</param>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected void AddKeyConfigLine<T>(T key, List<Keys> list) where T : Enum {
            AddKeyConfigLine(key.GetHashCode(), list);
        }

        /// <summary>
        /// Forces a key to be bound to an action, in addition to the already bound key.
        /// </summary>
        /// <param name="defaultKey">The key to force bind</param>
        /// <param name="boundKey">The key already bound</param>
        /// <returns>A list containing both key and defaultKey</returns>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected List<Keys> ForceDefaultKey(Keys defaultKey, Keys boundKey) {
            List<Keys> list = new List<Keys> { boundKey };
            if (boundKey != defaultKey)
                list.Add(defaultKey);
            return list;
        }

        /// <summary>
        /// Forces a key to be bound to an action, in addition to already bound keys.
        /// </summary>
        /// <param name="defaultKey">The key to force bind</param>
        /// <param name="boundKeys">The list of keys already bound</param>
        /// <returns>A list containing both keys in list and defaultKey</returns>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected List<Keys> ForceDefaultKey(Keys defaultKey, List<Keys> boundKeys) {
            if (!boundKeys.Contains(defaultKey))
                boundKeys.Add(defaultKey);
            return boundKeys;
        }

        /// <summary>
        /// Rebuilds the key mapping menu. Should clear the menu and add back all options.
        /// </summary>
        /// <param name="index">The index to focus on in the menu</param>
        public virtual void Reload(int index = -1) {
            Clear();
            Add(new Header(Dialog.Clean("KEY_CONFIG_TITLE")));
            Add(new InputMappingInfo(false));

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_MOVEMENT")));
            AddMap("LEFT", Settings.Instance.Left);
            AddMap("RIGHT", Settings.Instance.Right);
            AddMap("UP", Settings.Instance.Up);
            AddMap("DOWN", Settings.Instance.Down);

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_GAMEPLAY")));
            AddMap("JUMP", Settings.Instance.Jump);
            AddMap("DASH", Settings.Instance.Dash);
            AddMap("GRAB", Settings.Instance.Grab);
            AddMap("TALK", Settings.Instance.Talk);

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_MENUS")));
            AddMap("LEFT", Settings.Instance.MenuLeft);
            AddMap("RIGHT", Settings.Instance.MenuRight);
            AddMap("UP", Settings.Instance.MenuUp);
            AddMap("DOWN", Settings.Instance.MenuDown);
            AddMap("CONFIRM", Settings.Instance.Confirm);
            AddMap("CANCEL", Settings.Instance.Cancel);
            AddMap("JOURNAL", Settings.Instance.Journal);
            AddMap("PAUSE", Settings.Instance.Pause);
            AddMap("QUICKRESTART", Settings.Instance.QuickRestart);

            Add(new SubHeader(Dialog.Clean("KEY_CONFIG_ADVANCED")));
            AddMap("DEMODASH", Settings.Instance.DemoDash);

            Add(new SubHeader(""));
            Add(new Button(Dialog.Clean("KEY_CONFIG_RESET")) {
                IncludeWidthInMeasurement = false,
                AlwaysCenter = true,
                OnPressed = () => {
                    Settings.Instance.SetDefaultKeyboardControls(reset: true);
                    Input.Initialize();
                    Reload(Selection);
                }
            });

            if (index >= 0) {
                Selection = index;
            }
        }

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void AddMap(string label, Binding binding);

        public void AddMapForceLabel(string label, Binding binding) {
            Add(new Setting(label, binding, false).Pressed(() => {
                remappingText = label;
                Remap(binding);
            }).AltPressed(() => {
                ClearRemap(binding);
            }));
        }

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void Remap(Binding binding);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void ClearRemap(Binding binding);

        [MonoModIgnore]
        [MakeMethodPublic]
        public extern void AddRemap(Keys key);

        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        private void Remap(int mapping) {
            List<Keys> keys = GetRemapList(mapping, (Keys) (-1));
            if (keys == null)
                throw new Exception($"{GetType().FullName} is using the old input system (before Celeste 1.3.3.12) and Everest can't bridge the gap for this mod.");

            remapping = true;
            currentlyRemapping = mapping;
            remappingBinding = new Binding() {
                Keyboard = keys
            };
            remappingText = GetLabel(mapping);
            timeout = 5f;
            Focused = false;
        }

        /// <summary>
        /// Determines if the key being set supports multiple bindings.
        /// If this is the case, Shift+Confirm will allow to add a binding and Confirm will replace existing bindings.
        /// </summary>
        /// <param name="mapping">The mapping</param>
        /// <returns>true if the key supports multiple bindings, false otherwise</returns>
        [Obsolete("This method exists so that older mods can still be loaded but is no longer used.")]
        protected virtual bool SupportsMultipleBindings(int mapping) {
            return true;
        }

        /// <summary>
        /// Returns the list used to remap keys during a remap operation.
        /// This should be a List&lt;Keys&gt; field in your settings class
        /// </summary>
        /// <param name="remapping">The int value of the mapping being remapped</param>
        /// <param name="newKey">The new key that the user is attempting to set.</param>
        /// <returns>the field to set keys with, otherwise return null to cancel the operation</returns>
        [Obsolete("This method exists so that older mods can still be loaded but should no longer be used.")]
        protected virtual List<Keys> GetRemapList(int remapping, Keys newKey) {
            return null;
        }

    }
}