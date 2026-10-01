// 書き込み途中で落ちても元のファイルが壊れないよう、一時ファイルに書き切ってから置き換える。
// 設定ファイルの保存(XML/JSON)で共通して使う。
using System;
using System.IO;
using System.Text;

namespace StandardTemplate
{
    internal static class AtomicFile
    {
        public static void Write(String filePath, Action<String> writeTo)
        {
            String tempPath = filePath + ".tmp";
            try
            {
                writeTo(tempPath);
                if (File.Exists(filePath))
                {
                    File.Replace(tempPath, filePath, null);
                }
                else
                {
                    File.Move(tempPath, filePath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        public static void WriteAllText(String filePath, String text, Encoding enc)
        {
            Write(filePath, tempPath => File.WriteAllText(tempPath, text, enc));
        }
    }
}
