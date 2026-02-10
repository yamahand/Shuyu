using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Shuyu.Controls
{
    /// <summary>
    /// ホットキー入力用のTextBox。PowerToys風のキー入力UIを提供します。
    /// </summary>
    public class HotkeyTextBox : WpfTextBox
    {
        private uint _modifiers;
        private uint _virtualKey;
        
        /// <summary>
        /// モディファイアキーの依存関係プロパティ
        /// </summary>
        public static readonly DependencyProperty ModifiersProperty =
            DependencyProperty.Register(nameof(Modifiers), typeof(uint), typeof(HotkeyTextBox),
                new PropertyMetadata(0u, OnHotkeyChanged));
        
        /// <summary>
        /// 仮想キーコードの依存関係プロパティ
        /// </summary>
        public static readonly DependencyProperty VirtualKeyProperty =
            DependencyProperty.Register(nameof(VirtualKey), typeof(uint), typeof(HotkeyTextBox),
                new PropertyMetadata(0u, OnHotkeyChanged));
        
        /// <summary>
        /// モディファイアキーを取得または設定します。
        /// </summary>
        public uint Modifiers
        {
            get => (uint)GetValue(ModifiersProperty);
            set => SetValue(ModifiersProperty, value);
        }
        
        /// <summary>
        /// 仮想キーコードを取得または設定します。
        /// </summary>
        public uint VirtualKey
        {
            get => (uint)GetValue(VirtualKeyProperty);
            set => SetValue(VirtualKeyProperty, value);
        }
        
        /// <summary>
        /// HotkeyTextBox の新しいインスタンスを作成します。
        /// </summary>
        public HotkeyTextBox()
        {
            IsReadOnly = true;
            PreviewKeyDown += OnPreviewKeyDown;
            GotFocus += (s, e) => 
            {
                if (_virtualKey == 0)
                    Text = "キーを入力してください...";
            };
            LostFocus += (s, e) => UpdateDisplayText();
        }
        
        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            
            // モディファイアキー単体は無視
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System) // Alt キーと組み合わせた時の System キーを除外
            {
                return;
            }
            
            // Escキーでクリア
            if (e.Key == Key.Escape)
            {
                _modifiers = 0;
                _virtualKey = 0;
                Modifiers = 0;
                VirtualKey = 0;
                Text = string.Empty;
                return;
            }
            
            // モディファイアを取得
            _modifiers = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) _modifiers |= 0x0002; // MOD_CONTROL
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) _modifiers |= 0x0001;     // MOD_ALT
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) _modifiers |= 0x0004;   // MOD_SHIFT
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) _modifiers |= 0x0008; // MOD_WIN
            
            // 仮想キーコードを取得（Systemキーの場合は SystemKey プロパティを使用）
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            _virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            
            // プロパティを更新
            Modifiers = _modifiers;
            VirtualKey = _virtualKey;
            
            UpdateDisplayText();
        }
        
        private void UpdateDisplayText()
        {
            if (_virtualKey == 0)
            {
                Text = string.Empty;
                return;
            }
            
            var parts = new List<string>();
            if ((_modifiers & 0x0002) != 0) parts.Add("Ctrl");
            if ((_modifiers & 0x0001) != 0) parts.Add("Alt");
            if ((_modifiers & 0x0004) != 0) parts.Add("Shift");
            if ((_modifiers & 0x0008) != 0) parts.Add("Win");
            
            // 仮想キーコードから表示名を取得
            var keyName = GetKeyName(_virtualKey);
            parts.Add(keyName);
            
            Text = string.Join(" + ", parts);
        }
        
        private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyTextBox textBox)
            {
                textBox._modifiers = textBox.Modifiers;
                textBox._virtualKey = textBox.VirtualKey;
                textBox.UpdateDisplayText();
            }
        }
        
        private string GetKeyName(uint vk)
        {
            // よく使われるキーの表示名をマッピング
            return vk switch
            {
                0x2C => "PrintScreen",
                0x70 => "F1",
                0x71 => "F2",
                0x72 => "F3",
                0x73 => "F4",
                0x74 => "F5",
                0x75 => "F6",
                0x76 => "F7",
                0x77 => "F8",
                0x78 => "F9",
                0x79 => "F10",
                0x7A => "F11",
                0x7B => "F12",
                0x20 => "Space",
                0x0D => "Enter",
                0x09 => "Tab",
                0x2E => "Delete",
                0x2D => "Insert",
                0x24 => "Home",
                0x23 => "End",
                0x21 => "PageUp",
                0x22 => "PageDown",
                0x25 => "Left",
                0x26 => "Up",
                0x27 => "Right",
                0x28 => "Down",
                0xBD => "OemMinus",      // -
                0xBB => "OemPlus",       // +
                0xDB => "OemOpenBrackets", // [
                0xDD => "OemCloseBrackets", // ]
                0xDC => "OemPipe",       // \
                0xBA => "OemSemicolon",  // ;
                0xDE => "OemQuotes",     // '
                0xBC => "OemComma",      // ,
                0xBE => "OemPeriod",     // .
                0xBF => "OemQuestion",   // /
                0xC0 => "OemTilde",      // `
                _ => ((Key)KeyInterop.KeyFromVirtualKey((int)vk)).ToString()
            };
        }
    }
}
