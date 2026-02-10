using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Shuyu.Service
{
    internal enum HotkeyInvalidReason
    {
        None,
        RequiresModifier,
        TooManyKeys,
        NoMainKey,
        ForbiddenKey,
        OSReservedCombo,
        PrintScreenNeedsModifier
    }

    internal readonly struct HotkeyValidationResult
    {
        public readonly bool IsValid;
        public readonly HotkeyInvalidReason Reason;
        public HotkeyValidationResult(bool isValid, HotkeyInvalidReason reason)
        {
            IsValid = isValid;
            Reason = reason;
        }
    }

    internal static class HotkeyValidator
    {
        // Forbidden single keys (non-modifier) and UI navigation keys
        private static readonly HashSet<uint> ForbiddenMainKeys = new()
        {
            0x09, // Tab
            0x1B, // Esc
            0x0D, // Enter
            0x25, 0x26, 0x27, 0x28, // Arrows
            0x24, // Home
            0x23, // End
            0x21, // PageUp
            0x22, // PageDown
            0x91, // ScrollLock
            0x90, // NumLock
            0x14, // CapsLock
            0x13, // Pause/Break
            0x7B, // F12 (reserved by debugger)
        };

        // IME related virtual keys to avoid
        private static readonly HashSet<uint> ForbiddenImeKeys = new()
        {
            0x1C, // Convert
            0x1D, // NonConvert
            0x15, // Kana
            0x19, // Kanji
            0x1F, // Hanguel
            0x1A, // Hanji
            0x1E, // Accept / ModeChange
            0xE5, // ProcessKey
            0xE7, // Packet
        };

        // OS reserved combos (subset representative)
        private static readonly HashSet<(uint modifiers, uint vk)> OsReservedCombos = new()
        {
            (0x0001, 0x09), // Alt+Tab
            (0x0001, 0x73), // Alt+F4
            (0x0008, 0x5B), // Win+Left (LWin as modifier handled separately)
            (0x0008, 0x5C), // Win+Right (RWin)
            (0x0008, 0x44), // Win+D
            (0x0008, 0x4C), // Win+L
            (0x0008, 0x52), // Win+R
            (0x0008, 0x45), // Win+E
            (0x0008, 0x54), // Win+T
            (0x0008, 0x20), // Win+Space
            (0x0008 | 0x0004, 0x53), // Win+Shift+S
        };

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint VK_SNAPSHOT = 0x2C;

        public static HotkeyValidationResult ValidateShortcut(uint modifiers, uint vk)
        {
            // Unset is invalid per requirement
            if (vk == 0)
                return new HotkeyValidationResult(false, HotkeyInvalidReason.NoMainKey);

            bool hasModifier = (modifiers & (MOD_ALT | MOD_CONTROL | MOD_SHIFT | MOD_WIN)) != 0;
            if (!hasModifier)
                return new HotkeyValidationResult(false, HotkeyInvalidReason.RequiresModifier);

            // Only one main key allowed (this function sees single vk)
            // Max 3 keys total implied by up to 2 modifiers + 1 main key, we accept any number of modifiers but dialog will cap input

            // PrintScreen must be with modifiers
            if (vk == VK_SNAPSHOT && modifiers == 0)
                return new HotkeyValidationResult(false, HotkeyInvalidReason.PrintScreenNeedsModifier);

            // Forbidden main keys
            if (ForbiddenMainKeys.Contains(vk) || ForbiddenImeKeys.Contains(vk))
                return new HotkeyValidationResult(false, HotkeyInvalidReason.ForbiddenKey);

            // OS reserved representative checks (coarse)
            foreach (var combo in OsReservedCombos)
            {
                if (combo.vk == vk && (modifiers & combo.modifiers) == combo.modifiers)
                    return new HotkeyValidationResult(false, HotkeyInvalidReason.OSReservedCombo);
            }

            return new HotkeyValidationResult(true, HotkeyInvalidReason.None);
        }
    }
}
