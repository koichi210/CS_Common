// WPFのコントロール/Windowは STA スレッドでしか生成できないが、MSTest(v2, .NET Framework)の
// テストメソッドは MTA で動く。画面を作るテストはこのヘルパー経由で STA スレッド上で実行する。
using System;
using System.IO.Packaging;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace StandardTemplate.Wpf.Tests
{
    internal static class StaRunner
    {
        public static void Run(Action action)
        {
            // XAML内の相対パス(Icon等)を解決するpack://スキームは、Applicationが無いと未登録のままなので先に登録しておく
            String unused = PackUriHelper.UriSchemePack;

            Exception error = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }
    }
}
