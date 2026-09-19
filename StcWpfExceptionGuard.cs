using System;
using System.Reflection;
using System.Threading;
using System.Windows;

internal static class StcWpfExceptionGuard
{
    private static int IsShowing = 0;

    public static void Install(Application App)
    {
        App.DispatcherUnhandledException += (s, e) =>
        {
            // 起動時(MainWindowのコンストラクタ等)の例外で画面が1枚も無いまま継続すると、
            // 見えないプロセスだけが残って終了できなくなるため、その場合はアプリを閉じる
            // (コンストラクタで失敗したWindowもWindowsには登録されてしまうため、件数ではなく表示中かで判定する)
            Boolean HasWindow = false;
            foreach (Window window in App.Windows)
            {
                HasWindow |= window.IsVisible;
            }
            Notify(e.Exception, HasWindow);
            e.Handled = true;
            if (!HasWindow)
            {
                App.Shutdown();
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Notify(e.ExceptionObject as Exception, false);
    }

    private static void Notify(Exception ex, Boolean CanContinue)
    {
        // 同じ例外が連発しても、ダイアログが無限に積み重ならないようにする
        if (Interlocked.Exchange(ref IsShowing, 1) == 1 && CanContinue)
        {
            return;
        }

        try
        {
            String message = CanContinue
                ? "予期しないエラーが発生したよ(処理は継続するね)"
                : "予期しないエラーが発生したよ(アプリを終了するね)";
            MessageBox.Show(
                message + Environment.NewLine + Environment.NewLine + ex,
                Assembly.GetEntryAssembly().GetName().Name + " - エラー",
                MessageBoxButton.OK,
                CanContinue ? MessageBoxImage.Warning : MessageBoxImage.Error);
        }
        finally
        {
            Interlocked.Exchange(ref IsShowing, 0);
        }
    }
}
