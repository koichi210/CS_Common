// DataFolderMenu([[_Common/DataFolderMenu.cs]])のWPF版。システムメニュー(タイトルバー右クリック)に
// 「データ保存先を変更」を足す。保存先の選び直し・引っ越し自体はDataFolderMenuの処理をそのまま使う。
//
// 使い方(MainWindowのコンストラクタ):
//   WpfDataFolderMenu.Attach(this, () => DataFolderMenu.ChangeDataFolder(AppName, userDataFolder, ...));
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StandardTemplate.Wpf
{
    public static class WpfDataFolderMenu
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, Boolean bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern Boolean AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, String lpNewItem);

        private const uint MF_SEPARATOR = 0x800;
        private const uint MF_STRING = 0x0;
        private const int WM_SYSCOMMAND = 0x112;

        // DataFolderMenuと同じID(16の倍数かつ0xF000未満)
        private const int SysMenuId_ChangeDataFolder = 0x1000;

        public static void Attach(Window TargetWindow, Action OnChangeDataFolder)
        {
            TargetWindow.SourceInitialized += (s, e) =>
            {
                IntPtr hWnd = new WindowInteropHelper(TargetWindow).Handle;
                IntPtr systemMenu = GetSystemMenu(hWnd, false);
                AppendMenu(systemMenu, MF_SEPARATOR, UIntPtr.Zero, String.Empty);
                AppendMenu(systemMenu, MF_STRING, (UIntPtr)SysMenuId_ChangeDataFolder, "データ保存先を変更(&D)...");

                HwndSource.FromHwnd(hWnd).AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref Boolean handled) =>
                {
                    if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SysMenuId_ChangeDataFolder)
                    {
                        handled = true;
                        OnChangeDataFolder();
                    }
                    return IntPtr.Zero;
                });
            };
        }
    }
}
