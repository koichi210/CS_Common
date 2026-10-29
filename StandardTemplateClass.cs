using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text;
using System.Linq;
using System.IO;
using System.Xml;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace StandardTemplate
{
    public class StcUtils // ユーティリティ系 ***************************************************
    {
        // StcUtilsは元々どんな処理も無節操に詰め込んだ「何でも屋」クラスで、性質の違う
        // 責務(パス変換・プロセス実行・存在チェック・WinFormsコントロール操作等)が
        // 1クラスに同居していて見通しが悪かった。クラスを物理的に分割すると17プロジェクトの
        // csprojすべてに新規ファイルの<Link>追加が必要になり影響範囲が大きいため、
        // まずは既存のセクション区切りコメントを#regionに変えて、責務ごとに折りたためる
        // ようにするだけに留めた(公開APIも挙動も一切変えていない)。

        #region 初期化・パス種別判定
        public enum FilePathType
        {
            WindowsFullPath,
            PerforcePath,
            LinuxPath,
            WindowsPath,
            Other
        }

        // カレントディレクトリ移動
        public void SetCurrentDirectory()
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public FilePathType GetFilePathType(String filePath)
        {
            if (filePath.IndexOf(':') != -1)
            {
                return FilePathType.WindowsFullPath;
            }
            if (filePath.StartsWith("//"))
            {
                return FilePathType.PerforcePath;
            }
            if (filePath.IndexOf('/') != -1)
            {
                return FilePathType.LinuxPath;
            }
            if (filePath.IndexOf('\\') != -1)
            {
                return FilePathType.WindowsPath;
            }
            return FilePathType.Other;
        }

        #endregion

        #region プロセス実行
        // プロセス -----------------------------------------
        // プロセス実行
        public Process ExecuteProcess(String execPath, Boolean noWindow)
        {
            return ExecuteProcess(execPath, "", noWindow);
        }

        public Process ExecuteProcess(String execPath, String arguments = "", Boolean noWindow = false)
        {
            ProcessStartInfo psInfo = new ProcessStartInfo();
            psInfo.FileName = execPath;
            psInfo.Arguments = arguments;

            if (noWindow == true)
            {
                //psInfo.CreateNoWindow = true;     // ウィンドウを開かない
                psInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            return Process.Start(psInfo);
        }

        // プロセス実行&完了待ち
        public void ExecutePathWithWait(String path, String arguments = "", Boolean noWindow = false)
        {
            using (Process process = ExecuteProcess(path, arguments, noWindow))
            {
                // UseShellExecute経由では、既存のプロセスに処理を渡した場合などにProcess.Startがnullを返すことがある
                process?.WaitForExit(); // 処理が終わるまで待つ
            }
        }

        // プロセス実行&標準出力取得
        public Process ExecuteProcess(out String output, String execPath, String arguments = "")
        {
            ProcessStartInfo psInfo = new ProcessStartInfo();
            psInfo.FileName = execPath;
            psInfo.Arguments = arguments;
            psInfo.CreateNoWindow = true;
            psInfo.UseShellExecute = false;
            psInfo.RedirectStandardOutput = true; // 標準出力をリダイレクト

            Process p = Process.Start(psInfo);
            output = p.StandardOutput.ReadToEnd(); // 標準出力を取得
            return p;
        }

        #endregion

        #region 存在チェック・ファイル実行
        // Explorer系 -----------------------------------------
        // ファイルパスが存在するかチェック[環境変数のPathを考慮]
        public Boolean IsExistFileNameInEnvironment(String fileName = "")
        {
            // フルパスで指定されている
            if (File.Exists(fileName))
            {
                return true;
            }

            // 環境変数の[PATH]を取得
            String paths = System.Environment.GetEnvironmentVariable("Path");

            String[] pathArray = paths.Split(';');
            foreach (String pathName in pathArray)
            {
                if (pathName == String.Empty)
                {
                    continue;
                }

                String fullPathName = pathName.TrimEnd('\\') + '\\';
                if (File.Exists(fullPathName + fileName))
                {
                    // 環境変数のパスで登録された場所に存在する
                    return true;
                }
            }
            return false;
        }

        // パスが有効かチェック
        // 以前はIsExistPathからしか呼ばれないprivateのIsExistDirectoryを経由していたが、
        // 通知メッセージ用の引数は常に既定値(通知しない)で、実質Directory.Existsと同じだったため直接呼ぶ
        public Boolean IsExistPath(String filePath)
        {
            return Directory.Exists(filePath) || IsExistFile(filePath);
        }

        public Boolean IsExistFile(String filePath, Boolean isNoticeExceptMsg = false, String exceptMsgStr = "")
        {
            if (File.Exists(filePath))
            {
                return true;
            }

            if (IsExistFileNameInEnvironment(filePath))
            {
                return true;
            }

            if (isNoticeExceptMsg)
            {
                String msg = "";
                if (exceptMsgStr != String.Empty)
                {
                    msg += exceptMsgStr + Environment.NewLine;
                }
                msg += "ファイルが存在しません。" + Environment.NewLine;
                msg += "[" + filePath + "]";
                MessageBox.Show(msg);
            }
            return false;
        }

        // ファイルパス実行
        public Boolean ExecutePath(String execPath, Boolean noWindow = false)
        {
            return ExecutePath(execPath, "", noWindow);
        }

        // ファイルパス実行
        public Boolean ExecutePath(String execPath, String arguments, Boolean noWindow = false)
        {
            if (File.Exists(execPath) ||
                Directory.Exists(execPath) ||
                IsExistFileNameInEnvironment(execPath) ||
                execPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                execPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteProcess(execPath, arguments, noWindow);
                return true;
            }

            MessageBox.Show(
                "指定されたパスが存在しません。" + execPath,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        // ファイルパス実行
        public Boolean ExecutePath(String execPath, KeyEventArgs e)
        {
            if (e == null)
            {
                return false;
            }

            if (e.KeyCode != Keys.Enter)
            {
                // Enter押下じゃない
                return false;
            }

            return ExecutePath(execPath);
        }

        // 読み取り属性解除。以前はStcFileInputOutputをここでnewして使っており、
        // UtilからFileIOへ依存する逆向きの参照になっていた(TODOコメントで指摘されていた)。
        // 実体をこちらへ移し、StcFileInputOutput.RemoveReadonlyAttribute(String)からは
        // このメソッドへ委譲する形にして、依存の向き(StcFileInputOutput→StcUtils)を揃えた。
        public Boolean RemoveReadonlyAttribute(String fileName)
        {
            FileInfo fi = new FileInfo(fileName);
            if (!fi.Exists)
            {
                // ファイルが無い
                MessageBox.Show("指定されたパスが存在しません。");
                return false;
            }

            if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                DialogResult dlgResult = MessageBox.Show(
                    "読み取り専用属性を解除しますか？" + Environment.NewLine + fileName,
                    "Information",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (dlgResult == DialogResult.Yes)
                {
                    // 読み取り専用属性だけを解除する(隠し・システムなど他の属性は残す)
                    fi.Attributes &= ~FileAttributes.ReadOnly;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 数値・文字列・パス変換
        // GetNumber内で呼ばれるたびにRegexを生成し直していたため、1つを使い回す
        // (DataGridViewのセル読込など、ループ内から何度も呼ばれる)
        private static readonly Regex _nonDigitRegex = new Regex(@"[^0-9]");

        // 数値or文字列操作 -----------------------------------
        public int GetInteger(String text)
        {
            int result = 0;
            if (!text.Equals(String.Empty))
            {
                result = int.Parse(text);
            }
            return result;
        }

        public Boolean GetBoolean(String text, String detectWord = "True")
        {
            return !text.Equals(String.Empty) && text.Equals(detectWord);
        }

        // 空文字のときはdefaultValueをそのまま返す(桁数ではなく値として)
        public long GetNumber(String srcString, int defaultValue = 1)
        {
            long number = defaultValue;

            if (srcString != String.Empty)
            {
                number = long.Parse(srcString);
            }

            return number;
        }

        // 文字列から数値を取得
        public long GetNumber(String srcString, int startIdx, int length, String defaultString = "01")
        {
            int digitCount = defaultString.Length;  // 桁数

            // 負の値はSubstringで例外になるため、0(先頭/残り全部)に丸める
            startIdx = Math.Max(0, startIdx);
            length = Math.Max(0, length);

            // StartIdxを設定
            if (srcString.Length < startIdx)
            {
                // 範囲外だったら、終端を設定
                startIdx = srcString.Length;
            }

            // Lengthを設定
            if (length == 0 ||
                 (srcString.Length < startIdx + length))
            {
                // 長さが0 or 範囲外だったら、ギリギリ範囲内に設定
                length = srcString.Length - startIdx;
            }

            String srcNumStr = srcString.Substring(startIdx, length);
            String destNumStr = _nonDigitRegex.Replace(srcNumStr, "");

            if (destNumStr == String.Empty)
            {
                destNumStr = defaultString;
            }
            else
            {
                // 所望の桁数まで"0"埋めする
                destNumStr = destNumStr.PadLeft(digitCount, '0');
            }
            return GetNumber(destNumStr);
        }

        // 文字列から数値を取得
        public long GetNumberFromRear(String srcString, int endIdx, int length, String defaultString = "01")
        {
            int startIdx = srcString.Length - endIdx;
            return GetNumber(srcString, startIdx, length, defaultString);
        }

        public long GetNumberFromRear(String srcString, String endIdxString, String lengthString, String defaultString = "01")
        {
            // EndIdxを設定
            // (数字以外・負数など解釈できない入力は未指定として扱う。入力途中の「-」などで例外にしない)
            int endIdx = srcString.Length;
            int endIdxValue;
            if (int.TryParse(endIdxString, out endIdxValue) && endIdxValue >= 0)
            {
                endIdx = Math.Min(endIdx, endIdxValue);
            }

            // Lengthを設定
            int length = endIdx;
            int lengthValue;
            if (int.TryParse(lengthString, out lengthValue) && lengthValue >= 0)
            {
                length = lengthValue;
            }

            return GetNumberFromRear(srcString, endIdx, length, defaultString);
        }

        // パスがWindows仕様かチェック
        public Boolean IsWindowsPath(String path)
        {
            // "\"が見つかったらWindowsPathなので、置換対象と判断
            return path.IndexOf(@"\") != -1;
        }

        // ファイルパスの仕様を変更 [C:\ → /cygdrive/c]
        public String ChangeWindowsPath2CygwinPath(String oldPath)
        {
            String newPath = oldPath;
            if (IsWindowsPath(newPath))
            {
                if (newPath.IndexOf(@":") != -1)
                {
                    newPath = newPath.Replace(@":", @"");
                    newPath = "/cygdrive/" + newPath;
                }
            }

            return ChangeWindowsPath2LinuxPath(newPath);
        }

        // ドライブレターを変更 [/cygdrive/c → C:\]
        public String ChangeCygwinPath2WindowsPath(String oldPath)
        {
            String newPath = oldPath;
            if (!IsWindowsPath(newPath))
            {
                if (newPath.IndexOf(@"/cygdrive/") != -1)
                {
                    newPath = newPath.Replace(@"/cygdrive/", @"");

                    // ドライブレター1文字の直後にコロンを入れる。
                    // String.Insert は元の文字列を書き換えず新しい文字列を返すので、必ず受け取る。
                    if (newPath.Length >= 1)
                    {
                        newPath = newPath.Insert(1, @":");
                    }

                    // ChangeWindowsPath2CygwinPath と対になるよう、区切りも Windows 形式へ戻す
                    newPath = ChangeLinuxPath2WindowsPath(newPath);
                }
            }

            return newPath;
        }

        // パス区切りを変換[\ → /]
        public String ChangeWindowsPath2LinuxPath(String oldPath)
        {
            return oldPath.Replace(@"\", @"/");
        }

        // パス区切りを変換[\ → /]
        public String[] ChangeWindowsPath2LinuxPath(String[] oldPathArray)
        {
            return oldPathArray.Select(str => ChangeWindowsPath2LinuxPath(str)).ToArray();
        }

        // パス区切りを変換[/ → \]
        public String ChangeLinuxPath2WindowsPath(String oldPath)
        {
            return oldPath.Replace(@"/", @"\");
        }

        // パス区切りを変換[/ → \]
        public String[] ChangeLinuxPath2WindowsPath(String[] oldPathArray)
        {
            return oldPathArray.Select(str => ChangeLinuxPath2WindowsPath(str)).ToArray();
        }

        // 改行コードを変換[LF→CRLF]
        public String ChangeNewLineCodeLf2Crlf(String source)
        {
            // 単純に "\n" を "\r\n" に置換すると、すでに CRLF になっている箇所が
            // "\r\r\n" に増えてしまう。いったん LF に統一してから変換することで、
            // LF と CRLF が混在していても、何度呼んでも結果が変わらないようにする。
            return ChangeNewLineCodeCrlf2Lf(source).Replace("\n", "\r\n");
        }

        // 改行コードを変換[CRLF→LF]
        public String ChangeNewLineCodeCrlf2Lf(String source)
        {
            return source.Replace("\r\n", "\n");
        }

        // 改行コードを自動変換(Shift_JISならCRLF、それ以外はLF)
        public String ChangeNewLineCode(StcFileInputOutput.EncodingType encodingType, String source)
        {
            return (encodingType == StcFileInputOutput.EncodingType.ShiftJis)
                ? ChangeNewLineCodeLf2Crlf(source)
                : ChangeNewLineCodeCrlf2Lf(source);
        }

        // ダブルクォートをエスケープ[" → \"]
        public String ChangeDoubleQuote2BackSlashDoubleQuote(String source)
        {
            return source.Replace(@"""", @"\""");
        }

        // Linuxパス名を連結
        public String AppendLinuxPathName(String path1, String path2)
        {
            // 以前は Substring(Path1.Length, 0) と Substring(0, 0) で判定していたが、
            // どちらも常に空文字を返すため条件が必ず成立し、区切りを足し続けていた。
            Boolean isEndWithSlash = path1.EndsWith("/");
            Boolean isStartWithSlash = path2.StartsWith("/");

            // 両方にあるなら片方を落とす
            if (isEndWithSlash && isStartWithSlash)
            {
                return path1 + path2.Substring(1);
            }

            // 両方に無いなら足す
            if (!isEndWithSlash && !isStartWithSlash)
            {
                return path1 + "/" + path2;
            }

            // どちらか一方にあるなら、そのまま繋ぐ
            return path1 + path2;
        }

        // 並びをシャッフル(ランダムに並べ替える)
        public String[] ShuffleArray(String[] sources)
        {
            return sources.OrderBy(i => Guid.NewGuid()).ToArray();
        }

        // 特定の文字列を削除
        public String[] RemoveStringArray(String[] sources, String removeText)
        {
            // String.Replace は第1引数が空文字だと ArgumentException を投げる。
            // 「何も削除しない」指定とみなして、そのまま返す。
            if (removeText == String.Empty)
            {
                return sources.ToArray();
            }

            return sources.Select(str => str.Replace(removeText, "")).ToArray();
        }

        //配列→リスト
        public List<String> Array2List(String[] stringArray)
        {
            return new List<String>(stringArray);
        }

        //リスト→配列
        public String[] List2Array(List<String> stringList)
        {
            return stringList.ToArray();
        }

        // 画像サイズ取得
        public Size GetPictSize(String imgFileName)
        {
            using (Bitmap img = new Bitmap(imgFileName))
            {
                return img.Size;
            }
        }

        #endregion

        #region WinFormsコントロール操作(ComboBox/ListBox/ListView/DataGridView/D&D)
        //----------------------------------------------------------
        // コントロール操作 ------------------------------------------
        // テキストコントロールの中身を全て選択
        public void SelectAll(TextBox textBoxCtrl, KeyEventArgs e)
        {
            // 全選択
            if (e.KeyCode == Keys.A && e.Control == true)
            {
                textBoxCtrl.SelectAll();
            }
        }

        // リストコントロールの中身を全て選択
        public void SelectAll(KeyEventArgs e)
        {
            // ガード節にして、以降のネストを浅くした(挙動は変えていない)
            if (e.KeyCode != Keys.A || e.Control != true)
            {
                return;
            }

            // 全選択
            SendKeys.SendWait("{HOME}+{END}");
        }

        public String ChangeStrArray2Linear(String[] strArray, String separator)
        {
            return (strArray != null) ? String.Join(separator, strArray) : "";
        }

        public String[] ChangeStrLinear2Array(String strLinear, String delimiter, StringSplitOptions opt = StringSplitOptions.RemoveEmptyEntries)
        {
            return strLinear.Split(new[] { delimiter }, opt);
        }

        public String TrimDuplication(String source, String delimiter)
        {
            String[] sourceArray = source.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
            String[] destArray = TrimDuplication(sourceArray);
            return ChangeStrArray2Linear(destArray, delimiter);
        }

        // 重複を削除
        public String[] TrimDuplication(String[] sourceArray)
        {
            // ArrayList.Containsは毎回先頭から線形探索するため、要素数が多いとO(n^2)で遅くなる。
            // HashSetなら追加済みかの判定がO(1)になるため、順序を保ったままO(n)で重複除去できる。
            HashSet<String> seen = new HashSet<String>();
            List<String> result = new List<String>(sourceArray.Length);

            //基になる配列の要素を列挙する
            foreach (String i in sourceArray)
            {
                //コレクション内に存在していなければ、追加する
                if (seen.Add(i))
                {
                    result.Add(i);
                }
            }

            //配列に変換する
            return result.ToArray();
        }

        public String[] GetStringArray(ComboBox comboCtrl)
        {
            return comboCtrl.Items.Cast<String>().ToArray();
        }

        public String TrimEndGarbage(String source)
        {
            char[] trimChar = { '\\', '/', '\r', '\n' };
            return source.TrimEnd(trimChar);
        }

        // 以下のようにフォルダパスを整形
        //   TopDirectory・・・C:\
        //   SubDirectory・・・C:\test\sample
        public String AdjustDirectoryName(String srcDirName)
        {
            // 終端の\を削除。
            String destDirName = srcDirName.TrimEnd('\\');

            // フルパス指定かチェック
            int colonIdx = srcDirName.IndexOf(":");
            if (0 <= colonIdx)
            {
                // (例) C: のようにドライブレターだけが指定された場合は、終端に"\"が必要。
                // 以前は @":\" を足していたため "C::\" とコロンが二重になっていた。
                if (destDirName.Length == 2)
                {
                    destDirName += @"\";
                }
            }

            return destDirName;
        }

        // 選択項目を「RootPath\項目名」の形で改行区切りに連結する。項目名の取り出し方だけが
        // ListBox/ListViewで違うため、そこだけ呼び出し元から渡してもらう形に集約した
        private static String JoinSelectedNames(int count, String rootPath, Func<int, String> getItemText)
        {
            StringBuilder targetName = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (rootPath != String.Empty)
                {
                    targetName.Append(rootPath).Append(@"\");
                }
                targetName.Append(getItemText(i)).Append(Environment.NewLine);
            }

            return targetName.ToString();
        }

        public String GetSelectName(ListBox listBoxCtrl, String rootPath = "")
        {
            return JoinSelectedNames(listBoxCtrl.SelectedItems.Count, rootPath,
                i => listBoxCtrl.SelectedItems[i].ToString());
        }

        public String GetSelectListName(ListView listViewCtrl, String rootPath = "", int index = 0)
        {
            return JoinSelectedNames(listViewCtrl.SelectedItems.Count, rootPath,
                i => listViewCtrl.SelectedItems[i].SubItems[index].Text);
        }

        // Ctrl+Cかどうかの判定。ListBox/ListView両方のCopyToClipboardで同じだったため集約した
        private static Boolean IsCopyShortcut(KeyEventArgs e)
        {
            return e.KeyCode == Keys.C && e.Control == true;
        }

        // リストボックスの選択項目をコピー
        public void CopyToClipboard(KeyEventArgs e, ListBox listBoxCtrl, String rootPath = "")
        {
            if (!IsCopyShortcut(e))
            {
                return;
            }

            // コピー
            // (何も選択していない時にクリップボードを空にしないよう、ListView版と同じく空なら何もしない)
            String targetName = GetSelectName(listBoxCtrl, rootPath);
            if (!targetName.Equals(String.Empty))
            {
                SetClipboardText(targetName);
            }
        }

        // クリップボードは他のアプリが掴んでいる間は開けず、Clipboard.SetTextが例外になる
        // (ExternalException「要求されたクリップボード操作に成功しませんでした」)。
        // 少し待って数回やり直し、それでも駄目ならfalseを返す(ツールを落とさない)
        public Boolean SetClipboardText(String text)
        {
            return RetryClipboard(() =>
            {
                // SetTextは空文字を渡すと例外になるため、空のときはクリアする
                if (String.IsNullOrEmpty(text))
                {
                    Clipboard.Clear();
                }
                else
                {
                    Clipboard.SetText(text);
                }
            });
        }

        public Boolean SetClipboardData(Object data)
        {
            return RetryClipboard(() => Clipboard.SetDataObject(data));
        }

        private Boolean RetryClipboard(Action operation)
        {
            const int retryCount = 5;
            const int retryWaitMsec = 100;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    operation();
                    return true;
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    Thread.Sleep(retryWaitMsec);
                }
            }
            return false;
        }

        // リストコントロールの選択項目をコピー
        public void CopyToClipboard(KeyEventArgs e, ListView listViewCtrl, String rootPath = "", int index = 0)
        {
            if (!IsCopyShortcut(e))
            {
                return;
            }

            // コピー
            String targetName = GetSelectListName(listViewCtrl, rootPath, index);
            if (!targetName.Equals(String.Empty))
            {
                SetClipboardText(targetName);
            }
        }

        // リストコントロールの中身をString配列で取得
        public String[] GetStrArrayFromListBox(ListBox.SelectedObjectCollection listBoxSelected)
        {
            // 以前は改行区切りの文字列に連結してから配列へ分割し直す遠回りな実装だった。
            // 素直にLINQで直接配列へ変換する。
            return listBoxSelected.Cast<object>().Select(item => item.ToString()).ToArray();
        }

        // プロファイルをコンボボックスにリストアップ
        // comboCtrlは内部で再代入することはないため、ref不要のComboBox(値渡し)として受け取る。
        // これによりTextBoxEx/ComboBoxEx([[_Common/ComboBoxEx.cs]])のような派生型もそのまま渡せる
        public void UpdateProfileList(ComboBox comboCtrl, String defaultProfileName = "", String directoryPath = "", String fileExtension = "*.xml")
        {
            UpdateProfileList(comboCtrl, new String[] { fileExtension }, defaultProfileName, directoryPath);
        }

        // 設定ファイルをXMLからJSONへ移行中のプロジェクト向けに、複数の拡張子をまとめて一覧できる版
        public void UpdateProfileList(ComboBox comboCtrl, String[] fileExtensions, String defaultProfileName = "", String directoryPath = "")
        {
            if (directoryPath == String.Empty)
            {
                directoryPath = Directory.GetCurrentDirectory();
            }
            else
            {
                if (Directory.Exists(directoryPath) == false)
                {
                    // 指定されたディレクトリが存在しない
                    return;
                }
            }

            // ファイルをリストアップ(アクセス権の無いサブフォルダが1つでもあると例外になるため、その場合は一覧を更新しない)
            List<String> files = new List<String>();
            try
            {
                foreach (String extension in fileExtensions)
                {
                    files.AddRange(Directory.GetFiles(directoryPath, extension, SearchOption.AllDirectories));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return;
            }

            SetComboBoxFromArray(comboCtrl, files.ToArray(), directoryPath);
            SetComboBoxText(comboCtrl, defaultProfileName);
        }

        // 文字配列をコンボボックスにセット
        public void SetComboBoxFromArray(ComboBox comboCtrl, String[] array, String removeString = "", String limitString = "")
        {
            int startIdx = 0;
            if (removeString != String.Empty)
            {
                // 末尾に区切り文字があっても無くても、区切り文字1つ分を足した長さで切り落とす
                startIdx = removeString.TrimEnd('\\', '/').Length + 1;
            }
            SetComboBoxFromArraySubString(comboCtrl, array, startIdx, "", limitString);
        }

        // 文字配列（SubString）をコンボボックスにセット
        public void SetComboBoxFromArraySubString(ComboBox comboCtrl, String[] array, int startIdx, String endDelimiter = "", String limitString = "", Boolean isReverse = false)
        {
            comboCtrl.Items.Clear();
            for (int i = 0; i < array.Length; i++)
            {
                String valueName = array[i];

                // カラ文字
                if (valueName == String.Empty)
                {
                    continue;
                }

                // 文字列生成
                int length;
                if (endDelimiter == String.Empty)
                {
                    // 区切り文字の指定なし(IndexOf("")は0、LastIndexOf("")は末尾側を返して結果がずれるため使わない)
                    length = -1;
                }
                else if (!isReverse)
                {
                    length = valueName.IndexOf(endDelimiter);
                }
                else
                {
                    length = valueName.LastIndexOf(endDelimiter);
                }

                if (length >= 0 && length > startIdx)
                {
                    valueName = valueName.Substring(startIdx, length - startIdx);
                }
                else if (startIdx <= valueName.Length)
                {
                    valueName = valueName.Substring(startIdx);
                }
                else
                {
                    // 切り落とす長さより短い要素は空にする(Substringの例外を避ける)
                    valueName = String.Empty;
                }

                // 文字の絞り込み
                if (limitString != String.Empty)
                {
                    // 大文字小文字を区別せずに部分一致で検索
                    if (valueName.IndexOf(limitString, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                // 登録済みだったらスキップ
                if (IsRegisteredInComboBox(comboCtrl, valueName))
                {
                    continue;
                }

                comboCtrl.Items.Add(valueName);
            }
        }

        // ComboBoxの項目を前から順に見て、条件に最初に一致した項目の文字列を返す(無ければnull)。
        // 「一致するか判定→見つかったらそこで終了」という同じ形のループが3箇所にあったため集約した
        private static String FindComboBoxItem(ComboBox comboCtrl, Func<String, Boolean> isMatch)
        {
            for (int i = 0; i < comboCtrl.Items.Count; i++)
            {
                String itemText = comboCtrl.Items[i].ToString();
                if (isMatch(itemText))
                {
                    return itemText;
                }
            }
            return null;
        }

        // ComboBoxに登録済みかチェック
        private Boolean IsRegisteredInComboBox(ComboBox comboCtrl, String registerText)
        {
            return FindComboBoxItem(comboCtrl, item => item == registerText) != null;
        }

        // 文字列をコンボボックスに設定
        public void SetComboBoxText(ComboBox comboCtrl, String defaultProfileName)
        {
            String profileName = FindComboBoxItem(comboCtrl, item => item == defaultProfileName) ?? "";

            if (profileName == String.Empty && 0 < comboCtrl.Items.Count)
            {
                profileName = comboCtrl.Items[0].ToString();
            }

            comboCtrl.Text = profileName;
        }

        // コンボボックスの中から目的の文字列を探す
        public String FindStringFromComboBox(ComboBox comboCtrl, String srcName, String trimName = "", Boolean isReverse = false)
        {
            String searchName = srcName;
            String destName = "";

            // trimNameが設定されていたら、特定の文字列で区切る
            if (trimName != String.Empty)
            {
                int trimIdx;
                if (!isReverse)
                {
                    trimIdx = srcName.IndexOf(trimName);
                }
                else
                {
                    trimIdx = srcName.LastIndexOf(trimName);
                }

                if (0 <= trimIdx)
                {
                    searchName = srcName.Substring(0, trimIdx);
                }
            }

            if (searchName.Length != 0)
            {
                destName = FindComboBoxItem(comboCtrl, item => item.IndexOf(searchName) != -1) ?? "";
            }
            return destName;
        }

        // ComboBoxのTextをプルダウンに追加
        public void AddComboBoxTextToItems(ComboBox comboCtrl)
        {
            if (comboCtrl.Text == String.Empty)
            {
                return;
            }

            // Text文字をプルダウンに追加
            comboCtrl.Items.Add(comboCtrl.Text);

            // 重複は削除する
            String[] items = TrimDuplication(GetStringArray(comboCtrl));

            // プルダウンを更新
            comboCtrl.Items.Clear();
            comboCtrl.Items.AddRange(items);
        }

        // 数字以外のキーならtrue(KeyPressEventArgs.Handledにそのまま入れて入力を弾く用途)
        public Boolean IsNotNumberKey(KeyPressEventArgs e)
        {
            // Backspace・Ctrl+C/Vなどの制御文字は弾かない
            return !Char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9');
        }

        public void SetDataGridCell(DataGridView dgv, int rowIdx, int columnIdx, String val)
        {
            dgv.Rows[rowIdx].Cells[columnIdx].Value = val;
        }

        public String GetDataGridCell(DataGridView dgv, int rowIdx, int columnIdx)
        {
            Object value = dgv.Rows[rowIdx].Cells[columnIdx].Value;
            return (value != null) ? value.ToString() : "";
        }

        public void SetDragFile(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        public String[] GetDropListArray(DragEventArgs e)
        {
            String[] dropList = { "" };
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                dropList = (String[])e.Data.GetData(DataFormats.FileDrop, false);
            }
            return dropList;
        }

        public String GetDropListLinear(DragEventArgs e)
        {
            String[] dropList = GetDropListArray(e);
            return ChangeStrArray2Linear(dropList, Environment.NewLine);
        }
        #endregion
    }

    // *******************************************************************************
    // プロファイルの保存＆読み込み
    class StcSaveRestore
    {
        class OriginDb
        {
            public String AttrName { get; set; }
            public String AttrValue { get; set; }
            public String DefaultValue { get; set; }

            // typo修正等でAttrValue(実質的な設定キー)を変えたときに、旧キーで保存された
            // 設定ファイルも読めるようにするための読み替え用。無ければnull
            public String LegacyAttrValue { get; set; }

            public void SetParams(String attrName, String attrValue, String defaultValue, String legacyAttrValue = null)
            {
                AttrName = attrName;
                AttrValue = attrValue;
                DefaultValue = defaultValue;
                LegacyAttrValue = legacyAttrValue;
            }

            public Boolean IsExistFullMatch(XmlElement element, String attrName = "", String attrValue = "")
            {
                if (IsExistParam(element, attrName, attrValue, true))
                {
                    return true;
                }

                // 呼び出し元が比較値を明示していない(=このコントロール自身のAttrValueで
                // 比較している)場合のみ、旧キーでの一致も試す
                if (attrValue == String.Empty && LegacyAttrValue != null)
                {
                    return IsExistParam(element, attrName, LegacyAttrValue, true);
                }
                return false;
            }

            public Boolean IsExistPartMatch(XmlElement element, String attrName = "", String attrValue = "")
            {
                return IsExistParam(element, attrName, attrValue, false);
            }

            public Boolean IsExistParam(XmlElement element, String attrName, String attrValue, Boolean isFullCompare)
            {
                if (attrName == String.Empty)
                {
                    attrName = AttrName;
                }

                if (attrValue == String.Empty)
                {
                    attrValue = AttrValue;
                }

                String attribute = element.GetAttribute(attrName);
                if (attribute == String.Empty)
                {
                    return false;
                }

                return isFullCompare
                    ? attribute.Equals(attrValue)
                    : attribute.IndexOf(attrValue) != -1;
            }
        }

        class TextCtrlDb : OriginDb
        {
            public TextBox Ctrl { get; set; }
        }

        class RadioButtonCtrlDb : OriginDb
        {
            public RadioButton Ctrl { get; set; }
        }

        class CheckBoxDb : OriginDb
        {
            public CheckBox Ctrl { get; set; }
        }

        class ComboBoxDb : OriginDb
        {
            public ComboBox Ctrl { get; set; }
        }

        class CheckedListBoxDb : OriginDb
        {
            public CheckedListBox Ctrl { get; set; }
        }

        class HScrollBarDb : OriginDb
        {
            public HScrollBar Ctrl { get; set; }
        }

        class DataGridViewDb : OriginDb
        {
            private static readonly StcUtils _util = new StcUtils();

            public DataGridView Ctrl { get; set; }
            public String AttrCountValue { get; set; }

            // 設定ファイル読み込み
            public void LoadData(String attribute, String elementValue)
            {
                int rowIdx;
                int columnIdx;
                GetDataGridCellIdx(out rowIdx, out columnIdx, attribute);

                Ctrl.Rows[rowIdx].Cells[columnIdx].Value = elementValue;
            }

            /// <summary>
            /// attributeから、CellIdxを取得
            /// </summary>
            /// <param name="rowIdx"></param>
            /// <param name="columnIdx"></param>
            /// <param name="attribute"></param>
            private static void GetDataGridCellIdx(out int rowIdx, out int columnIdx, String attribute)
            {
                // 以前はセル1つ読むたびにStcUtilsをnewしていた。状態を持たないので1つを使い回す
                int rowStartIdx = attribute.IndexOf("_") + 1;
                int rowEndIdx = attribute.IndexOf("-");
                int columnStartIdx = rowEndIdx + 1;

                rowIdx = (int)_util.GetNumber(attribute, rowStartIdx, rowEndIdx - rowStartIdx, "0");

                columnIdx = (int)_util.GetNumber(attribute, columnStartIdx, attribute.Length - rowEndIdx - 1, "0");
            }
        }

        class SecureCtrlDb : TextCtrlDb
        {
            public List<byte> DesKey { get; set; }
            public List<byte> DesIv { get; set; }
            public List<byte> CryptData { get; set; }

            public String SecureAttrName { get; set; }
            public String SecureAttrValueDesKey { get; set; }
            public String SecureAttrValueDesIv { get; set; }
            public String SecureAttrValueCryptData { get; set; }
        }

        // private
        private const String _versionAttrName = "File";
        private const String _versionKeyName = "Version";

        private const String _defaultSecureAttrName = "ManageInfo";
        private const String _defaultSecureAttrValueDesKey = "Key1_";
        private const String _defaultSecureAttrValueDesIv = "Key2_";
        private const String _defaultSecureAttrValueCryptData = "Key3_";

        private const String _versionElementName = "Param";
        private String _elementName = "Param";

        private readonly List<TextCtrlDb> _regTextCtrl = new List<TextCtrlDb>();
        private readonly List<RadioButtonCtrlDb> _regRadioCtrl = new List<RadioButtonCtrlDb>();
        private readonly List<CheckBoxDb> _regCheckCtrl = new List<CheckBoxDb>();
        private readonly List<ComboBoxDb> _regComboCtrl = new List<ComboBoxDb>();
        private readonly List<DataGridViewDb> _regDataGridCtrl = new List<DataGridViewDb>();
        private readonly List<HScrollBarDb> _regHScrollBarCtrl = new List<HScrollBarDb>();
        private readonly List<ComboBoxDb> _regComboCtrlList = new List<ComboBoxDb>();
        private readonly List<CheckedListBoxDb> _regCheckedListBox = new List<CheckedListBoxDb>();
        private readonly List<SecureCtrlDb> _regSecureCtrl = new List<SecureCtrlDb>();

        private XmlDocument _writeDocument = null;
        private XmlElement _writeRoot = null;

        private readonly StcUtils _util = new StcUtils();

        // Element変更
        public void SetElement(String element)
        {
            _elementName = element;
        }

        // コントロール登録
        // 以前は配列をArray.Resizeで1つずつ伸ばしていた(登録のたびに全要素コピー)ため、Listにした
        private static void AddRegistered<TDb>(List<TDb> registeredCtrl, TDb item, String attrName, String attrValue, String defaultValue, String legacyAttrValue = null) where TDb : OriginDb
        {
            item.SetParams(attrName, attrValue, defaultValue, legacyAttrValue);
            registeredCtrl.Add(item);
        }

        public void RegisterCtrl(String attrName, String attrValue, TextBox ctrl, String defaultValue = "", String legacyAttrValue = null)
        {
            AddRegistered(_regTextCtrl, new TextCtrlDb { Ctrl = ctrl }, attrName, attrValue, defaultValue, legacyAttrValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, RadioButton ctrl, String defaultValue = "")
        {
            AddRegistered(_regRadioCtrl, new RadioButtonCtrlDb { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, CheckBox ctrl, String defaultValue = "", String legacyAttrValue = null)
        {
            AddRegistered(_regCheckCtrl, new CheckBoxDb { Ctrl = ctrl }, attrName, attrValue, defaultValue, legacyAttrValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, ComboBox ctrl, String defaultValue = "")
        {
            AddRegistered(_regComboCtrl, new ComboBoxDb { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, String attrCountValue, DataGridView ctrl, String defaultValue = "")
        {
            ctrl.RowCount = 1;
            AddRegistered(_regDataGridCtrl, new DataGridViewDb { Ctrl = ctrl, AttrCountValue = attrCountValue }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, HScrollBar ctrl, int defaultValue = 0)
        {
            AddRegistered(_regHScrollBarCtrl, new HScrollBarDb { Ctrl = ctrl }, attrName, attrValue, defaultValue.ToString());
        }

        public void RegisterCtrlList(String attrName, String attrValue, ComboBox ctrl, String defaultValue = "")
        {
            AddRegistered(_regComboCtrlList, new ComboBoxDb { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrlList(String attrName, String attrValue, CheckedListBox ctrl, String defaultValue = "")
        {
            AddRegistered(_regCheckedListBox, new CheckedListBoxDb { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterSecureCtrl(String attrName, String attrValue, TextBox ctrl, String defaultValue = "")
        {
            AddRegistered(_regSecureCtrl, new SecureCtrlDb
            {
                Ctrl = ctrl,
                DesKey = new List<byte>(),
                DesIv = new List<byte>(),
                CryptData = new List<byte>(),
                SecureAttrName = _defaultSecureAttrName,
                SecureAttrValueDesKey = _defaultSecureAttrValueDesKey,
                SecureAttrValueDesIv = _defaultSecureAttrValueDesIv,
                SecureAttrValueCryptData = _defaultSecureAttrValueCryptData,
            }, attrName, attrValue, defaultValue);
        }

        // コントロール初期値設定
        private void ResetCtrlsToDefault()
        {
            // [TextCtrl]
            for (int i = 0; i < _regTextCtrl.Count; i++)
            {
                _regTextCtrl[i].Ctrl.Text = _regTextCtrl[i].DefaultValue;
            }

            // [RadioButton]
            for (int i = 0; i < _regRadioCtrl.Count; i++)
            {
                _regRadioCtrl[i].Ctrl.Checked = _util.GetBoolean(_regRadioCtrl[i].DefaultValue);
            }

            // [CheckBox]
            for (int i = 0; i < _regCheckCtrl.Count; i++)
            {
                _regCheckCtrl[i].Ctrl.Checked = _util.GetBoolean(_regCheckCtrl[i].DefaultValue);
            }

            // [ComboBox]
            for (int i = 0; i < _regComboCtrl.Count; i++)
            {
                _regComboCtrl[i].Ctrl.Text = _regComboCtrl[i].DefaultValue;
            }

            // [ComboBox]リスト
            for (int i = 0; i < _regComboCtrlList.Count; i++)
            {
                _regComboCtrlList[i].Ctrl.Items.Clear();
            }

            // [CheckedListBoxList]
            for (int i = 0; i < _regCheckedListBox.Count; i++)
            {
                _regCheckedListBox[i].Ctrl.Items.Clear();
            }

            // [SecureCtrl]
            for (int i = 0; i < _regSecureCtrl.Count; i++)
            {
                _regSecureCtrl[i].DesKey.Clear();
                _regSecureCtrl[i].DesIv.Clear();
                _regSecureCtrl[i].CryptData.Clear();
                _regSecureCtrl[i].Ctrl.Text = _regSecureCtrl[i].DefaultValue;
            }
        }

        // ファイル名が空でなければ LoadXmlFile を実行する。
        //
        // 多くのプロジェクトの SaveRestore.cs（StcSaveRestore の派生クラス）で
        // 「ファイル名が空なら false を返して何もしない、そうでなければ LoadXmlFile を呼ぶ」
        // という同じ形の LoadProc がコピペされていたので、ここに集約した。
        // Parent の状態を使った追加処理（デフォルト値の設定など）が必要なプロジェクトは、
        // このメソッドと同名の LoadProc(string, Parent) を派生クラス側に定義すればよい
        // （C# のメソッド隠蔽により、そちらが優先して呼ばれる）。
        public Boolean LoadProc(String loadFileName)
        {
            if (loadFileName == String.Empty)
            {
                return false;
            }
            return LoadXmlFile(loadFileName);
        }

        // ファイル名が空でなければ SaveXmlFile を実行する。LoadProc と対になる形。
        public Boolean SaveSetting(String saveFileName)
        {
            if (saveFileName == String.Empty)
            {
                return false;
            }
            return SaveXmlFile(saveFileName);
        }

        // コントロール読み込み[一括]
        public Boolean LoadXmlFile(String fileName)
        {
            // 初期値を設定
            ResetCtrlsToDefault();

            if (fileName == String.Empty)
            {
                return false;
            }

            if (!File.Exists(fileName))
            {
                return false;
            }

            // 設定ファイルが壊れていても起動できるよう、途中で失敗したら全項目を初期値に戻して false を返す
            try
            {
                // 同じファイルをLoadSecureCodeと設定値読み込みでそれぞれ個別にXmlDocument.Load
                // していたため、ファイルI/OとXMLパースが2回走っていた。1回読み込んだ
                // XmlDocumentを両方で使い回すことで1回にまとめる。
                XmlDocument document = new XmlDocument();
                document.Load(fileName);

                // 管理情報は先に読む
                Boolean useSecure = UseSecureCtrl();
                if (useSecure)
                {
                    LoadSecureCode(document);
                }

                // 設定値を読む
                foreach (XmlElement element in document.DocumentElement.ChildNodes.OfType<XmlElement>())
                {
                    String elementValue = element.InnerText;
                    if (LoadTextCtrl(element, elementValue)) { continue; }
                    if (LoadCheckCtrl(element, elementValue)) { continue; }
                    if (LoadRadioCtrl(element, elementValue)) { continue; }
                    if (LoadComboCtrl(element, elementValue)) { continue; }
                    if (LoadComboCtrlList(element, elementValue)) { continue; }
                    if (LoadCheckedListBoxCtrl(element, elementValue)) { continue; }
                    if (LoadDataGridCtrl(element, elementValue)) { continue; }
                    if (LoadHScrollBarCtrl(element, elementValue)) { continue; }
                    if (useSecure && LoadSecureCtrl(element, elementValue)) { continue; }
                }
                return true;
            }
            catch (Exception)
            {
                ResetCtrlsToDefault();
                return false;
            }
        }

        // コントロール読み込み[個別]
        public String LoadXmlFile(String fileName, String attrName, String attrValue, String defaultValue = "")
        {
            XmlDocument document = TryLoadXmlDocument(fileName);
            if (document == null)
            {
                return defaultValue;
            }

            foreach (XmlElement element in FindElements(document, attrName, attrValue, false))
            {
                return element.InnerText;
            }
            return defaultValue;
        }

        // 属性値が一致する要素を順に返す(isFullMatchがfalseなら部分一致)
        private static IEnumerable<XmlElement> FindElements(XmlDocument document, String attrName, String attrValue, Boolean isFullMatch)
        {
            foreach (XmlElement element in document.DocumentElement.ChildNodes.OfType<XmlElement>())
            {
                String attribute = element.GetAttribute(attrName);
                Boolean isMatch = isFullMatch
                    ? attribute.Equals(attrValue)
                    : (attribute != String.Empty && attribute.IndexOf(attrValue) != -1);
                if (isMatch)
                {
                    yield return element;
                }
            }
        }

        // コントロール読み込み[個別&リスト]
        public String[] LoadXmlFileList(String fileName, String attrName, String attrValue)
        {
            XmlDocument document = TryLoadXmlDocument(fileName);
            if (document == null)
            {
                return new String[] { };
            }

            List<String> values = new List<String>();
            foreach (XmlElement element in FindElements(document, attrName, attrValue, false))
            {
                values.Add(element.InnerText);
            }
            return values.ToArray();
        }

        /// <summary>
        /// 登録済みコントロールの中から、この XML 要素に対応するものを探して値を反映する。
        /// 見つけたら反映して true を返し、呼び出し元（LoadXmlFile）のループを次の要素へ進める。
        ///
        /// 種類ごとに同じ形の for ループが並んでいたのを 1 つにまとめたもの。
        /// 種類による違いは「どう照合するか(IsMatch)」と「何を代入するか(Apply)」だけ。
        /// </summary>
        private static Boolean LoadRegisteredCtrl<T>(List<T> registeredCtrl, Func<T, Boolean> isMatch, Action<T> apply) where T : OriginDb
        {
            foreach (T ctrl in registeredCtrl)
            {
                if (isMatch(ctrl))
                {
                    apply(ctrl);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[TextBox]
        private Boolean LoadTextCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(_regTextCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Text = elementValue);
        }

        // 設定ファイル読み込み[RadioButton]
        private Boolean LoadRadioCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(_regRadioCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Checked = _util.GetBoolean(elementValue));
        }

        // 設定ファイル読み込み[CheckBox]
        private Boolean LoadCheckCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(_regCheckCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Checked = _util.GetBoolean(elementValue));
        }

        // 設定ファイル読み込み[ComboBox]
        private Boolean LoadComboCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(_regComboCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Text = elementValue);
        }

        // 設定ファイル読み込み[ComboBoxの履歴一覧]
        private Boolean LoadComboCtrlList(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(_regComboCtrlList,
                ctrl => ctrl.IsExistPartMatch(element),
                ctrl => ctrl.Ctrl.Items.Add(elementValue));
        }

        // 設定ファイル読み込み[CheckedListBox]
        private Boolean LoadCheckedListBoxCtrl(XmlElement element, String elementValue)
        {
            // 属性値は "<AttrValue>-<項目名>" の形で入っているので、前半で照合して後半を項目名として使う
            return LoadRegisteredCtrl(_regCheckedListBox,
                ctrl => ctrl.IsExistPartMatch(element, ctrl.AttrName, ctrl.AttrValue + "-"),
                ctrl =>
                {
                    String attribute = element.GetAttribute(ctrl.AttrName);
                    int itemNameIdx = ctrl.AttrValue.Length + 1;

                    // 名前
                    ctrl.Ctrl.Items.Add(attribute.Substring(itemNameIdx));

                    // 状態
                    Boolean isChecked = _util.GetBoolean(elementValue, "Checked");
                    ctrl.Ctrl.SetItemChecked(ctrl.Ctrl.Items.Count - 1, isChecked);
                });
        }

        // 設定ファイル読み込み[DataGridView]
        private Boolean LoadDataGridCtrl(XmlElement element, String elementValue)
        {
            // ここだけは照合が 2 通り（セルの値と行数）あるので、まとめずに残してある
            foreach (DataGridViewDb grid in _regDataGridCtrl)
            {
                if (grid.IsExistPartMatch(element))
                {
                    grid.LoadData(element.GetAttribute(grid.AttrName), elementValue);
                    return true;
                }

                if (grid.IsExistPartMatch(element, grid.AttrName, grid.AttrCountValue))
                {
                    grid.Ctrl.RowCount = _util.GetInteger(elementValue);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[HScrollBar]
        private Boolean LoadHScrollBarCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(_regHScrollBarCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Value = int.Parse(elementValue));
        }

        // 設定ファイル読み込み[SecureCtrl]
        private Boolean LoadSecureCtrl(XmlElement element, String elementValue)
        {
            foreach (SecureCtrlDb secureCtrl in _regSecureCtrl)
            {
                if (secureCtrl.IsExistFullMatch(element))
                {
                    if (!IsExistSecureCode(secureCtrl))
                    {
                        return false;
                    }

                    secureCtrl.Ctrl.Text = GetDecodeString(secureCtrl);
                    return true;
                }
            }
            return false;
        }

        private static String GetDecodeString(SecureCtrlDb secureCtrl)
        {
            StcSecure secure = new StcSecure();

            return secure.Decode(
                secureCtrl.DesKey.ToArray(),
                secureCtrl.DesIv.ToArray(),
                secureCtrl.CryptData.ToArray());
        }

        private static Boolean IsExistSecureCode(SecureCtrlDb secure)
        {
            return secure.DesKey.Count > 0 && secure.DesIv.Count > 0 && secure.CryptData.Count > 0;
        }

        private Boolean UseSecureCtrl()
        {
            // 管理情報がなければ読まない
            return _regSecureCtrl.Count != 0;
        }

        private void LoadSecureCode(XmlDocument document)
        {
            foreach (XmlElement element in document.DocumentElement.ChildNodes.OfType<XmlElement>())
            {
                String elementValue = element.InnerText;
                foreach (SecureCtrlDb secureCtrl in _regSecureCtrl)
                {
                    if (secureCtrl.IsExistPartMatch(element, secureCtrl.SecureAttrName, _defaultSecureAttrValueDesKey))
                    {
                        secureCtrl.DesKey.Add(Convert.ToByte(elementValue));
                    }
                    else if (secureCtrl.IsExistPartMatch(element, secureCtrl.SecureAttrName, _defaultSecureAttrValueDesIv))
                    {
                        secureCtrl.DesIv.Add(Convert.ToByte(elementValue));
                    }
                    else if (secureCtrl.IsExistPartMatch(element, secureCtrl.SecureAttrName, _defaultSecureAttrValueCryptData))
                    {
                        secureCtrl.CryptData.Add(Convert.ToByte(elementValue));
                    }
                }
            }
        }

        /// 設定ファイルの読み込み[バージョン]
        public int LoadXmlVersion(String fileName)
        {
            XmlDocument document = TryLoadXmlDocument(fileName);
            if (document == null)
            {
                return 0;
            }
            return LoadXmlVersion(document);
        }

        // ファイルが無い・読めない・XMLとして壊れている場合は null を返す
        private static XmlDocument TryLoadXmlDocument(String fileName)
        {
            if (!File.Exists(fileName))
            {
                return null;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(fileName);
                return document.DocumentElement == null ? null : document;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException)
            {
                return null;
            }
        }

        // 設定ファイルの読み込み[バージョン]
        public int LoadXmlVersion(XmlDocument document)
        {
            int versionNo = 0;
            foreach (XmlElement element in FindElements(document, _versionAttrName, _versionKeyName, true))
            {
                int.TryParse(element.InnerText, out versionNo);
                break;
            }
            return versionNo;
        }

        /// <summary>
        /// Save用にXmlファイルをオープンする
        /// </summary>
        /// <returns></returns>
        public XmlDocument OpenSaveXmlFile()
        {
            return new XmlDocument();
        }

        public Boolean CloseSaveXmlFile(String fileName)
        {
            try
            {
                AtomicFile.Write(fileName, _writeDocument.Save);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Open＆Write＆Save
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="versionNo"></param>
        /// <returns></returns>
        public Boolean SaveXmlFile(String fileName, String versionNo = "1")
        {
            XmlDocument document = OpenSaveXmlFile();
            SaveXmlFile(document, versionNo);
            return CloseSaveXmlFile(fileName);
        }

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="document"></param>
        /// <param name="versionNo"></param>
        public void SaveXmlFile(XmlDocument document, String versionNo = "1")
        {
            _writeDocument = document;

            SetRoot();
            SaveXmlVersion(versionNo);

            SaveTextCtrl();
            SaveRadioCtrl();
            SaveCheckCtrl();
            SaveComboCtrl();
            SaveComboCtrlList();
            SaveCheckedListBox();
            SaveDataGridCtrl();
            SaveHScrollBarCtrl();
            SaveSecureCtrl();
        }

        // 設定ファイルの保存[ルート]
        private void SetRoot()
        {
            XmlDeclaration declaration = _writeDocument.CreateXmlDeclaration("1.0", null, null);  // XML宣言
            _writeRoot = _writeDocument.CreateElement("root");  // ルート要素

            _writeDocument.AppendChild(declaration);
            _writeDocument.AppendChild(_writeRoot);
        }

        // 設定ファイルの保存[バージョン]
        private void SaveXmlVersion(String version)
        {
            SaveXmlString(_versionElementName, _versionAttrName, _versionKeyName, version);
        }

        // パラメータ保存 /////////////////////////////////////////
        // ファイル保存[String]
        public void SaveXmlString(String elementName, String attrName, String attrValue, String text)
        {
            SaveXmlString(_writeDocument, _writeRoot, elementName, attrName, attrValue, text);
        }

        // ファイル保存[String]
        public void SaveXmlString(String attrName, String attrValue, String text)
        {
            SaveXmlString(_elementName, attrName, attrValue, text);
        }

        // ファイル保存[StringArray]
        public void SaveXmlParamAll(String attrName, String attrValue, String[] strArray)
        {
            if (strArray == null)
            {
                return;
            }

            for (int i = 0; i < strArray.Length; i++)
            {
                SaveXmlString(attrName, attrValue + i.ToString(), strArray[i]);
            }
        }

        // ファイル保存[int]
        public void SaveXmlParam(String attrName, String attrValue, int number)
        {
            SaveXmlString(attrName, attrValue, number.ToString());
        }

        // ファイル保存[byte]
        // 暗号化した管理情報(鍵・IV・データ)の保存にしか使わないのでprivate
        private void SaveXmlManageParam(String attrName, String attrValue, byte[] values)
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                SaveXmlString(attrName, attrValue + i.ToString(), values[i].ToString());
            }
        }

        // ファイル保存[String]
        public void SaveXmlString(XmlDocument writeDocument, XmlElement writeRoot, String elementName, String attrName, String attrValue, String text)
        {
            XmlElement element = writeDocument.CreateElement(elementName);
            element.SetAttribute(attrName, attrValue);
            element.InnerText = text;
            writeRoot.AppendChild(element);
        }

        // コントロール保存 /////////////////////////////////////////
        /// <summary>
        /// 登録済みコントロールを 1 つずつ、属性名・属性値・現在値の組で書き出す。
        /// 種類による違いは「現在値をどう文字列にするか(GetValue)」だけ。
        ///
        /// 1 つのコントロールから複数行を書き出すもの（ComboBox の履歴一覧、CheckedListBox、
        /// DataGridView）は内側にもループがあって形が違うので、ここには通していない。
        /// 無理に共通化すると渡すデリゲートが増えてかえって読みにくくなるため。
        /// </summary>
        private void SaveRegisteredCtrl<T>(List<T> registeredCtrl, Func<T, String> getValue) where T : OriginDb
        {
            foreach (T ctrl in registeredCtrl)
            {
                SaveXmlString(ctrl.AttrName, ctrl.AttrValue, getValue(ctrl));
            }
        }

        // ファイル保存[TextBox]
        private void SaveTextCtrl()
        {
            SaveRegisteredCtrl(_regTextCtrl, ctrl => ctrl.Ctrl.Text);
        }

        // ファイル保存[RadioButton]
        private void SaveRadioCtrl()
        {
            SaveRegisteredCtrl(_regRadioCtrl, ctrl => ctrl.Ctrl.Checked.ToString());
        }

        // ファイル保存[CheckBox]
        private void SaveCheckCtrl()
        {
            SaveRegisteredCtrl(_regCheckCtrl, ctrl => ctrl.Ctrl.Checked.ToString());
        }

        // ファイル保存[ComboBox]
        private void SaveComboCtrl()
        {
            SaveRegisteredCtrl(_regComboCtrl, ctrl => ctrl.Ctrl.Text);
        }

        // ファイル保存[ComboBox]の中身
        private void SaveComboCtrlList()
        {
            for (int i = 0; i < _regComboCtrlList.Count; i++)
            {
                for (int j = 0; j < _regComboCtrlList[i].Ctrl.Items.Count; j++)
                {
                    SaveXmlString(_regComboCtrlList[i].AttrName,
                        _regComboCtrlList[i].AttrValue + j.ToString(),
                        _regComboCtrlList[i].Ctrl.Items[j].ToString());
                }
            }
        }

        // ファイル保存[CheckListBox]
        private void SaveCheckedListBox()
        {
            for (int i = 0; i < _regCheckedListBox.Count; i++)
            {
                for (int j = 0; j < _regCheckedListBox[i].Ctrl.Items.Count; j++)
                {
                    // 状態
                    SaveXmlString(_regCheckedListBox[i].AttrName,
                        _regCheckedListBox[i].AttrValue + "-" + _regCheckedListBox[i].Ctrl.Items[j].ToString(),
                        _regCheckedListBox[i].Ctrl.GetItemCheckState(j).ToString());
                }
            }
        }

        // ファイル保存[dataGrid]
        private void SaveDataGridCtrl()
        {
            for (int i = 0; i < _regDataGridCtrl.Count; i++)
            {
                SaveXmlParam(_regDataGridCtrl[i].AttrName, _regDataGridCtrl[i].AttrCountValue, _regDataGridCtrl[i].Ctrl.RowCount);
                for (int rowIdx = 0; rowIdx < _regDataGridCtrl[i].Ctrl.RowCount; rowIdx++)
                {
                    for (int columnIdx = 0; columnIdx < _regDataGridCtrl[i].Ctrl.ColumnCount; columnIdx++)
                    {
                        String cellValue = _util.GetDataGridCell(_regDataGridCtrl[i].Ctrl, rowIdx, columnIdx);

                        SaveXmlString(_regDataGridCtrl[i].AttrName,
                            _regDataGridCtrl[i].AttrValue + "_" + rowIdx.ToString() + "-" + columnIdx.ToString(),
                            cellValue);
                    }
                }
            }
        }

        // ファイル保存[HScrollBar]
        private void SaveHScrollBarCtrl()
        {
            SaveRegisteredCtrl(_regHScrollBarCtrl, ctrl => ctrl.Ctrl.Value.ToString());
        }

        // ファイル保存[暗号化キー]
        private void SaveSecureCtrl()
        {
            StcSecure secure = new StcSecure();

            byte[] desKey;
            byte[] desIv;
            byte[] cryptData;
            String secureWord;
            for (int i = 0; i < _regSecureCtrl.Count; i++)
            {
                secureWord = secure.Encode(_regSecureCtrl[i].Ctrl.Text, out desKey, out desIv, out cryptData);
                SaveXmlString(_regSecureCtrl[i].AttrName, _regSecureCtrl[i].AttrValue, secureWord);

                SaveXmlManageParam(_defaultSecureAttrName, _defaultSecureAttrValueDesKey, desKey);
                SaveXmlManageParam(_defaultSecureAttrName, _defaultSecureAttrValueDesIv, desIv);
                SaveXmlManageParam(_defaultSecureAttrName, _defaultSecureAttrValueCryptData, cryptData);
            }
        }

        // *******************************************************************************
        // JSON汎用プロファイル([[_Common/JsonFileStorage.cs]]と組み合わせて使う)
        //
        // EventRecorderのように保存したいデータに意味の分かる名前を付けられる場合は専用の
        // POCO(Profile.cs)を作る方が読みやすいが、RegisterCtrlで何十個ものコントロールを
        // 登録しているだけの既存プロジェクト(Cheetos/FileArranger等)では、フィールドごとに
        // 専用POCOを手書きすると数が多くズレの元になる。ここでは「登録済みコントロールの
        // 現在値」をそのままキー(AttrName+AttrValue、コントロール名由来なので読める)付きの
        // 汎用データに詰め替えるだけの橋渡しを用意し、個別のPOCOを作らずに済むようにする。
        //
        // このクラス自体はNewtonsoft.Jsonに依存しない(GenericProfileはDictionary/Listだけの
        // 素のPOCO)。実際のJSON読み書きは、JsonFileStorage.csをリンクした各プロジェクトの
        // SaveRestore.cs側でJsonFileStorage.Save/Load<GenericProfile>を呼ぶ形にすることで、
        // Newtonsoft.Jsonへの依存をJSON対応したプロジェクトだけに閉じ込めている。
        public GenericProfile BuildGenericProfile()
        {
            GenericProfile profile = new GenericProfile();

            for (int i = 0; i < _regTextCtrl.Count; i++) { profile.Values[GenericKey(_regTextCtrl[i])] = _regTextCtrl[i].Ctrl.Text; }
            for (int i = 0; i < _regRadioCtrl.Count; i++) { profile.Values[GenericKey(_regRadioCtrl[i])] = _regRadioCtrl[i].Ctrl.Checked.ToString(); }
            for (int i = 0; i < _regCheckCtrl.Count; i++) { profile.Values[GenericKey(_regCheckCtrl[i])] = _regCheckCtrl[i].Ctrl.Checked.ToString(); }
            for (int i = 0; i < _regComboCtrl.Count; i++) { profile.Values[GenericKey(_regComboCtrl[i])] = _regComboCtrl[i].Ctrl.Text; }
            for (int i = 0; i < _regHScrollBarCtrl.Count; i++) { profile.Values[GenericKey(_regHScrollBarCtrl[i])] = _regHScrollBarCtrl[i].Ctrl.Value.ToString(); }

            for (int i = 0; i < _regComboCtrlList.Count; i++)
            {
                List<String> items = new List<String>();
                foreach (Object item in _regComboCtrlList[i].Ctrl.Items) { items.Add(item.ToString()); }
                profile.Lists[GenericKey(_regComboCtrlList[i])] = items;
            }

            for (int i = 0; i < _regCheckedListBox.Count; i++)
            {
                List<String> items = new List<String>();
                Dictionary<String, Boolean> checkedStates = new Dictionary<String, Boolean>();
                for (int j = 0; j < _regCheckedListBox[i].Ctrl.Items.Count; j++)
                {
                    String itemName = _regCheckedListBox[i].Ctrl.Items[j].ToString();
                    items.Add(itemName);
                    checkedStates[itemName] = _regCheckedListBox[i].Ctrl.GetItemChecked(j);
                }
                profile.Lists[GenericKey(_regCheckedListBox[i])] = items;
                profile.CheckedStates[GenericKey(_regCheckedListBox[i])] = checkedStates;
            }

            for (int i = 0; i < _regDataGridCtrl.Count; i++)
            {
                List<List<String>> rows = new List<List<String>>();
                for (int r = 0; r < _regDataGridCtrl[i].Ctrl.RowCount; r++)
                {
                    List<String> row = new List<String>();
                    for (int c = 0; c < _regDataGridCtrl[i].Ctrl.ColumnCount; c++)
                    {
                        row.Add(_util.GetDataGridCell(_regDataGridCtrl[i].Ctrl, r, c));
                    }
                    rows.Add(row);
                }
                profile.Grids[GenericKey(_regDataGridCtrl[i])] = rows;
            }

            // SecureCtrl(暗号化して保存するパスワード等)。鍵・IV・暗号文をBase64にして「:」でつなぐ
            StcSecure secure = new StcSecure();
            for (int i = 0; i < _regSecureCtrl.Count; i++)
            {
                byte[] desKey;
                byte[] desIv;
                byte[] cryptData;
                secure.Encode(_regSecureCtrl[i].Ctrl.Text, out desKey, out desIv, out cryptData);
                profile.Secrets[GenericKey(_regSecureCtrl[i])] =
                    Convert.ToBase64String(desKey) + ":" + Convert.ToBase64String(desIv) + ":" + Convert.ToBase64String(cryptData);
            }
            return profile;
        }

        // profileの内容を登録済みコントロールへ反映する(BuildGenericProfileの逆)。
        // まずResetCtrlsToDefaultで初期値に戻してから、profileにある値だけ上書きする
        // (XML読込のLoadXmlFileと同じ、見つからない項目は初期値のまま残す方針)
        public void ApplyGenericProfile(GenericProfile profile)
        {
            ResetCtrlsToDefault();
            if (profile == null)
            {
                return;
            }

            String value;
            for (int i = 0; i < _regTextCtrl.Count; i++) { if (TryGetGenericValue(profile, _regTextCtrl[i], out value)) { _regTextCtrl[i].Ctrl.Text = value; } }
            for (int i = 0; i < _regRadioCtrl.Count; i++) { if (TryGetGenericValue(profile, _regRadioCtrl[i], out value)) { _regRadioCtrl[i].Ctrl.Checked = _util.GetBoolean(value); } }
            for (int i = 0; i < _regCheckCtrl.Count; i++) { if (TryGetGenericValue(profile, _regCheckCtrl[i], out value)) { _regCheckCtrl[i].Ctrl.Checked = _util.GetBoolean(value); } }
            for (int i = 0; i < _regComboCtrl.Count; i++) { if (TryGetGenericValue(profile, _regComboCtrl[i], out value)) { _regComboCtrl[i].Ctrl.Text = value; } }
            for (int i = 0; i < _regHScrollBarCtrl.Count; i++) { if (TryGetGenericValue(profile, _regHScrollBarCtrl[i], out value)) { _regHScrollBarCtrl[i].Ctrl.Value = _util.GetInteger(value); } }

            for (int i = 0; i < _regComboCtrlList.Count; i++)
            {
                _regComboCtrlList[i].Ctrl.Items.Clear();
                List<String> items;
                if (profile.Lists.TryGetValue(GenericKey(_regComboCtrlList[i]), out items))
                {
                    foreach (String item in items) { _regComboCtrlList[i].Ctrl.Items.Add(item); }
                }
            }

            for (int i = 0; i < _regCheckedListBox.Count; i++)
            {
                _regCheckedListBox[i].Ctrl.Items.Clear();
                List<String> items;
                if (profile.Lists.TryGetValue(GenericKey(_regCheckedListBox[i]), out items))
                {
                    Dictionary<String, Boolean> checkedStates;
                    profile.CheckedStates.TryGetValue(GenericKey(_regCheckedListBox[i]), out checkedStates);

                    foreach (String item in items)
                    {
                        int idx = _regCheckedListBox[i].Ctrl.Items.Add(item);
                        Boolean isChecked;
                        if (checkedStates == null || !checkedStates.TryGetValue(item, out isChecked))
                        {
                            isChecked = false;
                        }
                        _regCheckedListBox[i].Ctrl.SetItemChecked(idx, isChecked);
                    }
                }
            }

            for (int i = 0; i < _regDataGridCtrl.Count; i++)
            {
                _regDataGridCtrl[i].Ctrl.RowCount = 1;
                List<List<String>> rows;
                if (profile.Grids.TryGetValue(GenericKey(_regDataGridCtrl[i]), out rows) && rows.Count > 0)
                {
                    _regDataGridCtrl[i].Ctrl.RowCount = rows.Count;
                    for (int r = 0; r < rows.Count; r++)
                    {
                        for (int c = 0; c < rows[r].Count && c < _regDataGridCtrl[i].Ctrl.ColumnCount; c++)
                        {
                            _regDataGridCtrl[i].Ctrl.Rows[r].Cells[c].Value = rows[r][c];
                        }
                    }
                }
            }

            ApplySecrets(profile);
        }

        // BuildGenericProfileで保存したSecureCtrlを復号して反映する。保存が無い/壊れている時は既定値のまま
        private void ApplySecrets(GenericProfile profile)
        {
            StcSecure secure = new StcSecure();
            for (int i = 0; i < _regSecureCtrl.Count; i++)
            {
                String packed;
                if (profile.Secrets == null || !profile.Secrets.TryGetValue(GenericKey(_regSecureCtrl[i]), out packed))
                {
                    continue;
                }

                try
                {
                    String[] parts = packed.Split(':');
                    _regSecureCtrl[i].Ctrl.Text = secure.Decode(
                        Convert.FromBase64String(parts[0]), Convert.FromBase64String(parts[1]), Convert.FromBase64String(parts[2]));
                }
                catch (Exception ex) when (ex is FormatException || ex is IndexOutOfRangeException || ex is System.Security.Cryptography.CryptographicException)
                {
                }
            }
        }

        // GenericProfileのキー(コントロール名由来なので、旧XMLの「Cell_行-列」方式より読める)
        private static String GenericKey(OriginDb ctrl)
        {
            return GenericProfile.MakeKey(ctrl.AttrName, ctrl.AttrValue);
        }

        // GenericKeyで見つからなければ、LegacyAttrValue(旧キー)でも探す。
        // typo修正等でAttrValueを変えたコントロールでも、旧キーで保存されたJSONを読めるようにするため
        private static Boolean TryGetGenericValue(GenericProfile profile, OriginDb ctrl, out String value)
        {
            if (profile.Values.TryGetValue(GenericKey(ctrl), out value))
            {
                return true;
            }

            if (ctrl.LegacyAttrValue != null)
            {
                return profile.Values.TryGetValue(GenericProfile.MakeKey(ctrl.AttrName, ctrl.LegacyAttrValue), out value);
            }

            value = null;
            return false;
        }
    }

    // JSON汎用プロファイルのデータ本体([[_Common/JsonFileStorage.cs]]でシリアライズする)。
    // BuildGenericProfile/ApplyGenericProfile専用で、StcSaveRestore自体はこの型を
    // 経由してもNewtonsoft.Jsonには依存しない(依存するのはJsonFileStorage.Save/Loadを
    // 呼び出す側=各プロジェクトのSaveRestore.cs)
    public class GenericProfile
    {
        // TextBox/RadioButton/CheckBox/ComboBox/HScrollBarの現在値。キーは"AttrName|AttrValue"
        public Dictionary<String, String> Values { get; set; } = new Dictionary<String, String>();

        // ComboBoxの履歴一覧・CheckedListBoxの項目名一覧。キーは"AttrName|AttrValue"
        public Dictionary<String, List<String>> Lists { get; set; } = new Dictionary<String, List<String>>();

        // CheckedListBoxの各項目のチェック状態。キーは"AttrName|AttrValue"、値は項目名→チェック有無
        public Dictionary<String, Dictionary<String, Boolean>> CheckedStates { get; set; } = new Dictionary<String, Dictionary<String, Boolean>>();

        // DataGridViewの行データ(行×列の文字列)。キーは"AttrName|AttrValue"
        public Dictionary<String, List<List<String>>> Grids { get; set; } = new Dictionary<String, List<List<String>>>();

        // SecureCtrl(パスワード等)の暗号化データ。値は「鍵:IV:暗号文」(各Base64)。キーは"AttrName|AttrValue"
        public Dictionary<String, String> Secrets { get; set; } = new Dictionary<String, String>();

        // 保存キー"AttrName|AttrValue"を作る。WinForms版(StcSaveRestore)とWPF版(WpfSaveRestore)で
        // 同じキーにしないと互いのプロファイルを読めなくなるため、組み立て方をここ1か所にまとめた
        public static String MakeKey(String attrName, String attrValue)
        {
            return attrName + "|" + attrValue;
        }
    }

    // *******************************************************************************
    // Form1コンストラクタの定型処理(アイコン設定/カレントディレクトリ移動)をまとめた
    // 共通基底クラス。各プロジェクトのForm1は
    //   partial class Form1 : StcBaseForm<SaveRestore>
    // のように継承し、_util/_srフィールドはこちら側の物をそのまま使う。
    //
    // TSaveRestoreは各プロジェクト固有の"class SaveRestore : StcSaveRestore"を渡す想定。
    //
    // ここにまとめたのは「アイコン設定→カレントディレクトリ移動」の2行だけ。
    // RegisterItem(Form1 Parent)はプロジェクト固有のForm1型を直接引数に取るためジェネリック
    // からは呼べない。LoadProc/SaveSettingもFFEdit等の一部プロジェクトでは
    // (String, Form1)の2引数オーバーロードに副作用付きで差し替えられており、
    // 基底クラス側で1引数版を固定で呼んでしまうと差し替え版が呼ばれず挙動が変わってしまう。
    // そのためRegisterItem/LoadProc/SaveSettingの呼び出しは今まで通り各Form1コンストラクタに
    // 明示的に書く方針とし、プロジェクトによらず完全に同一だった2行だけを集約している。
    abstract class StcBaseForm<TSaveRestore> : Form
        where TSaveRestore : StcSaveRestore, new()
    {
        protected StcUtils _util = new StcUtils();
        protected TSaveRestore _sr = new TSaveRestore();

        protected void InitializeCommonSettings(Icon formIcon)
        {
            this.Icon = formIcon;
            _util.SetCurrentDirectory();
        }
    }

    // *******************************************************************************
    // セキュリティ
    public partial class StcSecure
    {
        // かつてここには「鍵とIVをソースに直書きした固定鍵版」のEncode(String)/Decode(String)が
        // あったが、本番の17プロジェクトからは一切使われておらず、実際に使われているのは
        // 下のEncode(str, out key, out iv, out data)(呼び出しごとに鍵を新規生成し、
        // 鍵ごと暗号文と一緒に保存する方式。StcSaveRestore.RegisterSecureCtrl経由でパスワード
        // 保存等に使われている)だけだった。「固定鍵をソースに書く」という誤った見本を
        // 残さないため、未使用だった固定鍵版は削除した(パフォーマンス改善#8で対応)。

        // 暗号化(鍵付き)。鍵とIVは呼び出しごとに新しく生成したものを返す
        public String Encode(String str, out byte[] desKey, out byte[] desIv, out byte[] cryptData)
        {
            // 以前は鍵生成用と暗号化用でTripleDESCryptoServiceProviderを2つ作っていたが、
            // CreateEncryptorには鍵とIVを明示的に渡しているので1つで足りる
            using (TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider())
            {
                desKey = des.Key;
                desIv = des.IV;

                byte[] source = Encoding.Unicode.GetBytes(str);
                cryptData = TransformBytes(source, des.CreateEncryptor(desKey, desIv));
            }

            // 暗号文をそのままUnicode文字列にすると、XML 1.0で使えない文字(制御文字など)が混ざって
            // 保存や読込が失敗することがあるため、Base64にする(復号には鍵・IV・暗号文のバイト列を使うので影響しない)
            return Convert.ToBase64String(cryptData);
        }

        // 復号(鍵付き)
        public String Decode(byte[] desKey, byte[] desIv, byte[] cryptData)
        {
            using (TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider())
            {
                byte[] destination = TransformBytes(cryptData, des.CreateDecryptor(desKey, desIv));
                return Encoding.Unicode.GetString(destination);
            }
        }

        // MemoryStream+CryptoStreamでの変換処理は暗号化/復号どちらの経路でも同じ形をしていたので、
        // ICryptoTransform(Encryptor/Decryptor)を渡すだけの共通処理としてまとめた。
        private static byte[] TransformBytes(byte[] source, ICryptoTransform transform)
        {
            using (transform)
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, transform, CryptoStreamMode.Write))
                {
                    cs.Write(source, 0, source.Length);
                }
                // CryptoStreamを閉じた(=最終ブロックを書き出した)後でも、MemoryStream.ToArrayは使える
                return ms.ToArray();
            }
        }
    }

    // *******************************************************************************
    // FileIO
    public partial class StcFileInputOutput
    {
        public enum EncodingType
        {
            ShiftJis,
            EucJp,
        }

        private readonly StcUtils _utils = new StcUtils();

        // テンポラリファイル作成
        public String CreateTempFile(String ext = "")
        {
            String destStr = Path.GetTempFileName();
            if (ext != String.Empty)
            {
                // 同じ名前の拡張子付きファイルが既にあると、そのファイルを返して呼び出し側に上書きされてしまうため、
                // 空いている名前になるまで作り直す。拡張子はパス全体の置換ではなく末尾だけを変える(フォルダ名に".tmp"があっても壊れない)
                for (int retry = 0; retry < 10; retry++)
                {
                    String srcStr = destStr;
                    destStr = Path.ChangeExtension(srcStr, ext);
                    if (!File.Exists(destStr))
                    {
                        File.Move(srcStr, destStr);
                        break;
                    }
                    File.Delete(srcStr);
                    destStr = Path.GetTempFileName();
                }
            }
            return destStr;
        }

        // ファイル作成
        public void CreateFile(String fileName, String data, Boolean debugMode = false)
        {
            CreateFile(fileName, data, EncodingType.ShiftJis, debugMode);
        }

        // ファイル作成
        public void CreateFile(String fileName, String data, EncodingType encodingType, Boolean debugMode = false)
        {
            // デバッグモードのときは、バッチの画面を閉じない
            if (debugMode)
            {
                data += @"PAUSE" + System.Environment.NewLine;
            }

            // 改行コードを変換
            data = _utils.ChangeNewLineCode(encodingType, data);
            using (StreamWriter sw = new StreamWriter(fileName, false, GetEncoding(encodingType)))
            {
                sw.Write(data);
            }
        }

        // 文字コード変換[UTF8→Sjis]
        // .NET文字列は内部的に常にUTF-16なので、変換は「読み込み時のエンコード指定」と
        // 「書き込み時のエンコード指定(SaveFileがShift_JIS固定)」だけで完了する
        public Boolean ChangeStringCodeUtf2Sjis(String inFileName, String outFileName)
        {
            if (!_utils.IsExistPath(inFileName))
            {
                // ファイルが存在しない(Euc2Sjisと同じく、出力先を空ファイルで上書きしない)
                return false;
            }

            SaveFile(outFileName, LoadFileWithEncoding(inFileName, Encoding.GetEncoding("utf-8")));
            return true;
        }

        // 文字コード変換[Euc→Sjis]
        public Boolean ChangeStringCodeEuc2Sjis(String inFileName, String outFileName)
        {
            if (!_utils.IsExistPath(inFileName))
            {
                // ファイルが存在しない
                return false;
            }

            SaveFile(outFileName, LoadFileWithEncoding(inFileName, Encoding.GetEncoding("EUC-JP")));
            return true;
        }

        // 読み込みファイルを選択
        // InitialDirectoryを指定すると、ダイアログの初期表示フォルダをそこに固定できる
        // (未指定時はWindowsが前回開いたフォルダ等を使う、これまで通りの挙動)
        public String SelectLoadFileName(String fileName = "", String initialDirectory = "")
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = fileName;
            if (initialDirectory != String.Empty)
            {
                ofd.InitialDirectory = initialDirectory;
            }
            ofd.Filter = "XMLファイル(*.xml)|*.xml|すべてのファイル(*.*)|*.*";
            ofd.Title = "読み込む設定ファイルを選択してください";

            String loadFileName = "";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                loadFileName = ofd.FileName;
            }
            return loadFileName;
        }

        // 保存ファイルを選択
        // InitialDirectoryを指定すると、(任意のファイル名を指定する側の)ダイアログの
        // 初期表示フォルダをそこに固定できる
        public String SelectSaveFileName(String fileName, String initialDirectory = "")
        {
            String saveFileName = "";
            if (fileName != String.Empty)
            {
                DialogResult dlgResult = MessageBox.Show(
                    "現在の設定ファイルに保存しますか？" + Environment.NewLine + fileName,
                    "保存ファイル名の選択",
                    MessageBoxButtons.YesNoCancel);
                if (dlgResult == DialogResult.Cancel)
                {
                    return "";
                }
                else if (dlgResult == DialogResult.Yes)
                {
                    // 既存のファイル名を使用。xmlは撲滅していく方針のため、拡張子がxmlなら
                    // jsonへ差し替える(呼び出し元が保存成功後に旧xmlを削除する)
                    String path = (initialDirectory != String.Empty) ? Path.Combine(initialDirectory, fileName) : fileName;
                    saveFileName = path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                        ? Path.ChangeExtension(path, ".json")
                        : path;
                }
            }

            // 任意のファイル名を設定
            if (saveFileName == String.Empty)
            {
                // 任意のファイル名を指定
                SaveFileDialog ofd = new SaveFileDialog();
                // JSON移行中のため、拡張子がxmlならjsonを既定の提案にする(そのままOKでJSON保存できるように)
                ofd.FileName = fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    ? Path.ChangeExtension(fileName, ".json")
                    : fileName;
                if (initialDirectory != String.Empty)
                {
                    ofd.InitialDirectory = initialDirectory;
                }
                ofd.Filter = "JSONファイル(*.json)|*.json|XMLファイル(*.xml)|*.xml|すべてのファイル(*.*)|*.*";
                ofd.Title = "保存する設定ファイルを選択してください";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    saveFileName = ofd.FileName;
                }
            }

            // 保存できるファイルか
            if (saveFileName != String.Empty && !IsSaveValidFilePath(saveFileName))
            {
                RemoveReadonlyAttribute(saveFileName);
            }

            return saveFileName;
        }

        // 保存できるファイルパスか
        public Boolean IsSaveValidFilePath(String filePathName)
        {
            // ファイルが存在していない、または読取専用属性がなければ保存可
            FileInfo fileInfo = new FileInfo(filePathName);
            return !fileInfo.Exists || (fileInfo.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly;
        }

        // 読み取り属性解除
        // 実体はStcUtils側に移した(ExecuteFileSupportReadOnlyがUtilからFileIOを参照する
        // 逆向きの依存になっていたのを解消するため)。StcFileInputOutputは元々utilsフィールド
        // 経由でStcUtilsに依存する側なので、そちらへ委譲する形にして依存の向きを揃えた。
        public Boolean RemoveReadonlyAttribute(String fileName)
        {
            return _utils.RemoveReadonlyAttribute(fileName);
        }

        // 読み取り属性解除
        public void RemoveReadonlyAttribute(FileInfo fi)
        {
            if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                fi.Attributes &= ~FileAttributes.ReadOnly;
            }
        }

        // 読み取り属性解除
        public void RemoveReadonlyAttribute(DirectoryInfo dirInfo)
        {
            //フォルダ属性を変更
            if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }

            //フォルダ内のファイル属性を変更
            foreach (FileInfo fi in dirInfo.GetFiles())
            {
                RemoveReadonlyAttribute(fi);
            }

            //サブフォルダの属性も変更(ジャンクション/シンボリックリンクはリンク先を書き換えないよう辿らない)
            foreach (DirectoryInfo di in dirInfo.GetDirectories())
            {
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
                RemoveReadonlyAttribute(di);
            }
        }

        // フォルダ削除
        public Boolean DeleteDirectoryAndFile(String deletePath)
        {
            Boolean deleteComplete = true;

            if (deletePath.IndexOf("*") != -1)
            {
                // ワイルドカードの指定があった
                deleteComplete = DeleteAnyFile(deletePath);
            }
            else if (Directory.Exists(deletePath))
            {
                DirectoryInfo delDir = new DirectoryInfo(deletePath);
                try
                {
                    RemoveReadonlyAttribute(delDir);
                    delDir.Delete(true);
                }
                catch (Exception)
                {
                    deleteComplete = false;
                }
            }
            else if (File.Exists(deletePath))
            {
                deleteComplete = DeleteFileWithRemoveReadonlyAttribute(deletePath);
            }

            return deleteComplete;
        }

        // ファイル削除
        public Boolean DeleteAnyFile(String targetName)
        {
            String dirName = Path.GetDirectoryName(targetName);
            String fileName = Path.GetFileName(targetName);

            // フォルダ指定の無い相対パス("*.log"など)は、カレントフォルダとして扱う
            if (dirName == String.Empty)
            {
                dirName = Directory.GetCurrentDirectory();
            }

            if (fileName.IndexOf("*") == -1)
            {
                // ワイルドカードの指定が無かった終了
                return true;
            }

            if (!Directory.Exists(dirName))
            {
                // 無効なディレクトリだったら終了
                return true;
            }

            // ファイルリストアップ
            Boolean deleteComplete = true;
            // Directory.GetFilesは拡張子が3文字のパターン("*.xls")だと".xlsx"にも一致する旧仕様があるため、
            // 取得したあとにワイルドカードを正確に照合して絞り込む
            Regex wildcard = new Regex(
                "^" + Regex.Escape(fileName).Replace(Regex.Escape("*"), ".*").Replace(Regex.Escape("?"), ".") + "$",
                RegexOptions.IgnoreCase);
            String[] files = Directory.GetFiles(dirName, fileName)
                .Where(f => wildcard.IsMatch(Path.GetFileName(f))).ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                if (!DeleteFileWithRemoveReadonlyAttribute(files[i]))
                {
                    deleteComplete = false;
                }
            }

            return deleteComplete;
        }

        // ファイル削除（読み取り属性解除）
        public Boolean DeleteFileWithRemoveReadonlyAttribute(String deleteFile)
        {
            try
            {
                RemoveReadonlyAttribute(new FileInfo(deleteFile));
                File.Delete(deleteFile);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ディレクトリを移動
        public void MoveDirectory(String sourcePath, String targetPath, Boolean isSubDirInclude = true)
        {
            if (isSubDirInclude)
            {
                // サブディレクトリも対象
                try
                {
                    Directory.Move(sourcePath, targetPath);
                }
                catch (Exception)
                {
                    MessageBox.Show("Move処理に失敗しました。" + Environment.NewLine +
                        "[" + sourcePath + "]" + Environment.NewLine +
                        "[" + targetPath + "]" + Environment.NewLine);
                }
            }
            else
            {
                // ファイルだけが対象
                MoveFileOnly(sourcePath, targetPath);
            }
        }

        // ファイルだけを移動
        private void MoveFileOnly(String sourcePath, String targetPath)
        {
            Directory.CreateDirectory(targetPath);

            // ファイルだけが対象なので、GetFileSystemEntries+File.Existsで絞り込むのをやめてGetFilesで直接取る
            foreach (String file in Directory.GetFiles(sourcePath))
            {
                String destName = targetPath + @"\" + GetLastPathName(file);
                if (!FileMove(file, destName))
                {
                    MessageBox.Show("Move処理に失敗しました。" + Environment.NewLine +
                        "[" + file + "]" + Environment.NewLine +
                        "[" + destName + "]" + Environment.NewLine);
                }
            }
        }

        // ファイル移動[移動できないことを考慮]
        public Boolean FileMove(String sourcePath, String targetPath)
        {
            try
            {
                File.Move(sourcePath, targetPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ファイルフルパスの中から先頭のパスを取得
        public String GetFirstPathName(String path)
        {
            String[] folders = path.Split('\\');
            return folders[0];
        }

        // ファイルフルパスの中から最後のパスを取得
        public String GetLastPathName(String path)
        {
            String[] folders = path.Split('\\');
            return folders[folders.Length - 1];
        }

        // ディレクトリが無ければ作る(IsAutoCreateがfalseのときは作ってよいか確認する)。
        // 作成しなかった場合だけfalseを返す
        public Boolean EnsureDirectory(String path, Boolean isAutoCreate = false)
        {
            if (Directory.Exists(path))
            {
                return true;
            }

            if (!isAutoCreate)
            {
                String msg = String.Format("ディレクトリは存在しません。作成しますか？\n{0}", path);

                DialogResult result = MessageBox.Show(msg,
                    "Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);

                // 以前は「いいえ」を選んでも作成してしまっていた
                if (result != DialogResult.Yes)
                {
                    return false;
                }
            }

            Directory.CreateDirectory(path);
            return true;
        }

        // データをセーブする
        public void SaveFile(String filePath, String data, Boolean isAppend = false)
        {
            using (StreamWriter sw = new StreamWriter(filePath, isAppend, Encoding.GetEncoding("Shift_JIS")))
            {
                sw.Write(data);
            }
        }

        // ファイルデータを取得する
        public String LoadFile(String filePath)
        {
            return LoadFileWithEncoding(filePath, Encoding.GetEncoding("Shift_JIS"));
        }

        // 指定した文字コードとして読み込む。ファイルが無ければ空文字を返す
        private String LoadFileWithEncoding(String filePath, Encoding srcEncoding)
        {
            if (!File.Exists(filePath))
            {
                return "";
            }

            using (StreamReader sr = new StreamReader(filePath, srcEncoding))
            {
                return sr.ReadToEnd();
            }
        }

        public Boolean DetectFileData(String filePath, String detectWord)
        {
            return LoadFile(filePath).IndexOf(detectWord) != -1;
        }

        private Encoding GetEncoding(EncodingType encodingType)
        {
            // 未知の種別は空文字のままEncoding.GetEncoding("")に渡されて例外になっていたので、既定をShift_JISにする
            String encodingName = "shift_jis";
            if (encodingType == EncodingType.EucJp)
            {
                encodingName = "euc-jp";
            }

            return Encoding.GetEncoding(encodingName);
        }
    }

    // *******************************************************************************
    // キャプチャ
    //
    // 物理的にクラスを分割すると17プロジェクトのcsprojすべてに影響するため、
    // StcUtilsのときと同じく#regionで責務ごとに折りたためるようにするだけに留めた
    // (「機能ごとにバラしておくと汎用的」というTODOへの対応)。
    public class CaptWindow
    {
        #region P/Invoke・列挙型・フィールド定義
        ///////////////////////////////////////////////////////////////////////////////////////////////
        [DllImport("user32.dll")]
        extern static uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        const int MOUSEEVENTF_MOVE = 0x0001;        // 移動
        const int MOUSEEVENTF_LEFTDOWN = 0x0002;    // 左ボタン Down
        const int MOUSEEVENTF_LEFTUP = 0x0004;      // 左ボタン Up
        const int MOUSEEVENTF_ABSOLUTE = 0x8000;    // 絶対値指定

        // モニタの「物理ピクセル」での位置とサイズを取得するために使う。
        // Screen.Boundsはアプリから見た論理サイズ(Windowsの表示倍率で割った値)を返すのに対し、
        // Graphics.CopyFromScreenは物理ピクセル単位でコピーするため、表示倍率が100%以外の
        // モニタでは「論理サイズ分しか撮れない=右端と下端が欠ける」というズレが起きる。
        // 例: 3840x2160を150%表示 → Screen.Boundsは2560x1440を返すが、実際の画面は3840x2160。
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettingsA(String deviceName, int modeNum, ref DEVMODE devMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public String dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY;
            public int dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public String dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
            public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        ///////////////////////////////////////////////////////////////////////////////////////////////


        // 型名に"Type"が付いているのは、同名のプロパティ(CaptureTarget/MouseEvent)と衝突させないため
        public enum CaptureTargetType
        {
            FullScreen,
            CurrentScreen,
            CurrentWindow,
        }

        public enum MouseEventType
        {
            Move,
            LeftClick,
            LeftDown,
            LeftUp,
        }

        // IsStopRequest: Stop()が呼ばれたら true。ExecuteSleep()等がループを途中で
        //   打ち切るために見ている中断フラグ(SetXXXメソッド無しで直接参照される)。
        // IsCaptureCase: SetCaptureCase()で設定する、そもそも今回キャプチャを実行するか
        //   どうかのオン/オフ。CaptureProc()はこれがfalseなら即座に何もせず抜ける。
        public Boolean IsStopRequest { get; set; }
        public Boolean IsCaptureCase { get; set; }

        public String FileFormat { get; set; }
        public int FileIdx { get; set; }
        public CaptureTargetType CaptureTarget { get; set; }

        // SetMousePointでX/Yを個別に書き換えている(構造体のメンバーへの代入)ため、プロパティにせずフィールドのまま
        public Point MousePt;
        public MouseEventType MouseEvent { get; set; } = MouseEventType.LeftClick;
        public Boolean IsMouseMove { get; set; } = true;
        public Boolean IsRestoreMousePos { get; set; } = false;
        public TimeSpan SleepTime { get; set; }    // Sleepする時間
        public TimeSpan SleepCycle { get; set; }   // Sleepを刻む間隔

        // CaptureTargetType.CurrentScreen で「このコントロール(通常は呼び出し元のForm)が
        // 今表示されているモニタ」を判定するために使う。設定されていなければ
        // Screen.PrimaryScreen(メイン画面)にフォールバックする。
        public Control TargetWindow { get; set; }

        private String _errorLog;   // 処理に失敗したファイル名(改行区切り)
        #endregion

        #region 初期化・パラメータ設定
        public CaptWindow()
        {
            Initialize();
        }

        // 初期化
        public void Initialize()
        {
            IsStopRequest = false;
            IsCaptureCase = false;

            _errorLog = "";

            FileFormat = "0";
            FileIdx = 0;
            CaptureTarget = CaptureTargetType.FullScreen;

            MousePt = new Point();
            SleepTime = new TimeSpan();
            SleepCycle = TimeSpan.FromSeconds(1);
        }

        // 処理停止要求
        public void Stop()
        {
            IsStopRequest = true;
        }

        // マウスを移動させるか
        public void SetMouseMove(Boolean isMove)
        {
            IsMouseMove = isMove;
        }

        // マウス移動後にもとの位置へ戻すか
        public void SetRestoreMousePosition(Boolean isRestore)
        {
            IsRestoreMousePos = isRestore;
        }

        // キャプチャ対象
        public void SetCaptureTarget(CaptureTargetType captTarget)
        {
            CaptureTarget = captTarget;
        }

        // Sleep時間設定
        public void SetSleepTimeMsec(String msec)
        {
            if (!msec.Equals(""))
            {
                SleepTime = TimeSpan.FromMilliseconds(uint.Parse(msec));
            }
        }

        // キャプチャ有無設定
        public void SetCaptureCase(Boolean isCapture)
        {
            IsCaptureCase = isCapture;
        }

        // マウスの座標設定
        public Boolean SetMousePoint(String x, String y)
        {
            if (x.Equals("") || y.Equals(""))
            {
                // 座標の指定なし
                MousePt.X = 0;
                MousePt.Y = 0;
                return false;
            }

            MousePt.X = int.Parse(x);
            MousePt.Y = int.Parse(y);
            return true;
        }

        public void SetMouseEvent(MouseEventType mouseEvent)
        {
            MouseEvent = mouseEvent;
        }

        // ファイルフォーマット設定
        public void SetFileFormat(String format)
        {
            FileFormat = format;
        }

        // ファイルIndex設定
        public void SetFileIdx(int idx)
        {
            FileIdx = idx;
        }

        // Sleep
        public void ExecuteSleep()
        {
            for (TimeSpan timer = TimeSpan.FromMilliseconds(0); timer < SleepTime; timer += SleepCycle)
            {
                if (IsStopRequest)
                {
                    break;
                }
                Thread.Sleep(SleepCycle);
            }
        }

        public String GetErrorLog()
        {
            return _errorLog;
        }
        #endregion

        #region 画面キャプチャ

        public void CaptureProc()
        {
            if (IsStopRequest)
            {
                return;
            }

            if (IsCaptureCase == false)
            {
                return;
            }

            String pictFileName = FileFormat + "_" + FileIdx.ToString() + ".png";
            FileIdx++;  // 次使うとき用にインクリ

            if (!CaptureEvent(pictFileName))
            {
                // 処理失敗したファイル名を追記
                _errorLog += pictFileName + Environment.NewLine;
            }
        }

        private Boolean CaptureEvent(String pictFileName)
        {
            switch (CaptureTarget)
            {
                case CaptureTargetType.FullScreen:
                    return SaveWithPrintScreen("^{PRTSC}", pictFileName);
                case CaptureTargetType.CurrentScreen:
                    return SaveWithCaptureCurrentScreen(pictFileName);
                case CaptureTargetType.CurrentWindow:
                    return SaveWithPrintScreen("%{PRTSC}", pictFileName);
                default:
                    return false;
            }
        }

        // PrintScreenでクリップボードへ取り込んでから保存する
        // (Ctrl+PrintScreen="^{PRTSC}"で全画面、Alt+PrintScreen="%{PRTSC}"でアクティブウィンドウ)
        private Boolean SaveWithPrintScreen(String printScreenKey, String pictFileName)
        {
            // 前回の画像が残っていると、PrintScreenの反映が遅れた時にそれを保存してしまう。
            // 先に空にして、画像が入るまで待つ(PrintScreenはどのみちクリップボードを書き換える)
            try
            {
                Clipboard.Clear();
            }
            catch (ExternalException)
            {
                // 他のアプリがクリップボードを掴んでいても、そのまま続ける
            }

            SendKeys.SendWait(printScreenKey);

            return SaveClipboard(pictFileName);
        }

        private Boolean SaveClipboard(String pictFileName)
        {
            try
            {
                IDataObject d = Clipboard.GetDataObject();

                // PrintScreenの内容がクリップボードへ入るまで、最大2秒ほど待つ
                for (int retry = 0; retry < 20 && (d == null || !d.GetDataPresent(DataFormats.Bitmap)); retry++)
                {
                    Thread.Sleep(100);
                    d = Clipboard.GetDataObject();
                }

                //ビットマップデータ形式に関連付けられているデータを取得
                // (取得できなかった場合はnullになり、Saveで例外→falseを返す。usingはnullでも安全)
                using (Image img = (Image)d.GetData(DataFormats.Bitmap))
                {
                    img.Save(pictFileName);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Boolean SaveWithCaptureCurrentScreen(String pictFileName)
        {
            // 「CurrentScreen」という名前なのに、以前は全モニタをまとめた1枚を作っていた
            // (バグ修正前は範囲計算自体も間違っていたが、直しても「全画面結合」という
            // 設計そのものがFullScreen(Ctrl+PrintScreenで撮る画面全体=全モニタ結合と
            // ほぼ同じ結果)と機能が重複していた)。名前の通り「呼び出し元のウィンドウが
            // 今表示されているモニタ1枚だけ」を撮るように直した。
            // SaveClipboard(SendKeys経由の他2種)と同じく、失敗時はtrueを固定で返さず
            // falseを返すようにした(ディスク書き込み失敗・GDI例外等を吸収する)。
            try
            {
                // TargetWindow(UIスレッドのフォーム)のHandleを別スレッドから読むと、デバッガー接続時などに
                // クロススレッド例外になるため、UIスレッドで画面を求める
                Screen targetScreen = Screen.PrimaryScreen;
                if (TargetWindow != null)
                {
                    targetScreen = TargetWindow.InvokeRequired
                        ? (Screen)TargetWindow.Invoke(new Func<Screen>(() => Screen.FromControl(TargetWindow)))
                        : Screen.FromControl(TargetWindow);
                }

                // Screen.Boundsではなく物理ピクセルでの範囲を使う(表示倍率が100%以外のモニタ対策)
                Rectangle captureArea = GetPhysicalBounds(targetScreen);

                using (Bitmap bmp = new Bitmap(captureArea.Width, captureArea.Height))
                {
                    //Graphicsの作成
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        //対象モニタの左上座標からコピーする
                        g.CopyFromScreen(captureArea.Location, new Point(0, 0), bmp.Size);
                    }

                    // ファイル保存
                    bmp.Save(pictFileName);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // モニタの物理ピクセルでの範囲を取得する。
        // Screen.Boundsは表示倍率で割られた論理サイズなので、CopyFromScreen(物理ピクセル単位)と
        // 組み合わせると倍率100%以外のモニタで欠けが出る。EnumDisplaySettingsで実際の
        // 解像度と配置を問い合わせて、そちらを使う。取得に失敗したらScreen.Boundsで代用する。
        private Rectangle GetPhysicalBounds(Screen targetScreen)
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettingsA(targetScreen.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return targetScreen.Bounds;
            }

            if (devMode.dmPelsWidth <= 0 || devMode.dmPelsHeight <= 0)
            {
                return targetScreen.Bounds;
            }

            return new Rectangle(devMode.dmPositionX, devMode.dmPositionY,
                                 devMode.dmPelsWidth, devMode.dmPelsHeight);
        }
        #endregion

        #region マウス操作
        // マウスのイベント処理
        public void MouseProc(String x, String y, MouseEventType mouseEvent)
        {
            // 座標設定
            if (!SetMousePoint(x, y))
            {
                return;
            }
            SetMouseEvent(mouseEvent);
            MouseProc();
        }

        // マウスのイベント処理
        public void MouseProc()
        {
            if (IsStopRequest)
            {
                return;
            }

            Point mousePtOrg = Cursor.Position;

            //マウス移動
            if (IsMouseMove)
            {
                Cursor.Position = MousePt;
            }

            //struct 配列の宣言
            INPUT[] input = new INPUT[2];
            uint inputCount = 0;

            switch(MouseEvent)
            {
                case MouseEventType.LeftClick:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                    input[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
                    inputCount = 2;
                    break;
                case MouseEventType.LeftDown:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                    inputCount = 1;
                    break;
                case MouseEventType.LeftUp:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;
                    inputCount = 1;
                    break;
                default:
                    break;
            }

            //イベントの一括生成
            SendInput(inputCount, input, Marshal.SizeOf(input[0]));

            // マウスの位置をもとに戻す
            if (IsMouseMove && IsRestoreMousePos)
            {
                Cursor.Position = mousePtOrg;
            }
        }
        #endregion
    }

    // *******************************************************************************
    // Exception
    [Serializable()]
    public class StcException : System.Exception
    {
        public StcException() : base() { }
        public StcException(String msg) : base(msg) { }
        public StcException(String msg, System.Exception inner) : base(msg, inner) { }

        protected StcException(System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    // *******************************************************************************
    // Debug
    public class StcDebug
    {
        // 以前はprivateフィールド+Get/Setメソッドの組だったが、C#のプロパティにまとめた
        public Boolean IsDebugMode { get; set; } = false;        // デバッグモードOnOff
        public Boolean UseTimeInFileName { get; set; } = false;  // ファイル名に時間を入れる
        public Boolean UseTimeInLog { get; set; } = false;       // デバッグログに時間を入れる
        public String DebugLogFileName { get; set; } = "DebugLog.txt";

        private const int _fileIndexDigit = 2;
        private int _fileIndex = 1;

        private readonly StcFileInputOutput _fio = new StcFileInputOutput();

        // コンストラクタ
        public StcDebug()
        {
        }

        // コンストラクタ
        public StcDebug(Boolean isMode)
        {
            IsDebugMode = isMode;
        }

        // 共通ファイルにWrite
        public void WriteData(String data, Boolean isAppend = true)
        {
            if (!IsDebugMode)
            {
                return;
            }

            String timePrefix = UseTimeInLog ? DateTime.Now.ToString() + "  : " : "";
            _fio.SaveFile(DebugLogFileName, timePrefix + data + Environment.NewLine, isAppend);
        }

        // 新しいファイルにWrite
        public void WriteDataInNewFile(String data, String filenameSuffixStr = "", String extension = "txt")
        {
            if (!IsDebugMode)
            {
                return;
            }

            String fileName = _fileIndex.ToString().PadLeft(_fileIndexDigit, '0');
            if (UseTimeInFileName)
            {
                fileName += DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            }
            fileName += filenameSuffixStr + "." + extension;

            _fio.SaveFile(fileName, data);

            _fileIndex++;
        }
    }
}
