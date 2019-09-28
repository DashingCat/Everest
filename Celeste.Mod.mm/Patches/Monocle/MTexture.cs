﻿#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

using Celeste.Mod;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Monocle {
    class patch_MTexture : MTexture {

        public MTexture Parent;

        [MonoModIgnore]
        public new VirtualTexture Texture { get; set; }
        [MonoModIgnore]
        public new Vector2 DrawOffset { get; set; }
        [MonoModIgnore]
        public new Rectangle ClipRect { get; set; }
        [MonoModIgnore]
        public new int Width { get; set; }
        [MonoModIgnore]
        public new int Height { get; set; }

        // Needed for mods which were built against old versions.
        public string get_AtlasPath() => AtlasPath;

        private bool _HasOrig;
        private VirtualTexture _OrigTexture;
        private Rectangle _OrigClipRect;
        private Vector2 _OrigDrawOffset;
        private int _OrigWidth;
        private int _OrigHeight;

        private List<ModAsset> _ModAssets;

        // Patching constructors is ugly.
        public extern void orig_ctor(MTexture parent, int x, int y, int width, int height);
        [MonoModConstructor]
        public void ctor(MTexture parent, int x, int y, int width, int height) {
            orig_ctor(parent, x, y, width, height);
            Parent = parent;
        }

        public extern void orig_ctor(MTexture parent, string atlasPath, Rectangle clipRect, Vector2 drawOffset, int width, int height);
        [MonoModConstructor]
        public void ctor(MTexture parent, string atlasPath, Rectangle clipRect, Vector2 drawOffset, int width, int height) {
            orig_ctor(parent, atlasPath, clipRect, drawOffset, width, height);
            Parent = parent;
        }

        private extern void orig_SetUtil();
        private void SetUtil() {
            orig_SetUtil();

            if (_ScaleFix <= 0f)
                _ScaleFix = 1f;
        }

        public void SetAtlasPath(string path) {
            if (AtlasPath != null)
                return;
            AtlasPath = path;
        }

        public void SetOverride(VirtualTexture texture, Vector2 drawOffset, int frameWidth, int frameHeight) {
            if (!_HasOrig) {
                _OrigTexture = Texture;
                _OrigClipRect = ClipRect;
                _OrigDrawOffset = DrawOffset;
                _OrigWidth = Width;
                _OrigHeight = Height;
                _HasOrig = true;
            }

            Texture = texture;
            ClipRect = new Rectangle(0, 0, texture.Width, texture.Height);
            DrawOffset = drawOffset;
            Width = frameWidth;
            Height = frameHeight;
            SetUtil();
        }

        public void SetOverride(ModAsset asset) {
            if (_ModAssets == null)
                _ModAssets = new List<ModAsset>();

            ModAsset assetPrev = _ModAssets.Count == 0 ? null : _ModAssets[_ModAssets.Count - 1];
            if (assetPrev != asset) {
                _ModAssets.Add(asset);
                asset.Targets.Add(this);
            }

            VirtualTexture vtex = VirtualContentExt.CreateTexture(asset);
            MTextureMeta meta = asset.GetMeta<MTextureMeta>();

            if (meta != null) {
                // Apply width and height from meta.
                if (meta.Width == 0)
                    meta.Width = vtex.Width;
                if (meta.Height == 0)
                    meta.Height = vtex.Height;
                SetOverride(vtex, new Vector2(meta.X, meta.Y), meta.Width, meta.Height);

            } else if (vtex.Width == ClipRect.Width && vtex.Height == ClipRect.Height) {
                // Replacement is a subtexture. Keep drawoffset, width and height from existing instance.
                SetOverride(vtex, DrawOffset, Width, Height);

            } else {
                // Full texture replacement.
                SetOverride(vtex, new Vector2(0f, 0f), vtex.Width, vtex.Height);
            }
        }

        public void UndoOverride() {
            if (_ModAssets != null && _ModAssets.Count > 0) {
                _ModAssets.RemoveAt(_ModAssets.Count - 1);
                if (_ModAssets.Count > 0) {
                    SetOverride(_ModAssets[_ModAssets.Count - 1]);
                    return;
                }
            }

            if (!_HasOrig)
                return;

            Texture = _OrigTexture;
            _OrigTexture = null;
            ClipRect = _OrigClipRect;
            DrawOffset = _OrigDrawOffset;
            Width = _OrigWidth;
            Height = _OrigHeight;
            SetUtil();
            _HasOrig = false;
        }

        public void UndoOverride(ModAsset asset) {
            if (_ModAssets == null)
                return;

            int index = _ModAssets.IndexOf(asset);
            if (index == -1)
                return;

            if (index == _ModAssets.Count - 1) {
                UndoOverride();
                return;
            }

            _ModAssets.Remove(asset);
        }

        [MonoModReplace]
        public new Rectangle GetRelativeRect(int cx, int cy, int cw, int ch) {
            Rectangle parentRect = ClipRect;
            Vector2 parentOffset = DrawOffset;

            int parentX = parentRect.X;
            int parentY = parentRect.Y;
            int parentW = parentRect.Width;
            int parentH = parentRect.Height;
            int parentR = parentX + parentW;
            int parentB = parentY + parentH;

            int parentOX = (int) parentOffset.X;
            int parentOY = (int) parentOffset.Y;

            int a = parentX - parentOX + cx;
            int b = parentY - parentOY + cy;
            int x = Math.Max(parentX, Math.Min(a, parentR));
            int y = Math.Max(parentY, Math.Min(b, parentB));
            int w = Math.Max(0, Math.Min(a + cw, parentR) - x);
            int h = Math.Max(0, Math.Min(b + ch, parentB) - y);

            return new Rectangle(x, y, w, h);
        }

        #region Drawing Methods

        #region Draw-related fixes

        // fix = orig / new
        private float _ScaleFix;
        public float ScaleFix {
            get {
                if (Parent != null)
                    return ((patch_MTexture) Parent).ScaleFix * _ScaleFix;
                return _ScaleFix;
            }
            set {
                _ScaleFix = value;
            }
        }

        #endregion

        #region Draw

        [MonoModReplace]
        public new void Draw(Vector2 position) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, Color.White, 0f, -DrawOffset / scaleFix, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, Color.White, 0f, (origin - DrawOffset) / scaleFix, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (origin - DrawOffset) / scaleFix, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color, float scale) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (origin - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color, float scale, float rotation) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (origin - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color, float scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (origin - DrawOffset) / scaleFix, scale * scaleFix, flip, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color, Vector2 scale) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (origin - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color, Vector2 scale, float rotation) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (origin - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color, Vector2 scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (origin - DrawOffset) / scaleFix, scale * scaleFix, flip, 0f);
        }

        [MonoModReplace]
        public new void Draw(Vector2 position, Vector2 origin, Color color, Vector2 scale, float rotation, Rectangle clip) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, GetRelativeRect(clip), color, rotation, (origin - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        #endregion

        #region DrawCentered

        [MonoModReplace]
        public new void DrawCentered(Vector2 position) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, Color.White, 0f, (Center - DrawOffset) / scaleFix, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawCentered(Vector2 position, Color color) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (Center - DrawOffset) / scaleFix, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawCentered(Vector2 position, Color color, float scale) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (Center - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawCentered(Vector2 position, Color color, float scale, float rotation) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (Center - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawCentered(Vector2 position, Color color, float scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (Center - DrawOffset) / scaleFix, scale * scaleFix, flip, 0f);
        }

        [MonoModReplace]
        public new void DrawCentered(Vector2 position, Color color, Vector2 scale) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (Center - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawCentered(Vector2 position, Color color, Vector2 scale, float rotation) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (Center - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawCentered(Vector2 position, Color color, Vector2 scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (Center - DrawOffset) / scaleFix, scale * scaleFix, flip, 0f);
        }

        #endregion

        #region DrawJustified

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, Color.White, 0f, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify, Color color) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify, Color color, float scale) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify, Color color, float scale, float rotation) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify, Color color, float scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scale * scaleFix, flip, 0f);
        }

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify, Color color, Vector2 scale) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, 0f, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify, Color color, Vector2 scale, float rotation) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scale * scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawJustified(Vector2 position, Vector2 justify, Color color, Vector2 scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;            
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, ClipRect, color, rotation, (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix, scale * scaleFix, flip, 0f);
        }

        #endregion

        #region DrawOutline

        [MonoModReplace]
        public new void DrawOutline(Vector2 position) {
            float scaleFix = ScaleFix;                        
            Rectangle clip = ClipRect;
            Vector2 offset = -DrawOffset / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scaleFix, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, Color.White, 0f, offset, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin) {
            float scaleFix = ScaleFix;                        
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scaleFix, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, Color.White, 0f, offset, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin, Color color) {
            float scaleFix = ScaleFix;                        
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scaleFix, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin, Color color, float scale) {
            float scaleFix = ScaleFix;                        
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin, Color color, float scale, float rotation) {
            float scaleFix = ScaleFix;                        
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin, Color color, float scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;                        
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, flip, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, flip, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin, Color color, Vector2 scale) {
            float scaleFix = ScaleFix;                        
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin, Color color, Vector2 scale, float rotation) {
            float scaleFix = ScaleFix;                        
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutline(Vector2 position, Vector2 origin, Color color, Vector2 scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;                        
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (origin - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, flip, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, flip, 0f);
        }

        #endregion

        #region DrawOutlineCentered

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position) {
            float scaleFix = ScaleFix; Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scaleFix, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, Color.White, 0f, offset, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position, Color color) {
            float scaleFix = ScaleFix; Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scaleFix, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position, Color color, float scale) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position, Color color, float scale, float rotation) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position, Color color, float scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, flip, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, flip, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position, Color color, Vector2 scale) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position, Color color, Vector2 scale, float rotation) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineCentered(Vector2 position, Color color, Vector2 scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (Center - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, flip, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, flip, 0f);
        }

        #endregion

        #region DrawOutlineJustified

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify) {
            float scaleFix = ScaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scaleFix, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, Color.White, 0f, offset, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify, Color color) {
            float scaleFix = ScaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scaleFix, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scaleFix, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify, Color color, float scale) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify, Color color, float scale, float rotation) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify, Color color, float scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, flip, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, flip, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify, Color color, Vector2 scale) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, 0f, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, 0f, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify, Color color, Vector2 scale, float rotation) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, SpriteEffects.None, 0f);
        }

        [MonoModReplace]
        public new void DrawOutlineJustified(Vector2 position, Vector2 justify, Color color, Vector2 scale, float rotation, SpriteEffects flip) {
            float scaleFix = ScaleFix;
            scale *= scaleFix;
            Rectangle clip = ClipRect;
            Vector2 offset = (new Vector2(Width * justify.X, Height * justify.Y) - DrawOffset) / scaleFix;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    if (x != 0 || y != 0) {
                        Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position + new Vector2(x, y), clip, Color.Black, rotation, offset, scale, flip, 0f);
                    }
                }
            }
            Monocle.Draw.SpriteBatch.Draw(Texture.Texture, position, clip, color, rotation, offset, scale, flip, 0f);
        }

        #endregion

        #endregion

    }
    public static class MTextureExt {

        /// <summary>
        /// Set the AtlasPath property for this MTexture. Can only be set if it is null.
        /// </summary>
        public static void SetAtlasPath(this MTexture self, string path)
            => ((patch_MTexture) self).SetAtlasPath(path);

        /// <summary>
        /// Override the given MTexutre with the given VirtualTexture and parameters.
        /// </summary>
        public static void SetOverride(this MTexture self, VirtualTexture texture, Vector2 drawOffset, int frameWidth, int frameHeight)
            => ((patch_MTexture) self).SetOverride(texture, drawOffset, frameWidth, frameHeight);

        /// <summary>
        /// Override the given MTexutre with the given mod asset.
        /// </summary>
        public static void SetOverride(this MTexture self, ModAsset asset)
            => ((patch_MTexture) self).SetOverride(asset);

        /// <summary>
        /// Undo the latest override applied to the given MTexture.
        /// </summary>
        public static void UndoOverride(this MTexture self)
            => ((patch_MTexture) self).UndoOverride();

        /// <summary>
        /// Undo the given override applied to the given MTexture.
        /// </summary>
        public static void UndoOverride(this MTexture self, ModAsset asset)
            => ((patch_MTexture) self).UndoOverride(asset);

        /// <summary>
        /// Gets the parent texture of the given MTexture.
        /// </summary>
        public static MTexture GetParent(this MTexture self)
            => ((patch_MTexture) self).Parent;

        /// <summary>
        /// Sets the parent texture of the given MTexture.
        /// </summary>
        public static void SetParent(this MTexture self, MTexture parent)
            => ((patch_MTexture) self).Parent = parent;

    }
}
