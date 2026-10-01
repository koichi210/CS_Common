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
        public enum FILE_PATH_TYPE
        {
            WINDOWS_FULLPATH,
            PERFORCE_PATH,
            LINUX_PATH,
            WINDOWS_PATH,
            OTHER
        } ;

        // カレントディレクトリ移動
        public void SetCurrentDirectory()
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public FILE_PATH_TYPE GetFilePathType(String filePath)
        {
            FILE_PATH_TYPE pathType = FILE_PATH_TYPE.WINDOWS_PATH;

            if (filePath.IndexOf(":") != -1)
            {
                pathType = FILE_PATH_TYPE.WINDOWS_FULLPATH;
            }
            else if (filePath.StartsWith("//"))
            {
                pathType = FILE_PATH_TYPE.PERFORCE_PATH;
            }
            else if (filePath.IndexOf("/") != -1)
            {
                pathType = FILE_PATH_TYPE.LINUX_PATH;
            }
            else if (filePath.IndexOf(@"\") != -1)
            {
                pathType = FILE_PATH_TYPE.WINDOWS_PATH;
            }
            else
            {
                pathType = FILE_PATH_TYPE.OTHER;
            }

            return pathType;
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
            Process hProcess = ExecuteProcess(path, arguments, noWindow);
            hProcess.WaitForExit(); // 処理が終わるまで待つ

            //hProcess.CloseMainWindow();
            hProcess.Close();
            hProcess.Dispose();
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
                execPath.IndexOf("http") != -1)
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
                    "Infomation",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (dlgResult == DialogResult.Yes)
                {
                    // 読み取り専用属性を解除する
                    fi.Attributes = FileAttributes.Normal;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 数値・文字列・パス変換
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
            Regex re = new Regex(@"[^0-9]");
            String destNumStr = re.Replace(srcNumStr, "");

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
            int endIdx = srcString.Length;
            if (endIdxString != String.Empty)
            {
                endIdx = Math.Min(endIdx, Convert.ToInt32(endIdxString));
            }

            // Lengthを設定
            int length = endIdx;
            if (lengthString != String.Empty)
            {
                length = Convert.ToInt32(lengthString);
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
        public String ChangeNewLineCodeLF2CRLF(String source)
        {
            // 単純に "\n" を "\r\n" に置換すると、すでに CRLF になっている箇所が
            // "\r\r\n" に増えてしまう。いったん LF に統一してから変換することで、
            // LF と CRLF が混在していても、何度呼んでも結果が変わらないようにする。
            String dest = source.Replace("\r\n", "\n").Replace("\n", "\r\n");
            return dest;
        }

        // 改行コードを変換[CRLF→LF]
        public String ChangeNewLineCodeCRLF2LF(String source)
        {
            String dest = source.Replace("\r\n", "\n");
            return dest;
        }

        // 改行コードを自動変換
        public String ChangeNewLineCode(StcFileInputOutput.ENCODING_TYPE encodingType, String source)
        {
            // 改行コードを変換 "CR+LF" →"LF"
            String dest = ChangeNewLineCodeCRLF2LF(source);

            if (encodingType == StcFileInputOutput.ENCODING_TYPE.SHIFT_JIS)
            {
                // 改行コードを変換 "LF" →"CR+LF"
                dest = ChangeNewLineCodeLF2CRLF(dest);
            }

            return dest;
        }

        // ダブルクォートをエスケープ[" → \"]
        public String ChangeDoubleQuote2BackSlashDoubleQuote(String source)
        {
            String dest = source.Replace(@"""", @"\""");
            return dest;
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
            Bitmap img = new Bitmap(imgFileName);

            Size sz = new Size();
            sz.Width = img.Width;
            sz.Height = img.Height;

            img.Dispose();
            return sz;
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
            String strLinear = "";
            if ( strArray != null )
            {
	            strLinear = String.Join(separator, strArray);
	        }
	        return strLinear;
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
            String targetName = GetSelectName(listBoxCtrl, rootPath);
            SetClipboardText(targetName);
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
                startIdx = removeString.Length + 1;
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
                if (!isReverse)
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
                else
                {
                    valueName = valueName.Substring(startIdx);
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
            return e.KeyChar < '0' || e.KeyChar > '9';
        }

        public void SetDataGridCell(DataGridView dgv, int rowIdx, int columnIdx, String val)
        {
            dgv.Rows[rowIdx].Cells[columnIdx].Value = val;
        }

        public String GetDataGridCell(DataGridView dgv, int rowIdx, int columnIdx)
        {
            String cellData = "";
            if (dgv.Rows[rowIdx].Cells[columnIdx].Value != null)
            {
                cellData = dgv.Rows[rowIdx].Cells[columnIdx].Value.ToString();
            }

            return cellData;
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
        class OriginDB
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

                if (isFullCompare)
                {
                    if (!attribute.Equals(attrValue))
                    {
                        return false;
                    }
                }
                else
                {
                    if (attribute.IndexOf(attrValue) == -1)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        class TextCtrlDB : OriginDB
        {
            public TextBox Ctrl { get; set; }
        }

        class RadioButtonCtrlDB : OriginDB
        {
            public RadioButton Ctrl { get; set; }
        }

        class CheckBoxDB : OriginDB
        {
            public CheckBox Ctrl { get; set; }
        }

        class ComboBoxDB : OriginDB
        {
            public ComboBox Ctrl { get; set; }
        }

        class CheckedListBoxDB : OriginDB
        {
            public CheckedListBox Ctrl { get; set; }
        }

        class HScrollBarDB : OriginDB
        {
            public HScrollBar Ctrl { get; set; }
        }

        class DataGridViewDB : OriginDB
        {
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
            private void GetDataGridCellIdx(out int rowIdx, out int columnIdx, String attribute)
            {
                StcUtils util = new StcUtils();

                int rowStartIdx = attribute.IndexOf("_") + 1;
                int rowEndIdx = attribute.IndexOf("-");
                int columnStartIdx = rowEndIdx + 1;

                rowIdx = (int)util.GetNumber(attribute, rowStartIdx, rowEndIdx - rowStartIdx, "0");

                columnIdx = (int)util.GetNumber(attribute, columnStartIdx, attribute.Length - rowEndIdx - 1, "0");
            }
        }

        class SecureCtrlDB : TextCtrlDB
        {
            public List<byte> DesKey { get; set; }
            public List<byte> DesIV { get; set; }
            public List<byte> CryptData { get; set; }

            public String SecureAttrName { get; set; }
            public String SecureAttrValueDesKey { get; set; }
            public String SecureAttrValueDesIV { get; set; }
            public String SecureAttrValueCryptData { get; set; }
        }

        // private
        private readonly String VersionAttrName = "File";
        private readonly String VersionKeyName = "Version";

        private readonly String DefaultSecureAttrName = "ManageInfo";
        private readonly String DefaultSecureAttrValueDesKey = "Key1_";
        private readonly String DefaultSecureAttrValueDesIV = "Key2_";
        private readonly String DefaultSecureAttrValueCryptData = "Key3_";

        private readonly String VersionElementName = "Param";
        private String ElementName = "Param";

        private TextCtrlDB[] RegTextCtrl = { };
        private RadioButtonCtrlDB[] RegRadioCtrl = { };
        private CheckBoxDB[] RegCheckCtrl = { };
        private ComboBoxDB[] RegComboCtrl = { };
        private DataGridViewDB[] RegDataGridCtrl = { };
        private HScrollBarDB[] RegHScrollBarCtrl = { };
        private ComboBoxDB[] RegComboCtrlList = { };
        private CheckedListBoxDB[] RegCheckedListBox = { };
        private SecureCtrlDB[] RegSecureCtrl = { };

        private XmlDocument m_WriteDocument = null;
        private XmlElement m_WriteRoot = null;

        private StcUtils util = new StcUtils();

        // Element変更
        public void SetElement(String element)
        {
            ElementName = element;
        }

        // コントロール登録
        private static void AddRegistered<TDB>(ref TDB[] registeredCtrl, TDB item, String attrName, String attrValue, String defaultValue, String legacyAttrValue = null) where TDB : OriginDB
        {
            item.SetParams(attrName, attrValue, defaultValue, legacyAttrValue);
            Array.Resize(ref registeredCtrl, registeredCtrl.Length + 1);
            registeredCtrl[registeredCtrl.Length - 1] = item;
        }

        public void RegisterCtrl(String attrName, String attrValue, TextBox ctrl, String defaultValue = "", String legacyAttrValue = null)
        {
            AddRegistered(ref RegTextCtrl, new TextCtrlDB { Ctrl = ctrl }, attrName, attrValue, defaultValue, legacyAttrValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, RadioButton ctrl, String defaultValue = "")
        {
            AddRegistered(ref RegRadioCtrl, new RadioButtonCtrlDB { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, CheckBox ctrl, String defaultValue = "", String legacyAttrValue = null)
        {
            AddRegistered(ref RegCheckCtrl, new CheckBoxDB { Ctrl = ctrl }, attrName, attrValue, defaultValue, legacyAttrValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, ComboBox ctrl, String defaultValue = "")
        {
            AddRegistered(ref RegComboCtrl, new ComboBoxDB { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, String attrCountValue, DataGridView ctrl, String defaultValue = "")
        {
            ctrl.RowCount = 1;
            AddRegistered(ref RegDataGridCtrl, new DataGridViewDB { Ctrl = ctrl, AttrCountValue = attrCountValue }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrl(String attrName, String attrValue, HScrollBar ctrl, int defaultValue = 0)
        {
            AddRegistered(ref RegHScrollBarCtrl, new HScrollBarDB { Ctrl = ctrl }, attrName, attrValue, defaultValue.ToString());
        }

        public void RegisterCtrlList(String attrName, String attrValue, ComboBox ctrl, String defaultValue = "")
        {
            AddRegistered(ref RegComboCtrlList, new ComboBoxDB { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterCtrlList(String attrName, String attrValue, CheckedListBox ctrl, String defaultValue = "")
        {
            AddRegistered(ref RegCheckedListBox, new CheckedListBoxDB { Ctrl = ctrl }, attrName, attrValue, defaultValue);
        }

        public void RegisterSecureCtrl(String attrName, String attrValue, TextBox ctrl, String defaultValue = "")
        {
            AddRegistered(ref RegSecureCtrl, new SecureCtrlDB
            {
                Ctrl = ctrl,
                DesKey = new List<byte>(),
                DesIV = new List<byte>(),
                CryptData = new List<byte>(),
                SecureAttrName = DefaultSecureAttrName,
                SecureAttrValueDesKey = DefaultSecureAttrValueDesKey,
                SecureAttrValueDesIV = DefaultSecureAttrValueDesIV,
                SecureAttrValueCryptData = DefaultSecureAttrValueCryptData,
            }, attrName, attrValue, defaultValue);
        }

        // コントロール初期値設定
        private void ResetCtrlsToDefault()
        {
            // [TextCtrl]
            for (int i = 0; i < RegTextCtrl.Length; i++)
            {
                RegTextCtrl[i].Ctrl.Text = RegTextCtrl[i].DefaultValue;
            }

            // [RadioButton]
            for (int i = 0; i < RegRadioCtrl.Length; i++)
            {
                RegRadioCtrl[i].Ctrl.Checked = util.GetBoolean(RegRadioCtrl[i].DefaultValue);
            }

            // [CheckBox]
            for (int i = 0; i < RegCheckCtrl.Length; i++)
            {
                RegCheckCtrl[i].Ctrl.Checked = util.GetBoolean(RegCheckCtrl[i].DefaultValue);
            }

            // [ComboBox]
            for (int i = 0; i < RegComboCtrl.Length; i++)
            {
                RegComboCtrl[i].Ctrl.Text = RegComboCtrl[i].DefaultValue;
            }

            // [ComboBox]リスト
            for (int i = 0; i < RegComboCtrlList.Length; i++)
            {
                RegComboCtrlList[i].Ctrl.Items.Clear();
            }

            // [CheckedListBoxList]
            for (int i = 0; i < RegCheckedListBox.Length; i++)
            {
                RegCheckedListBox[i].Ctrl.Items.Clear();
            }

            // [SecureCtrl]
            for (int i = 0; i < RegSecureCtrl.Length; i++)
            {
                RegSecureCtrl[i].DesKey.Clear();
                RegSecureCtrl[i].DesIV.Clear();
                RegSecureCtrl[i].CryptData.Clear();
                RegSecureCtrl[i].Ctrl.Text = RegSecureCtrl[i].DefaultValue;
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
                foreach (XmlElement element in document.DocumentElement)
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
            foreach (XmlElement element in document.DocumentElement)
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
        private Boolean LoadRegisteredCtrl<T>(T[] registeredCtrl, Func<T, Boolean> isMatch, Action<T> apply) where T : OriginDB
        {
            for (int i = 0; i < registeredCtrl.Length; i++)
            {
                if (isMatch(registeredCtrl[i]))
                {
                    apply(registeredCtrl[i]);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[TextBox]
        private Boolean LoadTextCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(RegTextCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Text = elementValue);
        }

        // 設定ファイル読み込み[RadioButton]
        private Boolean LoadRadioCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(RegRadioCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Checked = util.GetBoolean(elementValue));
        }

        // 設定ファイル読み込み[CheckBox]
        private Boolean LoadCheckCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(RegCheckCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Checked = util.GetBoolean(elementValue));
        }

        // 設定ファイル読み込み[ComboBox]
        private Boolean LoadComboCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(RegComboCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Text = elementValue);
        }

        // 設定ファイル読み込み[ComboBoxの履歴一覧]
        private Boolean LoadComboCtrlList(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(RegComboCtrlList,
                ctrl => ctrl.IsExistPartMatch(element),
                ctrl => ctrl.Ctrl.Items.Add(elementValue));
        }

        // 設定ファイル読み込み[CheckedListBox]
        private Boolean LoadCheckedListBoxCtrl(XmlElement element, String elementValue)
        {
            // 属性値は "<AttrValue>-<項目名>" の形で入っているので、前半で照合して後半を項目名として使う
            return LoadRegisteredCtrl(RegCheckedListBox,
                ctrl => ctrl.IsExistPartMatch(element, ctrl.AttrName, ctrl.AttrValue + "-"),
                ctrl =>
                {
                    String attribute = element.GetAttribute(ctrl.AttrName);
                    int itemNameIdx = ctrl.AttrValue.Length + 1;

                    // 名前
                    ctrl.Ctrl.Items.Add(attribute.Substring(itemNameIdx));

                    // 状態
                    Boolean isChecked = util.GetBoolean(elementValue, "Checked");
                    ctrl.Ctrl.SetItemChecked(ctrl.Ctrl.Items.Count - 1, isChecked);
                });
        }

        // 設定ファイル読み込み[DataGridView]
        private Boolean LoadDataGridCtrl(XmlElement element, String elementValue)
        {
            // ここだけは照合が 2 通り（セルの値と行数）あるので、まとめずに残してある
            for (int i = 0; i < RegDataGridCtrl.Length; i++)
            {
                if (RegDataGridCtrl[i].IsExistPartMatch(element))
                {
                    String attribute = element.GetAttribute(RegDataGridCtrl[i].AttrName);
                    RegDataGridCtrl[i].LoadData(attribute, elementValue);
                    return true;
                }
                else if (RegDataGridCtrl[i].IsExistPartMatch(element, RegDataGridCtrl[i].AttrName, RegDataGridCtrl[i].AttrCountValue))
                {
                    RegDataGridCtrl[i].Ctrl.RowCount = util.GetInteger(elementValue);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[HScrollBar]
        private Boolean LoadHScrollBarCtrl(XmlElement element, String elementValue)
        {
            return LoadRegisteredCtrl(RegHScrollBarCtrl,
                ctrl => ctrl.IsExistFullMatch(element),
                ctrl => ctrl.Ctrl.Value = int.Parse(elementValue));
        }

        // 設定ファイル読み込み[SecureCtrl]
        private Boolean LoadSecureCtrl(XmlElement element, String elementValue)
        {
            for (int i = 0; i < RegSecureCtrl.Length; i++)
            {
                if (RegSecureCtrl[i].IsExistFullMatch(element))
                {
                    if (!IsExistSecureCode(RegSecureCtrl[i]))
                    {
                        return false;
                    }

                    RegSecureCtrl[i].Ctrl.Text = GetDecodeString(RegSecureCtrl[i]);
                    return true;
                }
            }
            return false;
        }

        private String GetDecodeString(SecureCtrlDB secureCtrl)
        {
            StcSecure secure = new StcSecure();

            return secure.Decode(
                secureCtrl.DesKey.ToArray(),
                secureCtrl.DesIV.ToArray(),
                secureCtrl.CryptData.ToArray());
        }

        private Boolean IsExistSecureCode(SecureCtrlDB secure)
        {
            if (secure.DesKey.Count <= 0)
            {
                return false;
            }

            if (secure.DesIV.Count <= 0)
            {
                return false;
            }

            if (secure.CryptData.Count <= 0)
            {
                return false;
            }

            return true;
        }

        private Boolean UseSecureCtrl()
        {
            // 管理情報がなければ読まない
            return RegSecureCtrl.Length != 0;
        }

        private void LoadSecureCode(XmlDocument document)
        {
            foreach (XmlElement element in document.DocumentElement)
            {
                String elementValue = element.InnerText;
                for (int i = 0; i < RegSecureCtrl.Length; i++)
                {
                    if (RegSecureCtrl[i].IsExistPartMatch(element, RegSecureCtrl[i].SecureAttrName, DefaultSecureAttrValueDesKey))
                    {
                        RegSecureCtrl[i].DesKey.Add(Convert.ToByte(elementValue));
                        continue;
                    }

                    if (RegSecureCtrl[i].IsExistPartMatch(element, RegSecureCtrl[i].SecureAttrName, DefaultSecureAttrValueDesIV))
                    {
                        RegSecureCtrl[i].DesIV.Add(Convert.ToByte(elementValue));
                        continue;
                    }

                    if (RegSecureCtrl[i].IsExistPartMatch(element, RegSecureCtrl[i].SecureAttrName, DefaultSecureAttrValueCryptData))
                    {
                        RegSecureCtrl[i].CryptData.Add(Convert.ToByte(elementValue));
                        continue;
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
            foreach (XmlElement element in FindElements(document, VersionAttrName, VersionKeyName, true))
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
                AtomicFile.Write(fileName, m_WriteDocument.Save);
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
            m_WriteDocument = document;

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
            XmlDeclaration declaration = m_WriteDocument.CreateXmlDeclaration("1.0", null, null);  // XML宣言
            m_WriteRoot = m_WriteDocument.CreateElement("root");  // ルート要素

            m_WriteDocument.AppendChild(declaration);
            m_WriteDocument.AppendChild(m_WriteRoot);
        }

        // 設定ファイルの保存[バージョン]
        private void SaveXmlVersion(String version)
        {
            SaveXmlString(VersionElementName, VersionAttrName, VersionKeyName, version);
        }

        // パラメータ保存 /////////////////////////////////////////
        // ファイル保存[String]
        public void SaveXmlString(String elementName, String attrName, String attrValue, String text)
        {
            XmlElement element = m_WriteDocument.CreateElement(elementName);
            element.SetAttribute(attrName, attrValue);
            element.InnerText = text;
            m_WriteRoot.AppendChild(element);
        }

        // ファイル保存[String]
        public void SaveXmlString(String attrName, String attrValue, String text)
        {
            SaveXmlString(ElementName, attrName, attrValue, text);
        }

        // ファイル保存[StringArray]
        public void SaveXmlParamAll(String attrName, String attrValue, String[] strArray)
        {
            if(strArray == null)
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
        private void SaveRegisteredCtrl<T>(T[] registeredCtrl, Func<T, String> getValue) where T : OriginDB
        {
            for (int i = 0; i < registeredCtrl.Length; i++)
            {
                SaveXmlString(registeredCtrl[i].AttrName, registeredCtrl[i].AttrValue, getValue(registeredCtrl[i]));
            }
        }

        // ファイル保存[TextBox]
        private void SaveTextCtrl()
        {
            SaveRegisteredCtrl(RegTextCtrl, ctrl => ctrl.Ctrl.Text);
        }

        // ファイル保存[RadioButton]
        private void SaveRadioCtrl()
        {
            SaveRegisteredCtrl(RegRadioCtrl, ctrl => ctrl.Ctrl.Checked.ToString());
        }

        // ファイル保存[CheckBox]
        private void SaveCheckCtrl()
        {
            SaveRegisteredCtrl(RegCheckCtrl, ctrl => ctrl.Ctrl.Checked.ToString());
        }

        // ファイル保存[ComboBox]
        private void SaveComboCtrl()
        {
            SaveRegisteredCtrl(RegComboCtrl, ctrl => ctrl.Ctrl.Text);
        }

        // ファイル保存[ComboBox]の中身
        private void SaveComboCtrlList()
        {
            for (int i = 0; i < RegComboCtrlList.Length; i++)
            {
                for (int j = 0; j < RegComboCtrlList[i].Ctrl.Items.Count; j++)
                {
                    SaveXmlString(RegComboCtrlList[i].AttrName,
                        RegComboCtrlList[i].AttrValue + j.ToString(),
                        RegComboCtrlList[i].Ctrl.Items[j].ToString());
                }
            }
        }

        // ファイル保存[CheckListBox]
        private void SaveCheckedListBox()
        {
            for (int i = 0; i < RegCheckedListBox.Length; i++)
            {
                for (int j = 0; j < RegCheckedListBox[i].Ctrl.Items.Count; j++)
                {
                    // 状態
                    SaveXmlString(RegCheckedListBox[i].AttrName,
                        RegCheckedListBox[i].AttrValue + "-" + RegCheckedListBox[i].Ctrl.Items[j].ToString(),
                        RegCheckedListBox[i].Ctrl.GetItemCheckState(j).ToString());
                }
            }
        }

        // ファイル保存[dataGrid]
        private void SaveDataGridCtrl()
        {
            for (int i = 0; i < RegDataGridCtrl.Length; i++)
            {
                SaveXmlParam(RegDataGridCtrl[i].AttrName, RegDataGridCtrl[i].AttrCountValue, RegDataGridCtrl[i].Ctrl.RowCount);
                for (int rowIdx = 0; rowIdx < RegDataGridCtrl[i].Ctrl.RowCount; rowIdx++)
                {
                    for (int columnIdx = 0; columnIdx < RegDataGridCtrl[i].Ctrl.ColumnCount; columnIdx++)
                    {
                        String cellValue = util.GetDataGridCell(RegDataGridCtrl[i].Ctrl, rowIdx, columnIdx);

                        SaveXmlString(RegDataGridCtrl[i].AttrName,
                            RegDataGridCtrl[i].AttrValue + "_" + rowIdx.ToString() + "-" + columnIdx.ToString(),
                            cellValue);
                    }
                }
            }
        }

        // ファイル保存[HScrollBar]
        private void SaveHScrollBarCtrl()
        {
            SaveRegisteredCtrl(RegHScrollBarCtrl, ctrl => ctrl.Ctrl.Value.ToString());
        }

        // ファイル保存[暗号化キー]
        private void SaveSecureCtrl()
        {
            StcSecure secure = new StcSecure();

            byte[] desKey;
            byte[] desIV;
            byte[] cryptData;
            String secureWord;
            for (int i = 0; i < RegSecureCtrl.Length; i++)
            {
                secureWord = secure.Encode(RegSecureCtrl[i].Ctrl.Text, out desKey, out desIV, out cryptData);
                SaveXmlString(RegSecureCtrl[i].AttrName, RegSecureCtrl[i].AttrValue, secureWord);

                SaveXmlManageParam(DefaultSecureAttrName, DefaultSecureAttrValueDesKey, desKey);
                SaveXmlManageParam(DefaultSecureAttrName, DefaultSecureAttrValueDesIV, desIV);
                SaveXmlManageParam(DefaultSecureAttrName, DefaultSecureAttrValueCryptData, cryptData);
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

            for (int i = 0; i < RegTextCtrl.Length; i++) { profile.Values[GenericKey(RegTextCtrl[i])] = RegTextCtrl[i].Ctrl.Text; }
            for (int i = 0; i < RegRadioCtrl.Length; i++) { profile.Values[GenericKey(RegRadioCtrl[i])] = RegRadioCtrl[i].Ctrl.Checked.ToString(); }
            for (int i = 0; i < RegCheckCtrl.Length; i++) { profile.Values[GenericKey(RegCheckCtrl[i])] = RegCheckCtrl[i].Ctrl.Checked.ToString(); }
            for (int i = 0; i < RegComboCtrl.Length; i++) { profile.Values[GenericKey(RegComboCtrl[i])] = RegComboCtrl[i].Ctrl.Text; }
            for (int i = 0; i < RegHScrollBarCtrl.Length; i++) { profile.Values[GenericKey(RegHScrollBarCtrl[i])] = RegHScrollBarCtrl[i].Ctrl.Value.ToString(); }

            for (int i = 0; i < RegComboCtrlList.Length; i++)
            {
                List<String> items = new List<String>();
                foreach (Object item in RegComboCtrlList[i].Ctrl.Items) { items.Add(item.ToString()); }
                profile.Lists[GenericKey(RegComboCtrlList[i])] = items;
            }

            for (int i = 0; i < RegCheckedListBox.Length; i++)
            {
                List<String> items = new List<String>();
                Dictionary<String, Boolean> checkedStates = new Dictionary<String, Boolean>();
                for (int j = 0; j < RegCheckedListBox[i].Ctrl.Items.Count; j++)
                {
                    String itemName = RegCheckedListBox[i].Ctrl.Items[j].ToString();
                    items.Add(itemName);
                    checkedStates[itemName] = RegCheckedListBox[i].Ctrl.GetItemChecked(j);
                }
                profile.Lists[GenericKey(RegCheckedListBox[i])] = items;
                profile.CheckedStates[GenericKey(RegCheckedListBox[i])] = checkedStates;
            }

            for (int i = 0; i < RegDataGridCtrl.Length; i++)
            {
                List<List<String>> rows = new List<List<String>>();
                for (int r = 0; r < RegDataGridCtrl[i].Ctrl.RowCount; r++)
                {
                    List<String> row = new List<String>();
                    for (int c = 0; c < RegDataGridCtrl[i].Ctrl.ColumnCount; c++)
                    {
                        row.Add(util.GetDataGridCell(RegDataGridCtrl[i].Ctrl, r, c));
                    }
                    rows.Add(row);
                }
                profile.Grids[GenericKey(RegDataGridCtrl[i])] = rows;
            }

            // SecureCtrl(暗号化して保存するパスワード等)は現状どのプロジェクトも未使用のため、
            // JSON汎用プロファイルでは対応していない(必要になったら追加する)
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
            for (int i = 0; i < RegTextCtrl.Length; i++) { if (TryGetGenericValue(profile, RegTextCtrl[i], out value)) { RegTextCtrl[i].Ctrl.Text = value; } }
            for (int i = 0; i < RegRadioCtrl.Length; i++) { if (TryGetGenericValue(profile, RegRadioCtrl[i], out value)) { RegRadioCtrl[i].Ctrl.Checked = util.GetBoolean(value); } }
            for (int i = 0; i < RegCheckCtrl.Length; i++) { if (TryGetGenericValue(profile, RegCheckCtrl[i], out value)) { RegCheckCtrl[i].Ctrl.Checked = util.GetBoolean(value); } }
            for (int i = 0; i < RegComboCtrl.Length; i++) { if (TryGetGenericValue(profile, RegComboCtrl[i], out value)) { RegComboCtrl[i].Ctrl.Text = value; } }
            for (int i = 0; i < RegHScrollBarCtrl.Length; i++) { if (TryGetGenericValue(profile, RegHScrollBarCtrl[i], out value)) { RegHScrollBarCtrl[i].Ctrl.Value = util.GetInteger(value); } }

            for (int i = 0; i < RegComboCtrlList.Length; i++)
            {
                RegComboCtrlList[i].Ctrl.Items.Clear();
                List<String> items;
                if (profile.Lists.TryGetValue(GenericKey(RegComboCtrlList[i]), out items))
                {
                    foreach (String item in items) { RegComboCtrlList[i].Ctrl.Items.Add(item); }
                }
            }

            for (int i = 0; i < RegCheckedListBox.Length; i++)
            {
                RegCheckedListBox[i].Ctrl.Items.Clear();
                List<String> items;
                if (profile.Lists.TryGetValue(GenericKey(RegCheckedListBox[i]), out items))
                {
                    Dictionary<String, Boolean> checkedStates;
                    profile.CheckedStates.TryGetValue(GenericKey(RegCheckedListBox[i]), out checkedStates);

                    foreach (String item in items)
                    {
                        int idx = RegCheckedListBox[i].Ctrl.Items.Add(item);
                        Boolean isChecked;
                        if (checkedStates == null || !checkedStates.TryGetValue(item, out isChecked))
                        {
                            isChecked = false;
                        }
                        RegCheckedListBox[i].Ctrl.SetItemChecked(idx, isChecked);
                    }
                }
            }

            for (int i = 0; i < RegDataGridCtrl.Length; i++)
            {
                RegDataGridCtrl[i].Ctrl.RowCount = 1;
                List<List<String>> rows;
                if (profile.Grids.TryGetValue(GenericKey(RegDataGridCtrl[i]), out rows) && rows.Count > 0)
                {
                    RegDataGridCtrl[i].Ctrl.RowCount = rows.Count;
                    for (int r = 0; r < rows.Count; r++)
                    {
                        for (int c = 0; c < rows[r].Count && c < RegDataGridCtrl[i].Ctrl.ColumnCount; c++)
                        {
                            RegDataGridCtrl[i].Ctrl.Rows[r].Cells[c].Value = rows[r][c];
                        }
                    }
                }
            }
        }

        // GenericProfileのキー(コントロール名由来なので、旧XMLの「Cell_行-列」方式より読める)
        private static String GenericKey(OriginDB ctrl)
        {
            return ctrl.AttrName + "|" + ctrl.AttrValue;
        }

        // GenericKeyで見つからなければ、LegacyAttrValue(旧キー)でも探す。
        // typo修正等でAttrValueを変えたコントロールでも、旧キーで保存されたJSONを読めるようにするため
        private static Boolean TryGetGenericValue(GenericProfile profile, OriginDB ctrl, out String value)
        {
            if (profile.Values.TryGetValue(GenericKey(ctrl), out value))
            {
                return true;
            }

            if (ctrl.LegacyAttrValue != null)
            {
                return profile.Values.TryGetValue(ctrl.AttrName + "|" + ctrl.LegacyAttrValue, out value);
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
    }

    // *******************************************************************************
    // Form1コンストラクタの定型処理(アイコン設定/カレントディレクトリ移動)をまとめた
    // 共通基底クラス。各プロジェクトのForm1は
    //   partial class Form1 : StcBaseForm<SaveRestore>
    // のように継承し、util/srフィールドはこちら側の物をそのまま使う。
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
        protected StcUtils util = new StcUtils();
        protected TSaveRestore sr = new TSaveRestore();

        protected void InitializeCommonSettings(Icon formIcon)
        {
            this.Icon = formIcon;
            util.SetCurrentDirectory();
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
        public String Encode(String str, out byte[] desKey, out byte[] desIV, out byte[] cryptData)
        {
            // 以前は鍵生成用と暗号化用でTripleDESCryptoServiceProviderを2つ作っていたが、
            // CreateEncryptorには鍵とIVを明示的に渡しているので1つで足りる
            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();
            desKey = des.Key;
            desIV = des.IV;

            byte[] source = Encoding.Unicode.GetBytes(str);

            cryptData = TransformBytes(source, des.CreateEncryptor(desKey, desIV));

            return Encoding.Unicode.GetString(cryptData);
        }

        // 復号(鍵付き)
        public String Decode(byte[] desKey, byte[] desIV, byte[] cryptData)
        {
            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();
            byte[] destination = TransformBytes(cryptData, des.CreateDecryptor(desKey, desIV));

            return Encoding.Unicode.GetString(destination);
        }

        // MemoryStream+CryptoStreamでの変換処理は暗号化/復号どちらの経路でも同じ形をしていたので、
        // ICryptoTransform(Encryptor/Decryptor)を渡すだけの共通処理としてまとめた。
        private static byte[] TransformBytes(byte[] source, ICryptoTransform transform)
        {
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, transform, CryptoStreamMode.Write);

            cs.Write(source, 0, source.Length);
            cs.Close();

            byte[] result = ms.ToArray();
            ms.Close();

            return result;
        }
    }

    // *******************************************************************************
    // FileIO
    public partial class StcFileInputOutput
    {
        public enum ENCODING_TYPE
        {
            SHIFT_JIS,
            EUC_JP,
        };

        StcUtils utils = new StcUtils();

        // テンポラリファイル作成
        public String CreateTempFile(String ext = "")
        {
            String destStr = Path.GetTempFileName();
            if (ext != String.Empty)
            {
                String srcStr = destStr;
                destStr = destStr.Replace(".tmp", "." + ext);
                if (!File.Exists(destStr))
                {
                    File.Move(srcStr, destStr);
                }
            }
            return destStr;
        }

        // ファイル作成
        public void CreateFile(String fileName, String data, Boolean debugMode = false)
        {
            CreateFile(fileName, data, StcFileInputOutput.ENCODING_TYPE.SHIFT_JIS, debugMode);
        }

        // ファイル作成
        public void CreateFile(String fileName, String data, ENCODING_TYPE encodingType, Boolean debugMode = false)
        {
            // デバッグモードのときは、バッチの画面を閉じない
            if (debugMode)
            {
                data += @"PAUSE" + System.Environment.NewLine;
            }

            // 改行コードを変換
            data = utils.ChangeNewLineCode(encodingType, data);
            using (StreamWriter sw = new StreamWriter(fileName, false, GetEncoding(encodingType)))
            {
                sw.Write(data);
            }
        }

        // 文字コード変換[UTF8→Sjis]
        // .NET文字列は内部的に常にUTF-16なので、変換は「読み込み時のエンコード指定」と
        // 「書き込み時のエンコード指定(SaveFileがShift_JIS固定)」だけで完了する
        public Boolean ChangeStringCodeUTF2SJIS(String inFileName, String outFileName)
        {
            SaveFile(outFileName, LoadFileWithEncoding(inFileName, Encoding.GetEncoding("utf-8")));
            return true;
        }

        // 文字コード変換[Euc→Sjis]
        public Boolean ChangeStringCodeEUC2SJIS(String inFileName, String outFileName)
        {
            if (!utils.IsExistPath(inFileName))
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
            FileInfo cFileInfo = new FileInfo(filePathName);
            if (!cFileInfo.Exists)
            {
                // ファイルが存在していなければ、保存可
                return true;
            }

            if ((cFileInfo.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
            {
                // 読取専用属性がなければ、保存可
                return true;
            }

            return false;
        }

        // 読み取り属性解除
        // 実体はStcUtils側に移した(ExecuteFileSupportReadOnlyがUtilからFileIOを参照する
        // 逆向きの依存になっていたのを解消するため)。StcFileInputOutputは元々utilsフィールド
        // 経由でStcUtilsに依存する側なので、そちらへ委譲する形にして依存の向きを揃えた。
        public Boolean RemoveReadonlyAttribute(String fileName)
        {
            return utils.RemoveReadonlyAttribute(fileName);
        }

        // 読み取り属性解除
        public void RemoveReadonlyAttribute(FileInfo fi)
        {
            if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                fi.Attributes = FileAttributes.Normal;
            }
        }

        // 読み取り属性解除
        public void RemoveReadonlyAttribute(DirectoryInfo dirInfo)
        {
            //フォルダ属性を変更
            if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                dirInfo.Attributes = FileAttributes.Normal;
            }

            //フォルダ内のファイル属性を変更
            foreach (FileInfo fi in dirInfo.GetFiles())
            {
                RemoveReadonlyAttribute(fi);
            }

            //サブフォルダの属性も変更
            foreach (DirectoryInfo di in dirInfo.GetDirectories())
            {
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
            String[] files = Directory.GetFiles(dirName, fileName);
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
            Boolean deleteComplete = true;

            RemoveReadonlyAttribute(new FileInfo(deleteFile));

            try
            {
                File.Delete(deleteFile);
            }
            catch (Exception)
            {
                deleteComplete = false;
            }

            return deleteComplete;
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

            String[] files = Directory.GetFileSystemEntries(sourcePath);

            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i]))
                {
                    String destName = targetPath + @"\" + GetLastPathName(files[i]);
                    if ( ! FileMove(files[i], destName) )
                    {
                        MessageBox.Show("Move処理に失敗しました。" + Environment.NewLine +
                            "[" + files[i] + "]" + Environment.NewLine +
                            "[" + destName + "]" + Environment.NewLine);
                    }
                }
            }
        }

        // ファイル移動[移動できないことを考慮]
        public Boolean FileMove(String sourcePath, String targetPath)
        {
            Boolean isSuccess = true;
            try
            {
                File.Move(sourcePath, targetPath);
            }
            catch
            {
                isSuccess = false;
            }
            return isSuccess;
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
            Boolean isExist = true;
            if (!Directory.Exists(path))
            {
                Boolean isCreate = true;
                if (!isAutoCreate)
                {
                    String msg = String.Format("ディレクトリは存在しません。作成しますか？\n{0}", path);

                    DialogResult result = MessageBox.Show(msg,
                        "Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button1);

                    // 「いいえ」を選んでも作成してしまっていた(IsCreateがtrueのまま)
                    isCreate = (result == DialogResult.Yes);
                }

                if (isCreate)
                {
                    Directory.CreateDirectory(path);
                }
                else
                {
                    isExist = false;
                }
            }

            return isExist;
        }

        // データをセーブする
        public void SaveFile(String filePath, String data, Boolean isAppend = false)
        {
            using (StreamWriter sw = new StreamWriter(filePath, isAppend, System.Text.Encoding.GetEncoding("Shift_JIS")))
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
            Boolean isFound = false;
            String data = LoadFile(filePath);

            if (data.IndexOf(detectWord) != -1)
            {
                isFound = true;
            }

            return isFound;
        }

        private Encoding GetEncoding(ENCODING_TYPE encodingType)
        {
            // 未知の種別は空文字のままEncoding.GetEncoding("")に渡されて例外になっていたので、既定をShift_JISにする
            String encodingName = "shift_jis";
            if (encodingType == ENCODING_TYPE.EUC_JP)
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


        public enum CAPTURE_TARGET
        {
            FULL_SCREEN,
            CURRENT_SCREEN,
            CURRENT_WINDOW,
        };

        public enum MOUSE_EVENT
        {
            MOVE,
            LEFT_CLICK,
            LEFT_DOWN,
            LEFT_UP,
        };

        // IsStopRequest: Stop()が呼ばれたら true。ExecuteSleep()等がループを途中で
        //   打ち切るために見ている中断フラグ(SetXXXメソッド無しで直接参照される)。
        // IsCaptureCase: SetCaptureCase()で設定する、そもそも今回キャプチャを実行するか
        //   どうかのオン/オフ。CaptureProc()はこれがfalseなら即座に何もせず抜ける。
        public Boolean IsStopRequest { get; set; }
        public Boolean IsCaptureCase { get; set; }

        public String FileFormat { get; set; }
        public int FileIdx { get; set; }
        public CAPTURE_TARGET CaptureTarget { get; set; }

        // SetMousePointでX/Yを個別に書き換えている(構造体のメンバーへの代入)ため、プロパティにせずフィールドのまま
        public Point MousePt;
        public MOUSE_EVENT MouseEvent { get; set; } = MOUSE_EVENT.LEFT_CLICK;
        public Boolean IsMouseMove { get; set; } = true;
        public Boolean IsRestoreMousePos { get; set; } = false;
        public TimeSpan SleepTime { get; set; }    // Sleepする時間
        public TimeSpan SleepCycle { get; set; }   // Sleepを刻む間隔

        // CAPTURE_TARGET.CURRENT_SCREEN で「このコントロール(通常は呼び出し元のForm)が
        // 今表示されているモニタ」を判定するために使う。設定されていなければ
        // Screen.PrimaryScreen(メイン画面)にフォールバックする。
        public Control TargetWindow { get; set; }

        private String ErrorLog;
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

            ErrorLog = "";

            FileFormat = "0";
            FileIdx = 0;
            CaptureTarget = CAPTURE_TARGET.FULL_SCREEN;

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
        public void SetCaptureTarget(CAPTURE_TARGET captTarget)
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
            Boolean isSetPoint = false;
            if (x.Equals("") || y.Equals(""))
            {
                // 座標の指定なし
                MousePt.X = 0;
                MousePt.Y = 0;
            }
            else
            {
                MousePt.X = int.Parse(x);
                MousePt.Y = int.Parse(y);
                isSetPoint = true;
            }

            return isSetPoint;
        }

        public void SetMouseEvent(MOUSE_EVENT mouseEvent)
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
                System.Threading.Thread.Sleep(SleepCycle);
            }
        }

        public String GetErrorLog()
        {
            return ErrorLog;
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
                ErrorLog += pictFileName + Environment.NewLine;
            }
        }

        private Boolean CaptureEvent(String pictFileName)
        {
            Boolean isSuccess = false;
            switch (CaptureTarget)
            {
                case CAPTURE_TARGET.FULL_SCREEN:
                    isSuccess = SaveWithPrintScreen("^{PRTSC}", pictFileName);
                    break;
                case CAPTURE_TARGET.CURRENT_SCREEN:
                    isSuccess = SaveWithCaptureCurrentScreen(pictFileName);
                    break;
                case CAPTURE_TARGET.CURRENT_WINDOW:
                    isSuccess = SaveWithPrintScreen("%{PRTSC}", pictFileName);
                    break;
                default:
                    break;
            }

            return isSuccess;
        }

        // PrintScreenでクリップボードへ取り込んでから保存する
        // (Ctrl+PrintScreen="^{PRTSC}"で全画面、Alt+PrintScreen="%{PRTSC}"でアクティブウィンドウ)
        private Boolean SaveWithPrintScreen(String printScreenKey, String pictFileName)
        {
            SendKeys.SendWait(printScreenKey);

            return SaveClipboard(pictFileName);
        }

        private Boolean SaveClipboard(String pictFileName)
        {
            Boolean isSuccess = true;
            Image img = null;
            try
            {
                IDataObject d = Clipboard.GetDataObject();

                //ビットマップデータ形式に関連付けられているデータを取得
                img = (Image)d.GetData(DataFormats.Bitmap);
                img.Save(pictFileName);
            }
            catch (Exception)
            {
                isSuccess = false;
            }
            finally
            {
                // クリップボードから画像を取得できなかった場合はnullのままなので、そのままDisposeすると例外になる
                if (img != null)
                {
                    img.Dispose();
                }
            }

            return isSuccess;
        }

        private Boolean SaveWithCaptureCurrentScreen(String pictFileName)
        {
            // 「CURRENT_SCREEN」という名前なのに、以前は全モニタをまとめた1枚を作っていた
            // (バグ修正前は範囲計算自体も間違っていたが、直しても「全画面結合」という
            // 設計そのものがFULL_SCREEN(Ctrl+PrintScreenで撮る画面全体=全モニタ結合と
            // ほぼ同じ結果)と機能が重複していた)。名前の通り「呼び出し元のウィンドウが
            // 今表示されているモニタ1枚だけ」を撮るように直した。
            // SaveClipboard(SendKeys経由の他2種)と同じく、失敗時はtrueを固定で返さず
            // falseを返すようにした(ディスク書き込み失敗・GDI例外等を吸収する)。
            Boolean isSuccess = true;
            try
            {
                Screen targetScreen = (TargetWindow != null) ? Screen.FromControl(TargetWindow) : Screen.PrimaryScreen;

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
            }
            catch (Exception)
            {
                isSuccess = false;
            }

            return isSuccess;
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
        public void MouseProc(String x, String y, MOUSE_EVENT mouseEvent)
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
                case MOUSE_EVENT.LEFT_CLICK:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                    input[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
                    inputCount = 2;
                    break;
                case MOUSE_EVENT.LEFT_DOWN:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                    inputCount = 1;
                    break;
                case MOUSE_EVENT.LEFT_UP:
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

        private int FileIndex = 1;
        private int FileIndexDigit = 2;

        private StcFileInputOutput fio = new StcFileInputOutput();

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
            if (IsDebugMode == true)
            {
                String writeData = "";
                if (UseTimeInLog)
                {
                    writeData = DateTime.Now.ToString() + "  : ";
                }
                writeData += data + Environment.NewLine;
                fio.SaveFile(DebugLogFileName, writeData, isAppend);
            }
        }

        // 新しいファイルにWrite
        public void WriteDataInNewFile(String data, String filenameSuffixStr = "", String extension = "txt")
        {
            if (!IsDebugMode)
            {
                return;
            }

            String fileName = FileIndex.ToString().PadLeft(FileIndexDigit, '0');
            if (UseTimeInFileName)
            {
                fileName += System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            }
            fileName += filenameSuffixStr + "." + extension;

            fio.SaveFile(fileName, data);

            FileIndex++;
        }
    }
}
