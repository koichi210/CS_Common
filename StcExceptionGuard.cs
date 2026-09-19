using System;
using System.Threading;
using System.Windows.Forms;

internal static class StcExceptionGuard
{
    private static int IsShowing = 0;

    // Main()の先頭(フォーム生成より前)で呼ぶこと。SetUnhandledExceptionModeはウィンドウ生成後だと例外になる
    // 既定のJITデバッグダイアログは画面の裏に隠れて「固まった」ように見えるため、自前で通知して処理を継続させる
    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => Notify(e.Exception, true);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Notify(e.ExceptionObject as Exception, false);
    }

    private static void Notify(Exception ex, Boolean CanContinue)
    {
        // タイマー処理などで同じ例外が連発しても、ダイアログが無限に積み重ならないようにする
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
                Application.ProductName + " - エラー",
                MessageBoxButtons.OK,
                CanContinue ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
        }
        finally
        {
            Interlocked.Exchange(ref IsShowing, 0);
        }
    }
}
