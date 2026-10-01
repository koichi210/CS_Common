using System;
using System.Reflection;
using System.Threading;
using System.Windows;

internal static class StcWpfExceptionGuard
{
    private static int IsShowing = 0;

    public static void Install(Application app)
    {
        app.DispatcherUnhandledException += (s, e) =>
        {
            // 起動時(MainWindowのコンストラクタ等)の例外で画面が1枚も無いまま継続すると、
            // 見えないプロセスだけが残って終了できなくなるため、その場合はアプリを閉じる
            // (コンストラクタで失敗したWindowもWindowsには登録されてしまうため、件数ではなく表示中かで判定する)
            Boolean hasWindow = false;
            foreach (Window window in app.Windows)
            {
                hasWindow |= window.IsVisible;
            }
            Notify(e.Exception, hasWindow);
            e.Handled = true;
            if (!hasWindow)
            {
                app.Shutdown();
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Notify(e.ExceptionObject as Exception, false);
    }

    private static void Notify(Exception ex, Boolean canContinue)
    {
        // 同じ例外が連発しても、ダイアログが無限に積み重ならないようにする
        if (Interlocked.Exchange(ref IsShowing, 1) == 1 && canContinue)
        {
            return;
        }

        try
        {
            String message = canContinue
                ? "予期しないエラーが発生したよ(処理は継続するね)"
                : "予期しないエラーが発生したよ(アプリを終了するね)";
            MessageBox.Show(
                message + Environment.NewLine + Environment.NewLine + ex,
                Assembly.GetEntryAssembly().GetName().Name + " - エラー",
                MessageBoxButton.OK,
                canContinue ? MessageBoxImage.Warning : MessageBoxImage.Error);
        }
        finally
        {
            Interlocked.Exchange(ref IsShowing, 0);
        }
    }
}
