using System.Runtime.InteropServices;

namespace CefFlashBrowser.Utils
{
    public static class Win32
    {
        [DllImport("kernel32.dll")]
        public static extern bool SetDllDirectory(string lpPathName);

        public static class VirtualKeys
        {
            public const int VK_LBUTTON = 0x01; // Left mouse button
            public const int VK_RBUTTON = 0x02; // Right mouse button
            public const int VK_CANCEL = 0x03; // Control-break processing
            public const int VK_MBUTTON = 0x04; // Middle mouse button
            public const int VK_XBUTTON1 = 0x05; // X1 mouse button
            public const int VK_XBUTTON2 = 0x06; // X2 mouse button
            // - 0x07 Reserved
            public const int VK_BACK = 0x08; // BACKSPACE key
            public const int VK_TAB = 0x09; // TAB key
            // - 0x0A-0B Reserved
            public const int VK_CLEAR = 0x0C; // CLEAR key
            public const int VK_RETURN = 0x0D; // ENTER key
            // - 0x0E-0F Unassigned
            public const int VK_SHIFT = 0x10; // SHIFT key
            public const int VK_CONTROL = 0x11; // CTRL key
            public const int VK_MENU = 0x12; // ALT key
            public const int VK_PAUSE = 0x13; // PAUSE key
            public const int VK_CAPITAL = 0x14; // CAPS LOCK key
            public const int VK_KANA = 0x15; // IME Kana mode
            public const int VK_HANGUL = 0x15; // IME Hangul mode
            public const int VK_IME_ON = 0x16; // IME On
            public const int VK_JUNJA = 0x17; // IME Junja mode
            public const int VK_FINAL = 0x18; // IME final mode
            public const int VK_HANJA = 0x19; // IME Hanja mode
            public const int VK_KANJI = 0x19; // IME Kanji mode
            public const int VK_IME_OFF = 0x1A; // IME Off
            public const int VK_ESCAPE = 0x1B; // ESC key
            public const int VK_CONVERT = 0x1C; // IME convert
            public const int VK_NONCONVERT = 0x1D; // IME nonconvert
            public const int VK_ACCEPT = 0x1E; // IME accept
            public const int VK_MODECHANGE = 0x1F; // IME mode change request
            public const int VK_SPACE = 0x20; // SPACEBAR
            public const int VK_PRIOR = 0x21; // PAGE UP key
            public const int VK_NEXT = 0x22; // PAGE DOWN key
            public const int VK_END = 0x23; // END key
            public const int VK_HOME = 0x24; // HOME key
            public const int VK_LEFT = 0x25; // LEFT ARROW key
            public const int VK_UP = 0x26; // UP ARROW key
            public const int VK_RIGHT = 0x27; // RIGHT ARROW key
            public const int VK_DOWN = 0x28; // DOWN ARROW key
            public const int VK_SELECT = 0x29; // SELECT key
            public const int VK_PRINT = 0x2A; // PRINT key
            public const int VK_EXECUTE = 0x2B; // EXECUTE key
            public const int VK_SNAPSHOT = 0x2C; // PRINT SCREEN key
            public const int VK_INSERT = 0x2D; // INS key
            public const int VK_DELETE = 0x2E; // DEL key
            public const int VK_HELP = 0x2F; // HELP key
            // 0x30 0 key
            // 0x31 1 key
            // 0x32 2 key
            // 0x33 3 key
            // 0x34 4 key
            // 0x35 5 key
            // 0x36 6 key
            // 0x37 7 key
            // 0x38 8 key
            // 0x39 9 key
            // - 0x3A-40 Undefined
            // 0x41 A key
            // 0x42 B key
            // 0x43 C key
            // 0x44 D key
            // 0x45 E key
            // 0x46 F key
            // 0x47 G key
            // 0x48 H key
            // 0x49 I key
            // 0x4A J key
            // 0x4B K key
            // 0x4C L key
            // 0x4D M key
            // 0x4E N key
            // 0x4F O key
            // 0x50 P key
            // 0x51 Q key
            // 0x52 R key
            // 0x53 S key
            // 0x54 T key
            // 0x55 U key
            // 0x56 V key
            // 0x57 W key
            // 0x58 X key
            // 0x59 Y key
            // 0x5A Z key
            public const int VK_LWIN = 0x5B; // Left Windows key
            public const int VK_RWIN = 0x5C; // Right Windows key
            public const int VK_APPS = 0x5D; // Applications key
            // - 0x5E Reserved
            public const int VK_SLEEP = 0x5F; // Computer Sleep key
            public const int VK_NUMPAD0 = 0x60; // Numeric keypad 0 key
            public const int VK_NUMPAD1 = 0x61; // Numeric keypad 1 key
            public const int VK_NUMPAD2 = 0x62; // Numeric keypad 2 key
            public const int VK_NUMPAD3 = 0x63; // Numeric keypad 3 key
            public const int VK_NUMPAD4 = 0x64; // Numeric keypad 4 key
            public const int VK_NUMPAD5 = 0x65; // Numeric keypad 5 key
            public const int VK_NUMPAD6 = 0x66; // Numeric keypad 6 key
            public const int VK_NUMPAD7 = 0x67; // Numeric keypad 7 key
            public const int VK_NUMPAD8 = 0x68; // Numeric keypad 8 key
            public const int VK_NUMPAD9 = 0x69; // Numeric keypad 9 key
            public const int VK_MULTIPLY = 0x6A; // Multiply key
            public const int VK_ADD = 0x6B; // Add key
            public const int VK_SEPARATOR = 0x6C; // Separator key
            public const int VK_SUBTRACT = 0x6D; // Subtract key
            public const int VK_DECIMAL = 0x6E; // Decimal key
            public const int VK_DIVIDE = 0x6F; // Divide key
            public const int VK_F1 = 0x70; // F1 key
            public const int VK_F2 = 0x71; // F2 key
            public const int VK_F3 = 0x72; // F3 key
            public const int VK_F4 = 0x73; // F4 key
            public const int VK_F5 = 0x74; // F5 key
            public const int VK_F6 = 0x75; // F6 key
            public const int VK_F7 = 0x76; // F7 key
            public const int VK_F8 = 0x77; // F8 key
            public const int VK_F9 = 0x78; // F9 key
            public const int VK_F10 = 0x79; // F10 key
            public const int VK_F11 = 0x7A; // F11 key
            public const int VK_F12 = 0x7B; // F12 key
            public const int VK_F13 = 0x7C; // F13 key
            public const int VK_F14 = 0x7D; // F14 key
            public const int VK_F15 = 0x7E; // F15 key
            public const int VK_F16 = 0x7F; // F16 key
            public const int VK_F17 = 0x80; // F17 key
            public const int VK_F18 = 0x81; // F18 key
            public const int VK_F19 = 0x82; // F19 key
            public const int VK_F20 = 0x83; // F20 key
            public const int VK_F21 = 0x84; // F21 key
            public const int VK_F22 = 0x85; // F22 key
            public const int VK_F23 = 0x86; // F23 key
            public const int VK_F24 = 0x87; // F24 key
            // - 0x88-8F Reserved
            public const int VK_NUMLOCK = 0x90; // NUM LOCK key
            public const int VK_SCROLL = 0x91; // SCROLL LOCK key
            // - 0x92-96 OEM specific
            // - 0x97-9F Unassigned
            public const int VK_LSHIFT = 0xA0; // Left SHIFT key
            public const int VK_RSHIFT = 0xA1; // Right SHIFT key
            public const int VK_LCONTROL = 0xA2; // Left CONTROL key
            public const int VK_RCONTROL = 0xA3; // Right CONTROL key
            public const int VK_LMENU = 0xA4; // Left ALT key
            public const int VK_RMENU = 0xA5; // Right ALT key
            public const int VK_BROWSER_BACK = 0xA6; // Browser Back key
            public const int VK_BROWSER_FORWARD = 0xA7; // Browser Forward key
            public const int VK_BROWSER_REFRESH = 0xA8; // Browser Refresh key
            public const int VK_BROWSER_STOP = 0xA9; // Browser Stop key
            public const int VK_BROWSER_SEARCH = 0xAA; // Browser Search key
            public const int VK_BROWSER_FAVORITES = 0xAB; // Browser Favorites key
            public const int VK_BROWSER_HOME = 0xAC; // Browser Start and Home key
            public const int VK_VOLUME_MUTE = 0xAD; // Volume Mute key
            public const int VK_VOLUME_DOWN = 0xAE; // Volume Down key
            public const int VK_VOLUME_UP = 0xAF; // Volume Up key
            public const int VK_MEDIA_NEXT_TRACK = 0xB0; // Next Track key
            public const int VK_MEDIA_PREV_TRACK = 0xB1; // Previous Track key
            public const int VK_MEDIA_STOP = 0xB2; // Stop Media key
            public const int VK_MEDIA_PLAY_PAUSE = 0xB3; // Play/Pause Media key
            public const int VK_LAUNCH_MAIL = 0xB4; // Start Mail key
            public const int VK_LAUNCH_MEDIA_SELECT = 0xB5; // Select Media key
            public const int VK_LAUNCH_APP1 = 0xB6; // Start Application 1 key
            public const int VK_LAUNCH_APP2 = 0xB7; // Start Application 2 key
            // - 0xB8-B9 Reserved
            public const int VK_OEM_1 = 0xBA; // Used for miscellaneous characters; it can vary by keyboard. For the US standard keyboard, the ;: key
            public const int VK_OEM_PLUS = 0xBB; // For any country/region, the + key
            public const int VK_OEM_COMMA = 0xBC; // For any country/region, the , key
            public const int VK_OEM_MINUS = 0xBD; // For any country/region, the // - key
            public const int VK_OEM_PERIOD = 0xBE; // For any country/region, the . key
            public const int VK_OEM_2 = 0xBF; // Used for miscellaneous characters; it can vary by keyboard. For the US standard keyboard, the /? key
            public const int VK_OEM_3 = 0xC0; // Used for miscellaneous characters; it can vary by keyboard. For the US standard keyboard, the `~ key
            // - 0xC1-DA Reserved
            public const int VK_OEM_4 = 0xDB; // Used for miscellaneous characters; it can vary by keyboard. For the US standard keyboard, the [{ key
            public const int VK_OEM_5 = 0xDC; // Used for miscellaneous characters; it can vary by keyboard. For the US standard keyboard, the \\| key
            public const int VK_OEM_6 = 0xDD; // Used for miscellaneous characters; it can vary by keyboard. For the US standard keyboard, the ]} key
            public const int VK_OEM_7 = 0xDE; // Used for miscellaneous characters; it can vary by keyboard. For the US standard keyboard, the '" key
            public const int VK_OEM_8 = 0xDF; // Used for miscellaneous characters; it can vary by keyboard.
            // - 0xE0 Reserved
            // - 0xE1 OEM specific
            public const int VK_OEM_102 = 0xE2; // The <> keys on the US standard keyboard, or the \\| key on the non-US 102-key keyboard
            // - 0xE3-E4 OEM specific
            public const int VK_PROCESSKEY = 0xE5; // IME PROCESS key
            // - 0xE6 OEM specific
            public const int VK_PACKET = 0xE7; // Used to pass Unicode characters as if they were keystrokes. The VK_PACKET key is the low word of a 32-bit Virtual Key value used for non-keyboard input methods. For more information, see Remark in KEYBDINPUT, SendInput, WM_KEYDOWN, and WM_KEYUP
            // - 0xE8 Unassigned
            // - 0xE9-F5 OEM specific
            public const int VK_ATTN = 0xF6; // Attn key
            public const int VK_CRSEL = 0xF7; // CrSel key
            public const int VK_EXSEL = 0xF8; // ExSel key
            public const int VK_EREOF = 0xF9; // Erase EOF key
            public const int VK_PLAY = 0xFA; // Play key
            public const int VK_ZOOM = 0xFB; // Zoom key
            public const int VK_NONAME = 0xFC; // Reserved
            public const int VK_PA1 = 0xFD; // PA1 key
            public const int VK_OEM_CLEAR = 0xFE; // Clear key
        }
    }
}
