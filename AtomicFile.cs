// 書き込み途中で落ちても元のファイルが壊れないよう、一時ファイルに書き切ってから置き換える。
// 設定ファイルの保存(XML/JSON)で共通して使う。
using System;
using System.IO;
using System.Text;

namespace StandardTemplate
{
    internal static class AtomicFile
    {
        public static void Write(String FilePath, Action<String> WriteTo)
        {
            String TempPath = FilePath + ".tmp";
            try
            {
                WriteTo(TempPath);
                if (File.Exists(FilePath))
                {
                    File.Replace(TempPath, FilePath, null);
                }
                else
                {
                    File.Move(TempPath, FilePath);
                }
            }
            finally
            {
                if (File.Exists(TempPath))
                {
                    File.Delete(TempPath);
                }
            }
        }

        public static void WriteAllText(String FilePath, String Text, Encoding Enc)
        {
            Write(FilePath, TempPath => File.WriteAllText(TempPath, Text, Enc));
        }
    }
}
