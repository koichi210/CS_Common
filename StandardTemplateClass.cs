using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text;
using System.Linq;
using System.IO;
using System.Xml;
using System.Security.Cryptography;
using System.Net;
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

        public FILE_PATH_TYPE GetFilePathType(String FilePath)
        {
            FILE_PATH_TYPE PathType = FILE_PATH_TYPE.WINDOWS_PATH;

            if (FilePath.IndexOf(":") != -1)
            {
                PathType = FILE_PATH_TYPE.WINDOWS_FULLPATH;
            }
            else if (FilePath.StartsWith("//"))
            {
                PathType = FILE_PATH_TYPE.PERFORCE_PATH;
            }
            else if (FilePath.IndexOf("/") != -1)
            {
                PathType = FILE_PATH_TYPE.LINUX_PATH;
            }
            else if (FilePath.IndexOf(@"\") != -1)
            {
                PathType = FILE_PATH_TYPE.WINDOWS_PATH;
            }
            else
            {
                PathType = FILE_PATH_TYPE.OTHER;
            }

            return PathType;
        }

        #endregion

        #region プロセス実行
        // プロセス -----------------------------------------
        // プロセス実行
        public Process ExecuteProcess(String ExecPath, Boolean NoWindow)
        {
            String Param = "";
            return ExecuteProcess(ExecPath, Param, NoWindow);
        }

        public Process ExecuteProcess(String ExecPath, String Param = "", Boolean NoWindow = false)
        {
            ProcessStartInfo psInfo = new ProcessStartInfo();
            psInfo.FileName = ExecPath;
            psInfo.Arguments = Param;

            if (NoWindow == true)
            {
                //psInfo.CreateNoWindow = true;     // ウィンドウを開かない
                psInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            return Process.Start(psInfo);
        }

        // プロセス実行&完了待ち
        public void ExecutePathWithWait(String Path, String Param = "", Boolean NoWindow = false)
        {
            Process hProcess = ExecuteProcess(Path, Param, NoWindow);
            hProcess.WaitForExit(); // 処理が終わるまで待つ

            //hProcess.CloseMainWindow();
            hProcess.Close();
            hProcess.Dispose();
        }

        // プロセス実行&標準出力取得
        public Process ExecuteProcess(out String Output, String ExecPath, String Param = "")
        {
            ProcessStartInfo psInfo = new ProcessStartInfo();
            psInfo.FileName = ExecPath;
            psInfo.Arguments = Param;
            psInfo.CreateNoWindow = true;
            psInfo.UseShellExecute = false;
            psInfo.RedirectStandardOutput = true; // 標準出力をリダイレクト

            Process p = Process.Start(psInfo);
            Output = p.StandardOutput.ReadToEnd(); // 標準出力を取得
            return p;
        }

        #endregion

        #region 存在チェック・ファイル実行
        // Explorer系 -----------------------------------------
        // ファイルパスが存在するかチェック[環境変数のPathを考慮]
        public Boolean IsExistFileNameInEnvironment(String FileName = "")
        {
            // フルパスで指定されている
            if (File.Exists(FileName))
            {
                return true;
            }

            // 環境変数の[PATH]を取得
            String Paths = System.Environment.GetEnvironmentVariable("Path");

            String[] PathArray = Paths.Split(';');
            foreach (String PathName in PathArray)
            {
                if (PathName == String.Empty)
                {
                    continue;
                }

                String FullPathName = PathName.TrimEnd('\\') + '\\';
                if (File.Exists(FullPathName + FileName))
                {
                    // 環境変数のパスで登録された場所に存在する
                    return true;
                }
            }
            return false;
        }

        // パスが有効かチェック
        public Boolean IsExistPath(String FilePath)
        {
            if (IsExistDirectory(FilePath))
            {
                return true;
            }

            if (IsExistFile(FilePath))
            {
                return true;
            }

            return false;
        }

        // 存在するかを見るだけ(作成はしない)。IsExistPath内部からのみ使う
        private Boolean IsExistDirectory(String DirectoryPath, Boolean IsNoticeExceptMsg = false, String ExceptMsgStr = "")
        {
            if (Directory.Exists(DirectoryPath))
            {
                return true;
            }

            if (IsNoticeExceptMsg)
            {
                String Msg = "";
                if (ExceptMsgStr != String.Empty)
                {
                    Msg += ExceptMsgStr + Environment.NewLine;
                }
                Msg += "フォルダが存在しません。" + Environment.NewLine;
                Msg += "[" + DirectoryPath + "]";
                MessageBox.Show(Msg);
            }
            return false;
        }

        public Boolean IsExistFile(String FilePath, Boolean IsNoticeExceptMsg = false, String ExceptMsgStr = "")
        {
            if (File.Exists(FilePath))
            {
                return true;
            }

            if (IsExistFileNameInEnvironment(FilePath))
            {
                return true;
            }

            if (IsNoticeExceptMsg)
            {
                String Msg = "";
                if (ExceptMsgStr != String.Empty)
                {
                    Msg += ExceptMsgStr + Environment.NewLine;
                }
                Msg += "ファイルが存在しません。" + Environment.NewLine;
                Msg += "[" + FilePath + "]";
                MessageBox.Show(Msg);
            }
            return false;
        }

        // ファイルパス実行
        public Boolean ExecutePath(String ExecPath, Boolean NoWindow = false)
        {
            return ExecutePath(ExecPath, "", NoWindow);
        }

        // ファイルパス実行
        public Boolean ExecutePath(String ExecPath, String Param, Boolean NoWindow = false)
        {
            Boolean IsSuccess = true;
            if (File.Exists(ExecPath) ||
                Directory.Exists(ExecPath) ||
                IsExistFileNameInEnvironment(ExecPath) ||
                ExecPath.IndexOf("http") != -1)
            {
                ExecuteProcess(ExecPath, Param, NoWindow);
                IsSuccess = true;
            }
            else
            {
                MessageBox.Show(
                    "指定されたパスが存在しません。" + ExecPath,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                IsSuccess = false;
            }
            return IsSuccess;
        }

        // ファイルパス実行
        public Boolean ExecutePath(String ExecPath, KeyEventArgs e)
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

            return ExecutePath(ExecPath);
        }

        // 読み取り属性解除。以前はStcFileInputOutputをここでnewして使っており、
        // UtilからFileIOへ依存する逆向きの参照になっていた(TODOコメントで指摘されていた)。
        // 実体をこちらへ移し、StcFileInputOutput.RemoveReadonlyAttribute(String)からは
        // このメソッドへ委譲する形にして、依存の向き(StcFileInputOutput→StcUtils)を揃えた。
        public Boolean RemoveReadonlyAttribute(String FileName)
        {
            FileInfo fi = new FileInfo(FileName);
            if (!fi.Exists)
            {
                // ファイルが無い
                MessageBox.Show("指定されたパスが存在しません。");
                return false;
            }

            if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                DialogResult DlgResult = MessageBox.Show(
                    "読み取り専用属性を解除しますか？" + Environment.NewLine + FileName,
                    "Infomation",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (DlgResult == DialogResult.Yes)
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
        public int GetInteger(String Text)
        {
            int Parameter = 0;
            if (!Text.Equals(String.Empty))
            {
                Parameter = int.Parse(Text);
            }
            return Parameter;
        }

        public Boolean GetBoolean(String Text, String DetectWord = "True")
        {
            Boolean Parameter = false;
            if (!Text.Equals(String.Empty))
            {
                if (Text.Equals(DetectWord))
                {
                    Parameter = true;
                }
            }
            return Parameter;
        }

        public long GetNumber(String SrcString, int DefaultDigitNum = 1)
        {
            long DigitNum = DefaultDigitNum;

            if (SrcString != String.Empty)
            {
                DigitNum = long.Parse(SrcString);
            }

            return DigitNum;
        }

        // 文字列から数値を取得
        public long GetNumber(String SrcString, int StartIdx, int Length, String DefaultString = "01")
        {
            int digit = DefaultString.Length;  // 桁数

            // StartIdxを設定
            if (SrcString.Length < StartIdx)
            {
                // 範囲外だったら、終端を設定
                StartIdx = SrcString.Length;
            }

            // Lengthを設定
            if (Length == 0 ||
                 (SrcString.Length < StartIdx + Length))
            {
                // 長さが0 or 範囲外だったら、ギリギリ範囲内に設定
                Length = SrcString.Length - StartIdx;
            }

            String SrcNumStr = SrcString.Substring(StartIdx, Length);
            Regex re = new Regex(@"[^0-9]");
            String DestNumStr = re.Replace(SrcNumStr, "");

            if (DestNumStr == String.Empty)
            {
                DestNumStr = DefaultString;
            }
            else
            {
                // 所望の桁数まで"0"埋めする
                DestNumStr = DestNumStr.PadLeft(digit, '0');
            }
            return GetNumber(DestNumStr);
        }

        // 文字列から数値を取得
        public long GetNumberFromRear(String SrcString, int EndIdx, int Length, String DefaultString = "01")
        {
            int StartIdx = SrcString.Length - EndIdx;
            return GetNumber(SrcString, StartIdx, Length, DefaultString);
        }

        public long GetNumberFromRear(String SrcString, String EndIdxString, String LengthString, String DefaultString = "01")
        {
            // StartIdxを設定
            int EndIdx = SrcString.Length;
            if (EndIdxString != String.Empty)
            {
                EndIdx = Math.Min(EndIdx, Convert.ToInt32(EndIdxString));
            }

            // Lengthを設定
            int Length = EndIdx;
            if (LengthString != String.Empty)
            {
                Length = Convert.ToInt32(LengthString);
            }

            return GetNumberFromRear(SrcString, EndIdx, Length, DefaultString);
        }

        // パスがWindows仕様かチェック
        public Boolean IsWindowsPath(String Path)
        {
            Boolean IsWindows = false;

            // "\"が見つかったらWindowsPathなので、置換対象と判断
            if (Path.IndexOf(@"\") != -1)
            {
                IsWindows = true;
            }

            return IsWindows;
        }

        // ファイルパスの仕様を変更 [C:\ → /cygdrive/c]
        public String ChangeWindowsPath2CygwinPath(String OldPath)
        {
            String NewPath = OldPath;
            if (IsWindowsPath(NewPath))
            {
                if (NewPath.IndexOf(@":") != -1)
                {
                    NewPath = NewPath.Replace(@":", @"");
                    NewPath = "/cygdrive/" + NewPath;
                }
            }

            return ChangeWindowsPath2LinuxPath(NewPath);
        }

        // ドライブレターを変更 [/cygdrive/c → C:\]
        public String ChangeCygwinPath2WindowsPath(String OldPath)
        {
            String NewPath = OldPath;
            if (!IsWindowsPath(NewPath))
            {
                if (NewPath.IndexOf(@"/cygdrive/") != -1)
                {
                    NewPath = NewPath.Replace(@"/cygdrive/", @"");

                    // ドライブレター1文字の直後にコロンを入れる。
                    // String.Insert は元の文字列を書き換えず新しい文字列を返すので、必ず受け取る。
                    if (NewPath.Length >= 1)
                    {
                        NewPath = NewPath.Insert(1, @":");
                    }

                    // ChangeWindowsPath2CygwinPath と対になるよう、区切りも Windows 形式へ戻す
                    NewPath = ChangeLinuxPath2WindowsPath(NewPath);
                }
            }

            return NewPath;
        }

        // パス区切りを変換[\ → /]
        public String ChangeWindowsPath2LinuxPath(String OldPath)
        {
            return OldPath.Replace(@"\", @"/");
        }

        // パス区切りを変換[\ → /]
        public String[] ChangeWindowsPath2LinuxPath(String[] OldPathArray)
        {
            return OldPathArray.Select(str => ChangeWindowsPath2LinuxPath(str)).ToArray();
        }

        // パス区切りを変換[/ → \]
        public String ChangeLinuxPath2WindowsPath(String OldPath)
        {
            return OldPath.Replace(@"/", @"\");
        }

        // パス区切りを変換[/ → \]
        public String[] ChangeLinuxPath2WindowsPath(String[] OldPathArray)
        {
            return OldPathArray.Select(str => ChangeLinuxPath2WindowsPath(str)).ToArray();
        }

        // 改行コードを変換[LF→CRLF]
        public String ChangeNewLineCodeLF2CRLF(String Source)
        {
            // 単純に "\n" を "\r\n" に置換すると、すでに CRLF になっている箇所が
            // "\r\r\n" に増えてしまう。いったん LF に統一してから変換することで、
            // LF と CRLF が混在していても、何度呼んでも結果が変わらないようにする。
            String Dest = Source.Replace("\r\n", "\n").Replace("\n", "\r\n");
            return Dest;
        }

        // 改行コードを変換[CRLF→LF]
        public String ChangeNewLineCodeCRLF2LF(String Source)
        {
            String Dest = Source.Replace("\r\n", "\n");
            return Dest;
        }

        // 改行コードを自動変換
        public String ChangeNewLineCode(StcFileInputOutput.ENCORD_TYPE EncordType, String Source)
        {
            // 改行コードを変換 "CR+LF" →"LF"
            String Dest = ChangeNewLineCodeCRLF2LF(Source);

            if (EncordType == StcFileInputOutput.ENCORD_TYPE.SHIFT_JIS)
            {
                // 改行コードを変換 "LF" →"CR+LF"
                Dest = ChangeNewLineCodeLF2CRLF(Dest);
            }

            return Dest;
        }

        // ダブルクォートをエスケープ[" → \"]
        public String ChangeDoubleQuote2BackSlashDoubleQuote(String Source)
        {
            String Dest = Source.Replace(@"""", @"\""");
            return Dest;
        }

        // Linuxパス名を連結
        public String AppendLinuxPathName(String Path1, String Path2)
        {
            // 以前は Substring(Path1.Length, 0) と Substring(0, 0) で判定していたが、
            // どちらも常に空文字を返すため条件が必ず成立し、区切りを足し続けていた。
            Boolean IsEndWithSlash = Path1.EndsWith("/");
            Boolean IsStartWithSlash = Path2.StartsWith("/");

            // 両方にあるなら片方を落とす
            if (IsEndWithSlash && IsStartWithSlash)
            {
                return Path1 + Path2.Substring(1);
            }

            // 両方に無いなら足す
            if (!IsEndWithSlash && !IsStartWithSlash)
            {
                return Path1 + "/" + Path2;
            }

            // どちらか一方にあるなら、そのまま繋ぐ
            return Path1 + Path2;
        }

        // 並びをアソート
        public String[] AssortList(String[] Sources)
        {
            return Sources.OrderBy(i => Guid.NewGuid()).ToArray();
        }

        // 特定の文字列を削除
        public String[] RemoveStringArray(String[] Sources, String Remove)
        {
            // String.Replace は第1引数が空文字だと ArgumentException を投げる。
            // 「何も削除しない」指定とみなして、そのまま返す。
            if (Remove == String.Empty)
            {
                return Sources.ToArray();
            }

            return Sources.Select(str => str.Replace(Remove, "")).ToArray();
        }

        //配列→リスト
        public List<String> Array2List(String[] StringArray)
        {
            List<string> StringList = new List<string>();
            StringList.AddRange(StringArray);
            return StringList;
        }

        //リスト→配列
        public String[] List2Array(List<String> StringList)
        {
            return StringList.ToArray();
        }

        // 画像サイズ取得
        public Size GetPictSize(String ImgFileName)
        {
            Bitmap Img = new Bitmap(ImgFileName);

            Size sz = new Size();
            sz.Width = Img.Width;
            sz.Height = Img.Height;

            Img.Dispose();
            return sz;
        }

        #endregion

        #region WinFormsコントロール操作(ComboBox/ListBox/ListView/DataGridView/D&D)
        //----------------------------------------------------------
        // コントロール操作 ------------------------------------------
        // テキストコントロールの中身を全て選択
        public void SelectAll(TextBox TextBoxCtrl, KeyEventArgs e)
        {
            // 全選択
            if (e.KeyCode == Keys.A && e.Control == true)
            {
                TextBoxCtrl.SelectAll();
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

        public String ChangeStrArray2Linear(String[] StrArray, String Suffix)
        {
            String StrLinear = "";
            if ( StrArray != null )
            {
	            StrLinear = String.Join(Suffix, StrArray);
	        }
	        return StrLinear;
        }

        public String[] ChangeStrLinear2Array(String StrLinear, String Delimiter, StringSplitOptions Opt = StringSplitOptions.RemoveEmptyEntries)
        {
            return StrLinear.Split(new[] { Delimiter }, Opt);
        }

        public String TrimDuplication(String Source, String Delimiter)
        {
            String[] SourceArray = Source.Split(new[] { Delimiter }, StringSplitOptions.RemoveEmptyEntries);
            String[] DestArray = TrimDuplication(SourceArray);
            return ChangeStrArray2Linear(DestArray, Delimiter);
        }

        // 重複を削除
        public String[] TrimDuplication(String[] SourceArray)
        {
            // ArrayList.Containsは毎回先頭から線形探索するため、要素数が多いとO(n^2)で遅くなる。
            // HashSetなら追加済みかの判定がO(1)になるため、順序を保ったままO(n)で重複除去できる。
            HashSet<String> seen = new HashSet<String>();
            List<String> result = new List<String>(SourceArray.Length);

            //基になる配列の要素を列挙する
            foreach (String i in SourceArray)
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

        public String[] GetStringArray(ComboBox CbCtrl)
        {
            String[] CombBoxArray = CbCtrl.Items.Cast<String>().ToArray();
            return CombBoxArray;
        }

        public String TrimEndGarbage(String Source)
        {
            char[] TrimChar = { '\\', '/', '\r', '\n' };
            return Source.TrimEnd(TrimChar);
        }

        // 以下のようにフォルダパスを整形
        //   TopDirectory・・・C:\
        //   SubDirectory・・・C:\test\sample
        public String AdjustDirectoryName(String SrcDirName)
        {
            // 終端の\を削除。
            String DestDirName = SrcDirName.TrimEnd('\\');

            // フルパス指定かチェック
            int FileNameidx = SrcDirName.IndexOf(":");
            if (0 <= FileNameidx)
            {
                // (例) C: のようにドライブレターだけが指定された場合は、終端に"\"が必要。
                // 以前は @":\" を足していたため "C::\" とコロンが二重になっていた。
                if (DestDirName.Length == 2)
                {
                    DestDirName += @"\";
                }
            }

            return DestDirName;
        }

        public String GetSelectName(ListBox ListBoxCtrl, String RootPath = "")
        {
            StringBuilder TargetName = new StringBuilder();
            for (int i = 0; i < ListBoxCtrl.SelectedItems.Count; i++)
            {
                if (RootPath != String.Empty)
                {
                    TargetName.Append(RootPath).Append(@"\");
                }
                TargetName.Append(ListBoxCtrl.SelectedItems[i].ToString()).Append(Environment.NewLine);
            }

            return TargetName.ToString();
        }

        public String GetSelectListName(ListView ListViewCtrl, String RootPath = "", int index = 0)
        {
            StringBuilder TargetName = new StringBuilder();
            for (int i = 0; i < ListViewCtrl.SelectedItems.Count; i++)
            {
                if (RootPath != String.Empty)
                {
                    TargetName.Append(RootPath).Append(@"\");
                }
                TargetName.Append(ListViewCtrl.SelectedItems[i].SubItems[index].Text).Append(Environment.NewLine);
            }

            return TargetName.ToString();
        }

        // リストボックスの選択項目をコピー
        public void CopyToClipboard(KeyEventArgs e, ListBox ListBoxCtrl, String RootPath = "")
        {
            // ガード節にして、以降のネストを浅くした(挙動は変えていない)
            if (e.KeyCode != Keys.C || e.Control != true)
            {
                return;
            }

            // コピー
            String TargetName = GetSelectName(ListBoxCtrl, RootPath);
            Clipboard.SetText(TargetName);
        }

        // リストコントロールの選択項目をコピー
        public void CopyToClipboard(KeyEventArgs e, ListView ListViewCtrl, String RootPath = "", int index = 0)
        {
            // ガード節にして、以降のネストを浅くした(挙動は変えていない)
            if (e.KeyCode != Keys.C || e.Control != true)
            {
                return;
            }

            // コピー
            String TargetName = GetSelectListName(ListViewCtrl, RootPath, index);
            if (!TargetName.Equals(String.Empty))
            {
                Clipboard.SetText(TargetName);
            }
        }

        // リストコントロールの中身をString配列で取得
        public String[] GetStrArrayFromListBox(ListBox.SelectedObjectCollection ListBoxSelected)
        {
            // 以前は改行区切りの文字列に連結してから配列へ分割し直す遠回りな実装だった。
            // 素直にLINQで直接配列へ変換する。
            return ListBoxSelected.Cast<object>().Select(Item => Item.ToString()).ToArray();
        }

        // プロファイルをコンボボックスにリストアップ
        public void UpdateProfileList(ref ComboBox ComboCtrl, String DefaultProfileName = "", String DirectoryPath = "", String FileExtension = "*.xml")
        {
            if (DirectoryPath == String.Empty)
            {
                DirectoryPath = Directory.GetCurrentDirectory();
            }
            else
            {
                if (Directory.Exists(DirectoryPath) == false)
                {
                    // 指定されたディレクトリが存在しない
                    return;
                }
            }

            // ファイルをリストアップ(アクセス権の無いサブフォルダが1つでもあると例外になるため、その場合は一覧を更新しない)
            String[] files;
            try
            {
                files = Directory.GetFiles(DirectoryPath, FileExtension, SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return;
            }

            SetComboBoxFromArray(ComboCtrl, files, DirectoryPath);
            SetComboBoxText(ComboCtrl, DefaultProfileName);
        }

        // 文字配列をコンボボックスにセット
        public void SetComboBoxFromArray(ComboBox ComboCtrl, String[] Array, String RemoveString = "", String LimitString = "")
        {
            int StartIdx = 0;
            if (RemoveString != String.Empty)
            {
                StartIdx = RemoveString.Length + 1;
            }
            SetComboBoxFromArraySubString(ComboCtrl, Array, StartIdx, "", LimitString);
        }

        // 文字配列（SubString）をコンボボックスにセット
        public void SetComboBoxFromArraySubString(ComboBox ComboCtrl, String[] Array, int StartIdx, String EndDelimiter = "", String LimitString = "", Boolean IsReverse = false)
        {
            ComboCtrl.Items.Clear();
            for (int i = 0; i < Array.Length; i++)
            {
                String ValueName = Array[i];

                // カラ文字
                if (ValueName == String.Empty)
                {
                    continue;
                }

                // 文字列生成
                int Length;
                if (!IsReverse)
                {
                    Length = ValueName.IndexOf(EndDelimiter);
                }
                else
                {
                    Length = ValueName.LastIndexOf(EndDelimiter);
                }

                if (Length >= 0 && Length > StartIdx)
                {
                    ValueName = ValueName.Substring(StartIdx, Length - StartIdx);
                }
                else
                {
                    ValueName = ValueName.Substring(StartIdx);
                }

                // 文字の絞り込み
                if (LimitString != String.Empty)
                {
                    // 大文字小文字を区別せずに部分一致で検索
                    if (ValueName.IndexOf(ComboCtrl.Text,StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                // 登録済みだったらスキップ
                if (IsRegisteredCombBox(ComboCtrl, ValueName))
                {
                    continue;
                }

                ComboCtrl.Items.Add(ValueName);
            }
        }

        // ComboBoxに登録済みかチェック
        private Boolean IsRegisteredCombBox(ComboBox ComboCtrl, String RegisterText)
        {
            Boolean IsRegistered = false;
            for (int i = 0; i < ComboCtrl.Items.Count; i++)
            {
                if (RegisterText == ComboCtrl.Items[i].ToString())
                {
                    // すでに登録済みの文字だった
                    IsRegistered = true;
                    break;
                }
            }
            return IsRegistered;
        }

        // 文字列をコンボボックスに設定
        public void SetComboBoxText(ComboBox ComboCtrl, String DefaultProfileName)
        {
            String ProfileName = "";

            for (int i = 0; i < ComboCtrl.Items.Count; i++)
            {
                if (ComboCtrl.Items[i].ToString() == DefaultProfileName)
                {
                    ProfileName = DefaultProfileName;
                    break;
                }
            }

            if (ProfileName == String.Empty && 0 < ComboCtrl.Items.Count)
            {
                ProfileName = ComboCtrl.Items[0].ToString();
            }

            ComboCtrl.Text = ProfileName;
        }

        // コンボボックスの中から目的の文字列を探す
        public String FindStringFromComboBox(ComboBox CmbCtrl, String SrcName, String TrimName = "", Boolean IsReverse = false)
        {
            String SerchName = SrcName;
            String DestName = "";

            // SrcTrimNameが設定されていたら、特定の文字列で区切る
            if (TrimName != String.Empty)
            {
                int FileNameidx;
                if (!IsReverse)
                {
                    FileNameidx = SrcName.IndexOf(TrimName);
                }
                else
                {
                    FileNameidx = SrcName.LastIndexOf(TrimName);
                }

                if (0 <= FileNameidx)
                {
                    SerchName = SrcName.Substring(0, FileNameidx);
                }
            }

            if (SerchName.Length != 0)
            {
                for (int i = 0; i < CmbCtrl.Items.Count; i++)
                {
                    String ComboString = CmbCtrl.Items[i].ToString();
                    if (ComboString.IndexOf(SerchName) != -1)
                    {
                        DestName = ComboString;
                        break;
                    }
                }
            }
            return DestName;
        }

        // ComboBoxのTextをプルダウンに追加
        public void ModifyCombBoxList(ComboBox ComboCtrl)
        {
            if (ComboCtrl.Text == String.Empty)
            {
                return;
            }

            // Text文字をプルダウンに追加
            ComboCtrl.Items.Add(ComboCtrl.Text);

            // 重複は削除する
            String[] CombBoxArray = { "" };
            CombBoxArray = GetStringArray(ComboCtrl);
            CombBoxArray = TrimDuplication(CombBoxArray);

            // プルダウンを更新
            ComboCtrl.Items.Clear();
            ComboCtrl.Items.AddRange(CombBoxArray);
            //ComboCtrl.DataSource = CombBoxArray;
        }

        // 数字以外のキーならtrue(KeyPressEventArgs.Handledにそのまま入れて入力を弾く用途)
        public Boolean IsNotNumberKey(KeyPressEventArgs e)
        {
            return e.KeyChar < '0' || e.KeyChar > '9';
        }

        public void SetDataGridCell(DataGridView dgv, int RowCount, int ColumnCount, String Val)
        {
            dgv.Rows[RowCount].Cells[ColumnCount].Value = Val;
        }

        public String GetDataGridCell(DataGridView dgv, int RowCount, int ColumnCount)
        {
            String CellData = "";
            if (dgv.Rows[RowCount].Cells[ColumnCount].Value != null)
            {
                CellData = dgv.Rows[RowCount].Cells[ColumnCount].Value.ToString();
            }

            return CellData;
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
            String[] DropList = { "" };
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropList = (String[])e.Data.GetData(DataFormats.FileDrop, false);
            }
            return DropList;
        }

        public String GetDropListLinear(DragEventArgs e)
        {
            String[] DropList = GetDropListArray(e);
            return ChangeStrArray2Linear(DropList, Environment.NewLine);
        }
        #endregion
    }

    // *******************************************************************************
    // プロファイルの保存＆読み込み
    // 書き込み途中で落ちても元のファイルが壊れないよう、一時ファイルに書き切ってから置き換える
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

    class StcSaveRestore
    {
        class OriginDB
        {
            public String AttrName { get; set; }
            public String AttrValue { get; set; }
            public String ElementValue { get; set; }

            public void SetDefaultParam(String attr_name, String attr_value, String element_value)
            {
                AttrName = attr_name;
                AttrValue = attr_value;
                ElementValue = element_value;
            }

            public Boolean IsExistFullMatch(XmlElement element, String attr_name = "", String attr_value = "")
            {
                return IsExistParam(element, attr_name, attr_value, true);
            }

            public Boolean IsExistPartMatch(XmlElement element, String attr_name = "", String attr_value = "")
            {
                return IsExistParam(element, attr_name, attr_value, false);
            }

            public Boolean IsExistParam(XmlElement element, String attr_name, String attr_value, Boolean IsFullCompare)
            {
                if (attr_name == String.Empty)
                {
                    attr_name = AttrName;
                }

                if (attr_value == String.Empty)
                {
                    attr_value = AttrValue;
                }

                String attribute = element.GetAttribute(attr_name);
                if (attribute == String.Empty)
                {
                    return false;
                }

                if (IsFullCompare)
                {
                    if (!attribute.Equals(attr_value))
                    {
                        return false;
                    }
                }
                else
                {
                    if (attribute.IndexOf(attr_value) == -1)
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
            public void LoadData(String attribute, String ElementValue)
            {
                int RowIdx;
                int ColumnIdx;
                GetDataGridCellIdx(out RowIdx, out ColumnIdx, attribute);

                Ctrl.Rows[RowIdx].Cells[ColumnIdx].Value = ElementValue;
            }

            /// <summary>
            /// attributeから、CellIdxを取得
            /// </summary>
            /// <param name="RowIdx"></param>
            /// <param name="ColumnIdx"></param>
            /// <param name="Attribute"></param>
            private void GetDataGridCellIdx(out int RowIdx, out int ColumnIdx, String Attribute)
            {
                StcUtils util = new StcUtils();

                int RowStartIdx = Attribute.IndexOf("_") + 1;
                int RowEndIdx = Attribute.IndexOf("-");
                int ColmunStartIdx = RowEndIdx + 1;

                RowIdx = (int)util.GetNumber(Attribute, RowStartIdx, RowEndIdx - RowStartIdx, "0");

                ColumnIdx = (int)util.GetNumber(Attribute, ColmunStartIdx, Attribute.Length - RowEndIdx - 1, "0");
            }
        }

        class SecureCtrlDB : TextCtrlDB
        {
            public List<byte> DesKey = null;
            public List<byte> DesIV = null;
            public List<byte> cryptData = null;

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
        private static void AddRegistered<TDB>(ref TDB[] RegisteredCtrl, TDB Item, String AttrName, String AttrValue, String ElementValue) where TDB : OriginDB
        {
            Item.SetDefaultParam(AttrName, AttrValue, ElementValue);
            Array.Resize(ref RegisteredCtrl, RegisteredCtrl.Length + 1);
            RegisteredCtrl[RegisteredCtrl.Length - 1] = Item;
        }

        public void RegistCtrl(String AttrName, String AttrValue, TextBox Ctrl, String ElementValue = "")
        {
            AddRegistered(ref RegTextCtrl, new TextCtrlDB { Ctrl = Ctrl }, AttrName, AttrValue, ElementValue);
        }

        public void RegistCtrl(String AttrName, String AttrValue, RadioButton Ctrl, String ElementValue = "")
        {
            AddRegistered(ref RegRadioCtrl, new RadioButtonCtrlDB { Ctrl = Ctrl }, AttrName, AttrValue, ElementValue);
        }

        public void RegistCtrl(String AttrName, String AttrValue, CheckBox Ctrl, String ElementValue = "")
        {
            AddRegistered(ref RegCheckCtrl, new CheckBoxDB { Ctrl = Ctrl }, AttrName, AttrValue, ElementValue);
        }

        public void RegistCtrl(String AttrName, String AttrValue, ComboBox Ctrl, String ElementValue = "")
        {
            AddRegistered(ref RegComboCtrl, new ComboBoxDB { Ctrl = Ctrl }, AttrName, AttrValue, ElementValue);
        }

        public void RegistCtrl(String AttrName, String AttrValue, String AttrCountValue, DataGridView Ctrl, String ElementValue = "")
        {
            Ctrl.RowCount = 1;
            AddRegistered(ref RegDataGridCtrl, new DataGridViewDB { Ctrl = Ctrl, AttrCountValue = AttrCountValue }, AttrName, AttrValue, ElementValue);
        }

        public void RegistCtrl(String AttrName, String AttrValue, HScrollBar Ctrl, int ElementValue = 0)
        {
            AddRegistered(ref RegHScrollBarCtrl, new HScrollBarDB { Ctrl = Ctrl }, AttrName, AttrValue, ElementValue.ToString());
        }

        public void RegistCtrlList(String AttrName, String AttrValue, ComboBox Ctrl, String ElementValue = "")
        {
            AddRegistered(ref RegComboCtrlList, new ComboBoxDB { Ctrl = Ctrl }, AttrName, AttrValue, ElementValue);
        }

        public void RegistCtrlList(String AttrName, String AttrValue, CheckedListBox Ctrl, String ElementValue = "")
        {
            AddRegistered(ref RegCheckedListBox, new CheckedListBoxDB { Ctrl = Ctrl }, AttrName, AttrValue, ElementValue);
        }

        public void RegistSecureCtrl(String AttrName, String AttrValue, TextBox Ctrl, String ElementValue = "")
        {
            AddRegistered(ref RegSecureCtrl, new SecureCtrlDB
            {
                Ctrl = Ctrl,
                DesKey = new List<byte>(),
                DesIV = new List<byte>(),
                cryptData = new List<byte>(),
                SecureAttrName = DefaultSecureAttrName,
                SecureAttrValueDesKey = DefaultSecureAttrValueDesKey,
                SecureAttrValueDesIV = DefaultSecureAttrValueDesIV,
                SecureAttrValueCryptData = DefaultSecureAttrValueCryptData,
            }, AttrName, AttrValue, ElementValue);
        }

        // コントロール初期値設定
        private void SetDefaultParam()
        {
            // [TextCtrl]
            for (int i = 0; i < RegTextCtrl.Length; i++)
            {
                RegTextCtrl[i].Ctrl.Text = RegTextCtrl[i].ElementValue;
            }

            // [RadioButton]
            for (int i = 0; i < RegRadioCtrl.Length; i++)
            {
                RegRadioCtrl[i].Ctrl.Checked = util.GetBoolean(RegRadioCtrl[i].ElementValue);
            }

            // [CheckBox]
            for (int i = 0; i < RegCheckCtrl.Length; i++)
            {
                RegCheckCtrl[i].Ctrl.Checked = util.GetBoolean(RegCheckCtrl[i].ElementValue);
            }

            // [ComboBox]
            for (int i = 0; i < RegComboCtrl.Length; i++)
            {
                RegComboCtrl[i].Ctrl.Text = RegComboCtrl[i].ElementValue;
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
                RegSecureCtrl[i].cryptData.Clear();
                RegSecureCtrl[i].Ctrl.Text = RegSecureCtrl[i].ElementValue;
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
        public Boolean LoadProc(String LoadFileName)
        {
            if (LoadFileName == String.Empty)
            {
                return false;
            }
            return LoadXmlFile(LoadFileName);
        }

        // ファイル名が空でなければ SaveXmlFile を実行する。LoadProc と対になる形。
        public Boolean SaveSetting(String SaveFileName)
        {
            if (SaveFileName == String.Empty)
            {
                return false;
            }
            return SaveXmlFile(SaveFileName);
        }

        // コントロール読み込み[一括]
        public Boolean LoadXmlFile(String FileName)
        {
            // 初期値を設定
            SetDefaultParam();

            if (FileName == String.Empty)
            {
                return false;
            }

            if (!File.Exists(FileName))
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
                document.Load(FileName);

                // 管理情報は先に読む
                Boolean UseSecure = UseSecureCtrl();
                if (UseSecure)
                {
                    LoadSecureCode(document);
                }

                // 設定値を読む
                foreach (XmlElement element in document.DocumentElement)
                {
                    String ElementValue = element.InnerText;
                    if (LoadTextCtrl(element, ElementValue)) { continue; }
                    if (LoadCheckCtrl(element, ElementValue)) { continue; }
                    if (LoadRadioCtrl(element, ElementValue)) { continue; }
                    if (LoadComboCtrl(element, ElementValue)) { continue; }
                    if (LoadComboCtrlList(element, ElementValue)) { continue; }
                    if (LoadCheckedListBoxCtrl(element, ElementValue)) { continue; }
                    if (LoadDataGridCtrl(element, ElementValue)) { continue; }
                    if (LoadHScrollBarCtrl(element, ElementValue)) { continue; }
                    if (UseSecure && LoadSecureCtrl(element, ElementValue)) { continue; }
                }
                return true;
            }
            catch (Exception)
            {
                SetDefaultParam();
                return false;
            }
        }

        // コントロール読み込み[個別]
        public String LoadXmlFile(String FileName, String AttrName, String AttrValue, String DefaultElement = "")
        {
            String ElementValue = DefaultElement;
            XmlDocument document = TryLoadXmlDocument(FileName);
            if (document == null)
            {
                return ElementValue;
            }

            foreach (XmlElement element in FindElements(document, AttrName, AttrValue, false))
            {
                return element.InnerText;
            }
            return ElementValue;
        }

        // 属性値が一致する要素を順に返す(IsFullMatchがfalseなら部分一致)
        private static IEnumerable<XmlElement> FindElements(XmlDocument document, String AttrName, String AttrValue, Boolean IsFullMatch)
        {
            foreach (XmlElement element in document.DocumentElement)
            {
                String attribute = element.GetAttribute(AttrName);
                Boolean IsMatch = IsFullMatch
                    ? attribute.Equals(AttrValue)
                    : (attribute != String.Empty && attribute.IndexOf(AttrValue) != -1);
                if (IsMatch)
                {
                    yield return element;
                }
            }
        }

        // コントロール読み込み[個別&リスト]
        public String[] LoadXmlFileList(String FileName, String AttrName, String AttrValue)
        {
            XmlDocument document = TryLoadXmlDocument(FileName);
            if (document == null)
            {
                return new String[] { };
            }

            List<String> Values = new List<String>();
            foreach (XmlElement element in FindElements(document, AttrName, AttrValue, false))
            {
                Values.Add(element.InnerText);
            }
            return Values.ToArray();
        }

        // 設定ファイル読み込み[TextCtrl]
        /// <summary>
        /// 登録済みコントロールの中から、この XML 要素に対応するものを探して値を反映する。
        /// 見つけたら反映して true を返し、呼び出し元（LoadXmlFile）のループを次の要素へ進める。
        ///
        /// 種類ごとに同じ形の for ループが並んでいたのを 1 つにまとめたもの。
        /// 種類による違いは「どう照合するか(IsMatch)」と「何を代入するか(Apply)」だけ。
        /// </summary>
        private Boolean LoadRegisteredCtrl<T>(T[] RegisteredCtrl, Func<T, Boolean> IsMatch, Action<T> Apply) where T : OriginDB
        {
            for (int i = 0; i < RegisteredCtrl.Length; i++)
            {
                if (IsMatch(RegisteredCtrl[i]))
                {
                    Apply(RegisteredCtrl[i]);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[TextBox]
        private Boolean LoadTextCtrl(XmlElement element, String ElementValue)
        {
            return LoadRegisteredCtrl(RegTextCtrl,
                Ctrl => Ctrl.IsExistFullMatch(element),
                Ctrl => Ctrl.Ctrl.Text = ElementValue);
        }

        // 設定ファイル読み込み[RadioButton]
        private Boolean LoadRadioCtrl(XmlElement element, String ElementValue)
        {
            return LoadRegisteredCtrl(RegRadioCtrl,
                Ctrl => Ctrl.IsExistFullMatch(element),
                Ctrl => Ctrl.Ctrl.Checked = util.GetBoolean(ElementValue));
        }

        // 設定ファイル読み込み[CheckBox]
        private Boolean LoadCheckCtrl(XmlElement element, String ElementValue)
        {
            return LoadRegisteredCtrl(RegCheckCtrl,
                Ctrl => Ctrl.IsExistFullMatch(element),
                Ctrl => Ctrl.Ctrl.Checked = util.GetBoolean(ElementValue));
        }

        // 設定ファイル読み込み[ComboBox]
        private Boolean LoadComboCtrl(XmlElement element, String ElementValue)
        {
            return LoadRegisteredCtrl(RegComboCtrl,
                Ctrl => Ctrl.IsExistFullMatch(element),
                Ctrl => Ctrl.Ctrl.Text = ElementValue);
        }

        // 設定ファイル読み込み[ComboBoxの履歴一覧]
        private Boolean LoadComboCtrlList(XmlElement element, String ElementValue)
        {
            return LoadRegisteredCtrl(RegComboCtrlList,
                Ctrl => Ctrl.IsExistPartMatch(element),
                Ctrl => Ctrl.Ctrl.Items.Add(ElementValue));
        }

        // 設定ファイル読み込み[CheckedListBox]
        private Boolean LoadCheckedListBoxCtrl(XmlElement element, String ElementValue)
        {
            // 属性値は "<AttrValue>-<項目名>" の形で入っているので、前半で照合して後半を項目名として使う
            return LoadRegisteredCtrl(RegCheckedListBox,
                Ctrl => Ctrl.IsExistPartMatch(element, Ctrl.AttrName, Ctrl.AttrValue + "-"),
                Ctrl =>
                {
                    String attribute = element.GetAttribute(Ctrl.AttrName);
                    int ItemNameIdx = Ctrl.AttrValue.Length + 1;

                    // 名前
                    Ctrl.Ctrl.Items.Add(attribute.Substring(ItemNameIdx));

                    // 状態
                    Boolean IsChecked = util.GetBoolean(ElementValue, "Checked");
                    Ctrl.Ctrl.SetItemChecked(Ctrl.Ctrl.Items.Count - 1, IsChecked);
                });
        }

        // 設定ファイル読み込み[DataGridView]
        private Boolean LoadDataGridCtrl(XmlElement element, String ElementValue)
        {
            // ここだけは照合が 2 通り（セルの値と行数）あるので、まとめずに残してある
            for (int i = 0; i < RegDataGridCtrl.Length; i++)
            {
                if (RegDataGridCtrl[i].IsExistPartMatch(element))
                {
                    String attribute = element.GetAttribute(RegDataGridCtrl[i].AttrName);
                    RegDataGridCtrl[i].LoadData(attribute, ElementValue);
                    return true;
                }
                else if (RegDataGridCtrl[i].IsExistPartMatch(element, RegDataGridCtrl[i].AttrName, RegDataGridCtrl[i].AttrCountValue))
                {
                    RegDataGridCtrl[i].Ctrl.RowCount = util.GetInteger(ElementValue);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[HScrollBar]
        private Boolean LoadHScrollBarCtrl(XmlElement element, String ElementValue)
        {
            return LoadRegisteredCtrl(RegHScrollBarCtrl,
                Ctrl => Ctrl.IsExistFullMatch(element),
                Ctrl => Ctrl.Ctrl.Value = int.Parse(ElementValue));
        }

        // 設定ファイル読み込み[SecureCtrl]
        private Boolean LoadSecureCtrl(XmlElement element, String ElementValue)
        {
            for (int i = 0; i < RegSecureCtrl.Length; i++)
            {
                if (RegSecureCtrl[i].IsExistFullMatch(element))
                {
                    if (!IsExistSecureCode(RegSecureCtrl[i]))
                    {
                        return false;
                    }

                    RegSecureCtrl[i].Ctrl.Text = GetDecodeString(ElementValue, RegSecureCtrl[i]);
                    return true;
                }
            }
            return false;
        }

        private String GetDecodeString(String ElementValue, SecureCtrlDB SecureCtrl)
        {
            StcSecure secure = new StcSecure();

            String DecodeString = secure.Decode(ElementValue,
                SecureCtrl.DesKey.ToArray(),
                SecureCtrl.DesIV.ToArray(),
                SecureCtrl.cryptData.ToArray());

            return DecodeString;
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

            if (secure.cryptData.Count <= 0)
            {
                return false;
            }

            return true;
        }

        private Boolean UseSecureCtrl()
        {
            // 管理情報がなければ読まない
            if (RegSecureCtrl.Length == 0)
            {
                return false;
            }
            return true;
        }

        private void LoadSecureCode(XmlDocument document)
        {
            foreach (XmlElement element in document.DocumentElement)
            {
                String ElementValue = element.InnerText;
                for (int i = 0; i < RegSecureCtrl.Length; i++)
                {
                    if (RegSecureCtrl[i].IsExistPartMatch(element, RegSecureCtrl[i].SecureAttrName, DefaultSecureAttrValueDesKey))
                    {
                        RegSecureCtrl[i].DesKey.Add(Convert.ToByte(ElementValue));
                        continue;
                    }

                    if (RegSecureCtrl[i].IsExistPartMatch(element, RegSecureCtrl[i].SecureAttrName, DefaultSecureAttrValueDesIV))
                    {
                        RegSecureCtrl[i].DesIV.Add(Convert.ToByte(ElementValue));
                        continue;
                    }

                    if (RegSecureCtrl[i].IsExistPartMatch(element, RegSecureCtrl[i].SecureAttrName, DefaultSecureAttrValueCryptData))
                    {
                        RegSecureCtrl[i].cryptData.Add(Convert.ToByte(ElementValue));
                        continue;
                    }
                }
            }
        }

        /// 設定ファイルの読み込み[バージョン]
        public int LoadXmlVersion(String FileName)
        {
            XmlDocument document = TryLoadXmlDocument(FileName);
            if (document == null)
            {
                return 0;
            }
            return LoadXmlVersion(document);
        }

        // ファイルが無い・読めない・XMLとして壊れている場合は null を返す
        private static XmlDocument TryLoadXmlDocument(String FileName)
        {
            if (!File.Exists(FileName))
            {
                return null;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(FileName);
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
            int VersionNo = 0;
            foreach (XmlElement element in FindElements(document, VersionAttrName, VersionKeyName, true))
            {
                int.TryParse(element.InnerText, out VersionNo);
                break;
            }
            return VersionNo;
        }

        /// <summary>
        /// Save用にXmlファイルをオープンする
        /// </summary>
        /// <param name="file_name"></param>
        /// <returns></returns>
        public XmlDocument OpenSaveXmlFile()
        {
            return new XmlDocument();
        }

        public Boolean CloseSaveXmlFile(String file_name)
        {
            try
            {
                AtomicFile.Write(file_name, m_WriteDocument.Save);
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
        /// <param name="file_name"></param>
        /// <param name="version_no"></param>
        /// <returns></returns>
        public Boolean SaveXmlFile(String file_name, String version_no = "1")
        {
            XmlDocument document = OpenSaveXmlFile();
            SaveXmlFile(document, version_no);
            return CloseSaveXmlFile(file_name);
        }

        /// <summary>
        /// Write
        /// </summary>
        /// <param name="document"></param>
        /// <param name="version_no"></param>
        public void SaveXmlFile(XmlDocument document, String version_no = "1")
        {
            m_WriteDocument = document;

            SetRoot();
            SaveXmlVersion(version_no);

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
        private void SaveXmlVersion(String Version)
        {
            SaveXmlString(VersionElementName, VersionAttrName, VersionKeyName, Version);
        }

        // パラメータ保存 /////////////////////////////////////////
        // ファイル保存[String]
        public void SaveXmlString(String Element, String AttrName, String AttrValue, String Text)
        {
            XmlElement element = m_WriteDocument.CreateElement(Element);
            element.SetAttribute(AttrName, AttrValue);
            element.InnerText = Text;
            m_WriteRoot.AppendChild(element);
        }

        // ファイル保存[String]
        public void SaveXmlString(String AttrName, String AttrValue, String Text)
        {
            SaveXmlString(ElementName, AttrName, AttrValue, Text);
        }

        // ファイル保存[StringArray]
        public void SaveXmlParamAll(String AttrName, String AttrValue, String[] StrArray)
        {
            if(StrArray == null)
            {
                return;
            }

            for (int i = 0; i < StrArray.Length; i++)
            {
                SaveXmlString(AttrName, AttrValue + i.ToString(), StrArray[i]);
            }
        }

        // ファイル保存[int]
        public void SaveXmlParam(String AttrName, String AttrValue, int Number)
        {
            SaveXmlString(AttrName, AttrValue, Number.ToString());
        }

        // ファイル保存[byte]
        // 暗号化した管理情報(鍵・IV・データ)の保存にしか使わないのでprivate
        private void SaveXmlManageParam(String AttrName, String AttrValue, byte[] Value)
        {
            if (Value == null)
            {
                return;
            }

            for (int i = 0; i < Value.Length; i++)
            {
                SaveXmlString(AttrName, AttrValue + i.ToString(), Value[i].ToString());
            }
        }

        // ファイル保存[String]
        public void SaveXmlString(XmlDocument WriteDocument, XmlElement WriteRoot, String Element, String AttrName, String AttrValue, String Text)
        {
            XmlElement element = WriteDocument.CreateElement(Element);
            element.SetAttribute(AttrName, AttrValue);
            element.InnerText = Text;
            WriteRoot.AppendChild(element);
        }

        // コントロール保存 /////////////////////////////////////////
        // ファイル保存[TextBox]
        /// <summary>
        /// 登録済みコントロールを 1 つずつ、属性名・属性値・現在値の組で書き出す。
        /// 種類による違いは「現在値をどう文字列にするか(GetValue)」だけ。
        ///
        /// 1 つのコントロールから複数行を書き出すもの（ComboBox の履歴一覧、CheckedListBox、
        /// DataGridView）は内側にもループがあって形が違うので、ここには通していない。
        /// 無理に共通化すると渡すデリゲートが増えてかえって読みにくくなるため。
        /// </summary>
        private void SaveRegisteredCtrl<T>(T[] RegisteredCtrl, Func<T, String> GetValue) where T : OriginDB
        {
            for (int i = 0; i < RegisteredCtrl.Length; i++)
            {
                SaveXmlString(RegisteredCtrl[i].AttrName, RegisteredCtrl[i].AttrValue, GetValue(RegisteredCtrl[i]));
            }
        }

        private void SaveTextCtrl()
        {
            SaveRegisteredCtrl(RegTextCtrl, Ctrl => Ctrl.Ctrl.Text);
        }

        // ファイル保存[RadioButton]
        private void SaveRadioCtrl()
        {
            SaveRegisteredCtrl(RegRadioCtrl, Ctrl => Ctrl.Ctrl.Checked.ToString());
        }

        // ファイル保存[CheckBox]
        private void SaveCheckCtrl()
        {
            SaveRegisteredCtrl(RegCheckCtrl, Ctrl => Ctrl.Ctrl.Checked.ToString());
        }

        // ファイル保存[ComboBox]
        private void SaveComboCtrl()
        {
            SaveRegisteredCtrl(RegComboCtrl, Ctrl => Ctrl.Ctrl.Text);
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
                for (int RowCount = 0; RowCount < RegDataGridCtrl[i].Ctrl.RowCount; RowCount++)
                {
                    for (int ColumnCount = 0; ColumnCount < RegDataGridCtrl[i].Ctrl.ColumnCount; ColumnCount++)
                    {
                        String CellValue = util.GetDataGridCell(RegDataGridCtrl[i].Ctrl, RowCount, ColumnCount);

                        SaveXmlString(RegDataGridCtrl[i].AttrName,
                            RegDataGridCtrl[i].AttrValue + "_" + RowCount.ToString() + "-" + ColumnCount.ToString(),
                            CellValue);
                    }
                }
            }
        }

        // ファイル保存[HScrollBar]
        private void SaveHScrollBarCtrl()
        {
            SaveRegisteredCtrl(RegHScrollBarCtrl, Ctrl => Ctrl.Ctrl.Value.ToString());
        }

        // ファイル保存[暗号化キー]
        private void SaveSecureCtrl()
        {
            StcSecure secure = new StcSecure();

            byte[] DesKey;
            byte[] DesIV;
            byte[] cryptData;
            String SecureWord;
            for (int i = 0; i < RegSecureCtrl.Length; i++)
            {
                SecureWord = secure.Encode(RegSecureCtrl[i].Ctrl.Text, out DesKey, out DesIV, out cryptData);
                SaveXmlString(RegSecureCtrl[i].AttrName, RegSecureCtrl[i].AttrValue, SecureWord);

                SaveXmlManageParam(DefaultSecureAttrName, DefaultSecureAttrValueDesKey, DesKey);
                SaveXmlManageParam(DefaultSecureAttrName, DefaultSecureAttrValueDesIV, DesIV);
                SaveXmlManageParam(DefaultSecureAttrName, DefaultSecureAttrValueCryptData, cryptData);
            }
        }

        // *******************************************************************************
        // JSON汎用プロファイル([[_Common/JsonFileStorage.cs]]と組み合わせて使う)
        //
        // EventRecorderのように保存したいデータに意味の分かる名前を付けられる場合は専用の
        // POCO(Profile.cs)を作る方が読みやすいが、RegistCtrlで何十個ものコントロールを
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
        // まずSetDefaultParamで初期値に戻してから、profileにある値だけ上書きする
        // (XML読込のLoadXmlFileと同じ、見つからない項目は初期値のまま残す方針)
        public void ApplyGenericProfile(GenericProfile profile)
        {
            SetDefaultParam();
            if (profile == null)
            {
                return;
            }

            String value;
            for (int i = 0; i < RegTextCtrl.Length; i++) { if (profile.Values.TryGetValue(GenericKey(RegTextCtrl[i]), out value)) { RegTextCtrl[i].Ctrl.Text = value; } }
            for (int i = 0; i < RegRadioCtrl.Length; i++) { if (profile.Values.TryGetValue(GenericKey(RegRadioCtrl[i]), out value)) { RegRadioCtrl[i].Ctrl.Checked = util.GetBoolean(value); } }
            for (int i = 0; i < RegCheckCtrl.Length; i++) { if (profile.Values.TryGetValue(GenericKey(RegCheckCtrl[i]), out value)) { RegCheckCtrl[i].Ctrl.Checked = util.GetBoolean(value); } }
            for (int i = 0; i < RegComboCtrl.Length; i++) { if (profile.Values.TryGetValue(GenericKey(RegComboCtrl[i]), out value)) { RegComboCtrl[i].Ctrl.Text = value; } }
            for (int i = 0; i < RegHScrollBarCtrl.Length; i++) { if (profile.Values.TryGetValue(GenericKey(RegHScrollBarCtrl[i]), out value)) { RegHScrollBarCtrl[i].Ctrl.Value = util.GetInteger(value); } }

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
        private static String GenericKey(OriginDB Ctrl)
        {
            return Ctrl.AttrName + "|" + Ctrl.AttrValue;
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
    // RegistItem(Form1 Parent)はプロジェクト固有のForm1型を直接引数に取るためジェネリック
    // からは呼べない。LoadProc/SaveSettingもFFEdit等の一部プロジェクトでは
    // (String, Form1)の2引数オーバーロードに副作用付きで差し替えられており、
    // 基底クラス側で1引数版を固定で呼んでしまうと差し替え版が呼ばれず挙動が変わってしまう。
    // そのためRegistItem/LoadProc/SaveSettingの呼び出しは今まで通り各Form1コンストラクタに
    // 明示的に書く方針とし、プロジェクトによらず完全に同一だった2行だけを集約している。
    abstract class StcBaseForm<TSaveRestore> : Form
        where TSaveRestore : StcSaveRestore, new()
    {
        protected StcUtils util = new StcUtils();
        protected TSaveRestore sr = new TSaveRestore();

        protected void InitializeCommonSettings(Icon FormIcon)
        {
            this.Icon = FormIcon;
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
        // 鍵ごと暗号文と一緒に保存する方式。StcSaveRestore.RegistSecureCtrl経由でパスワード
        // 保存等に使われている)だけだった。「固定鍵をソースに書く」という誤った見本を
        // 残さないため、未使用だった固定鍵版は削除した(パフォーマンス改善#8で対応)。

        // 暗号化witch鍵
        public String Encode(String str, out byte[] DesKey, out byte[] DesIV, out byte[] cryptData)
        {
            TripleDESCryptoServiceProvider TDES = new TripleDESCryptoServiceProvider();
            DesKey = TDES.Key;
            DesIV = TDES.IV;

            byte[] source = Encoding.Unicode.GetBytes(str);

            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();
            cryptData = TransformBytes(source, des.CreateEncryptor(DesKey, DesIV));

            return Encoding.Unicode.GetString(cryptData);
        }

        // 複合化with鍵
        public String Decode(String str, byte[] DesKey, byte[] DesIV, byte[] cryptData)
        {
            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();
            byte[] destination = TransformBytes(cryptData, des.CreateDecryptor(DesKey, DesIV));

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
        public enum ENCORD_TYPE
        {
            SHIFT_JIS,
            EUC_JP,
        };

        StcUtils utils = new StcUtils();

        // テンポラリファイル作成
        public String CreateTempFile(String Ext = "")
        {
            String DestStr = Path.GetTempFileName();
            if (Ext != String.Empty)
            {
                String SrcStr = DestStr;
                DestStr = DestStr.Replace(".tmp", "." + Ext);
                if (!File.Exists(DestStr))
                {
                    File.Move(SrcStr, DestStr);
                }
            }
            return DestStr;
        }

        // ファイル作成
        public void CreateFile(String FileName, String Data, Boolean DebugMode = false)
        {
            CreateFile(FileName, Data, StcFileInputOutput.ENCORD_TYPE.SHIFT_JIS, DebugMode);
        }

        // ファイル作成
        public void CreateFile(String FileName, String Data, ENCORD_TYPE EncordType, Boolean DebugMode = false)
        {
            // デバッグモードのときは、バッチの画面を閉じない
            if (DebugMode)
            {
                Data += @"PAUSE" + System.Environment.NewLine;
            }

            // 改行コードを変換
            Data = utils.ChangeNewLineCode(EncordType, Data);
            using (StreamWriter sw = new StreamWriter(FileName, false, GetEncord(EncordType)))
            {
                sw.Write(Data);
            }
        }

        // 文字コード変換[UTF8→Sjis]
        // .NET文字列は内部的に常にUTF-16なので、変換は「読み込み時のエンコード指定」と
        // 「書き込み時のエンコード指定(SaveFileがShift_JIS固定)」だけで完了する
        public Boolean ChangeStringCodeUTF2SJIS(String InFileName, String OutFileName)
        {
            SaveFile(OutFileName, LoadFileWithEncoding(InFileName, Encoding.GetEncoding("utf-8")));
            return true;
        }

        // 文字コード変換[Euc→Sjis]
        public Boolean ChangeStringCodeEUC2SJIS(String InFileName, String OutFileName)
        {
            if (!utils.IsExistPath(InFileName))
            {
                // ファイルが存在しない
                return false;
            }

            SaveFile(OutFileName, LoadFileWithEncoding(InFileName, Encoding.GetEncoding("EUC-JP")));
            return true;
        }

        // 読み込みファイルを選択
        // InitialDirectoryを指定すると、ダイアログの初期表示フォルダをそこに固定できる
        // (未指定時はWindowsが前回開いたフォルダ等を使う、これまで通りの挙動)
        public String SelectLoadFileName(String FileName = "", String InitialDirectory = "")
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = FileName;
            if (InitialDirectory != String.Empty)
            {
                ofd.InitialDirectory = InitialDirectory;
            }
            ofd.Filter = "XMLファイル(*.xml)|*.xml|すべてのファイル(*.*)|*.*";
            ofd.Title = "読み込む設定ファイルを選択してください";

            String LoadFileName = "";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LoadFileName = ofd.FileName;
            }
            return LoadFileName;
        }

        // 保存ファイルを選択
        // InitialDirectoryを指定すると、(任意のファイル名を指定する側の)ダイアログの
        // 初期表示フォルダをそこに固定できる
        public String SelectSaveFileName(String FileName, String InitialDirectory = "")
        {
            String SaveFileName = "";
            if (FileName != String.Empty)
            {
                DialogResult DlgResult = MessageBox.Show(
                    "現在の設定ファイルに保存しますか？" + Environment.NewLine + FileName,
                    "保存ファイル名の選択",
                    MessageBoxButtons.YesNoCancel);
                if (DlgResult == DialogResult.Cancel)
                {
                    return "";
                }
                else if (DlgResult == DialogResult.Yes)
                {
                    // 既存のファイル名を使用
                    SaveFileName = FileName;
                }
            }

            // 任意のファイル名を設定
            if (SaveFileName == String.Empty)
            {
                // 任意のファイル名を指定
                SaveFileDialog ofd = new SaveFileDialog();
                ofd.FileName = FileName;
                if (InitialDirectory != String.Empty)
                {
                    ofd.InitialDirectory = InitialDirectory;
                }
                ofd.Filter = "XMLファイル(*.xml)|*.xml|すべてのファイル(*.*)|*.*";
                ofd.Title = "保存する設定ファイルを選択してください";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    SaveFileName = ofd.FileName;
                }
            }

            // 保存できるファイルか
            if (SaveFileName != String.Empty && !IsSaveValidFilePath(SaveFileName))
            {
                RemoveReadonlyAttribute(SaveFileName);
            }

            return SaveFileName;
        }

        // 保存できるファイルパスか
        public Boolean IsSaveValidFilePath(String FilePathName)
        {
            FileInfo cFileInfo = new FileInfo(FilePathName);
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
        public Boolean RemoveReadonlyAttribute(String FileName)
        {
            return utils.RemoveReadonlyAttribute(FileName);
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
        public Boolean DeleteDirectoryAndFile(String DeletePath)
        {
            Boolean DeleteComplete = true;

            if (DeletePath.IndexOf("*") != -1)
            {
                // ワイルドカードの指定があった
                DeleteComplete = DeleteAnyFile(DeletePath);
            }
            else if (Directory.Exists(DeletePath))
            {
                DirectoryInfo delDir = new DirectoryInfo(DeletePath);
                try
                {
                    RemoveReadonlyAttribute(delDir);
                    delDir.Delete(true);
                }
                catch (Exception)
                {
                    DeleteComplete = false;
                }
            }
            else if (File.Exists(DeletePath))
            {
                DeleteComplete = DeleteFileWithRemoveReadonlyAttribute(DeletePath);
            }

            return DeleteComplete;
        }

        // ファイル削除
        public Boolean DeleteAnyFile(String TargetName)
        {
            String DirName = Path.GetDirectoryName(TargetName);
            String FileName = Path.GetFileName(TargetName);

            if (FileName.IndexOf("*") == -1)
            {
                // ワイルドカードの指定が無かった終了
                return true;
            }

            if (!Directory.Exists(DirName))
            {
                // 無効なディレクトリだったら終了
                return true;
            }

            // ファイルリストアップ
            Boolean DeleteComplete = true;
            String[] files = Directory.GetFiles(DirName, FileName);
            for (int i = 0; i < files.Length; i++)
            {
                if (!DeleteFileWithRemoveReadonlyAttribute(files[i]))
                {
                    DeleteComplete = false;
                }
            }

            return DeleteComplete;
        }

        // ファイル削除（読み取り属性解除）
        public Boolean DeleteFileWithRemoveReadonlyAttribute(String DeleteFile)
        {
            Boolean DeleteComplete = true;

            RemoveReadonlyAttribute(new FileInfo(DeleteFile));

            try
            {
                File.Delete(DeleteFile);
            }
            catch (Exception)
            {
                DeleteComplete = false;
            }

            return DeleteComplete;
        }

        // ディレクトリを移動
        public void MoveDirectory(String SourcePath, String TargetPath, Boolean IsSubDirInclude = true)
        {
            if (IsSubDirInclude)
            {
                // サブディレクトリも対象
                try
                {
                    Directory.Move(SourcePath, TargetPath);
                }
                catch (Exception)
                {
                    MessageBox.Show("Move処理に失敗しました。" + Environment.NewLine +
                        "[" + SourcePath + "]" + Environment.NewLine +
                        "[" + TargetPath + "]" + Environment.NewLine);
                }
            }
            else
            {
                // ファイルだけが対象
                MoveFileOnly(SourcePath, TargetPath);
            }
        }

        // ファイルだけを移動
        private void MoveFileOnly(String SourcePath, String TargetPath)
        {
            Directory.CreateDirectory(TargetPath);

            String[] files = Directory.GetFileSystemEntries(SourcePath);

            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i]))
                {
                    String DestName = TargetPath + @"\" + GetLastPathName(files[i]);
                    if ( ! FileMove(files[i], DestName) )
                    {
                        MessageBox.Show("Move処理に失敗しました。" + Environment.NewLine +
                            "[" + files[i] + "]" + Environment.NewLine +
                            "[" + DestName + "]" + Environment.NewLine);
                    }
                }
            }
        }

        // ファイル移動[移動できないことを考慮]
        public Boolean FileMove(String SourcePath, String TargetPath)
        {
            Boolean IsSuccess = true;
            try
            {
                File.Move(SourcePath, TargetPath);
            }
            catch
            {
                IsSuccess = false;
            }
            return IsSuccess;
        }

        // ファイルフルパスの中から先頭のパスを取得
        public String GetFirstPathName(String Path)
        {
            String[] Folders = Path.Split('\\');
            return Folders[0];
        }

        // ファイルフルパスの中から最後のパスを取得
        public String GetLastPathName(String Path)
        {
            String[] Folders = Path.Split('\\');
            return Folders[Folders.Length - 1];
        }

        // ディレクトリが無ければ作る(IsAutoCreateがfalseのときは作ってよいか確認する)。
        // 作成しなかった場合だけfalseを返す
        public Boolean EnsureDirectory(String Path, Boolean IsAutoCreate = false)
        {
            Boolean IsExist = true;
            if (!Directory.Exists(Path))
            {
                Boolean IsCreate = true;
                if (!IsAutoCreate)
                {
                    String Msg = String.Format("ディレクトリは存在しません。作成しますか？\n{0}", Path);

                    DialogResult result = MessageBox.Show(Msg,
                        "Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button1);

                    // 「いいえ」を選んでも作成してしまっていた(IsCreateがtrueのまま)
                    IsCreate = (result == DialogResult.Yes);
                }

                if (IsCreate)
                {
                    Directory.CreateDirectory(Path);
                }
                else
                {
                    IsExist = false;
                }
            }

            return IsExist;
        }

        // データをセーブする
        public void SaveFile(String FilePath, String Data, Boolean IsAppend = false)
        {
            using (StreamWriter sw = new StreamWriter(FilePath, IsAppend, System.Text.Encoding.GetEncoding("Shift_JIS")))
            {
                sw.Write(Data);
            }
        }

        // ファイルデータを取得する
        public String LoadFile(String FilePath)
        {
            return LoadFileWithEncoding(FilePath, Encoding.GetEncoding("Shift_JIS"));
        }

        // 指定した文字コードとして読み込む。ファイルが無ければ空文字を返す
        private String LoadFileWithEncoding(String FilePath, Encoding SrcEncoding)
        {
            if (!File.Exists(FilePath))
            {
                return "";
            }

            using (StreamReader sr = new StreamReader(FilePath, SrcEncoding))
            {
                return sr.ReadToEnd();
            }
        }

        public Boolean DetectFileData(String FilePath, String DetectWord)
        {
            Boolean IsFound = false;
            String Data = LoadFile(FilePath);

            if (Data.IndexOf(DetectWord) != -1)
            {
                IsFound = true;
            }

            return IsFound;
        }

        private Encoding GetEncord(ENCORD_TYPE EncordType)
        {
            // 未知の種別は空文字のままEncoding.GetEncoding("")に渡されて例外になっていたので、既定をShift_JISにする
            String EncordStr = "shift_jis";
            if (EncordType == ENCORD_TYPE.EUC_JP)
            {
                EncordStr = "euc-jp";
            }

            return Encoding.GetEncoding(EncordStr);
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
        private static extern bool EnumDisplaySettingsA(String DeviceName, int ModeNum, ref DEVMODE DevMode);

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
        public Boolean IsStopRequest;
        public Boolean IsCaptureCase;

        public String FileFormat;
        public int FileIdx;
        public CAPTURE_TARGET CaptureTarget;

        public Point MousePt;
        public MOUSE_EVENT MouseEvent = MOUSE_EVENT.LEFT_CLICK;
        public Boolean IsMouseMove = true;
        public Boolean IsRestoreMousePos = false;
        public TimeSpan SleepTimeMsec;    // Sleepする時間
        public TimeSpan SleepCycleMsec;   // Sleepを刻む感覚

        // CAPTURE_TARGET.CURRENT_SCREEN で「このコントロール(通常は呼び出し元のForm)が
        // 今表示されているモニタ」を判定するために使う。設定されていなければ
        // Screen.PrimaryScreen(メイン画面)にフォールバックする。
        public Control TargetWindow;

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
            SleepTimeMsec = new TimeSpan();
            SleepCycleMsec = TimeSpan.FromSeconds(1);
        }

        // 処理停止要求
        public void Stop()
        {
            IsStopRequest = true;
        }

        // マウスを移動させるか
        public void SetMouseMove(Boolean IsMove)
        {
            IsMouseMove = IsMove;
        }

        // マウス移動後にもとの位置へ戻すか
        public void RestoreMousePosition(Boolean IsResotre)
        {
            IsRestoreMousePos = IsResotre;
        }

        // キャプチャ対象
        public void SetCaptureTarget(CAPTURE_TARGET CaptTarget)
        {
            CaptureTarget = CaptTarget;
        }

        // Sleep時間設定
        public void SetSleepTimeMsec(String msec)
        {
            if (!msec.Equals(""))
            {
                SleepTimeMsec = TimeSpan.FromMilliseconds(uint.Parse(msec));
            }
        }

        // キャプチャ有無設定
        public void SetCaptureCase(Boolean IsCapture)
        {
            IsCaptureCase = IsCapture;
        }

        // マウスの座標設定
        public Boolean SetMousePoint(String x, String y)
        {
            Boolean IsSetPoint = false;
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
                IsSetPoint = true;
            }

            return IsSetPoint;
        }

        public void SetMouseEvent(MOUSE_EVENT Event)
        {
            MouseEvent = Event;
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
            for (TimeSpan Timer = TimeSpan.FromMilliseconds(0); Timer < SleepTimeMsec; Timer += SleepCycleMsec)
            {
                if (IsStopRequest)
                {
                    break;
                }
                System.Threading.Thread.Sleep(SleepCycleMsec);
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

            String PictFileName = FileFormat + "_" + FileIdx.ToString() + ".png";
            FileIdx++;  // 次使うとき用にインクリ

            if (!CaptureEvent(PictFileName))
            {
                // 処理失敗したファイル名を追記
                ErrorLog += PictFileName + Environment.NewLine;
            }
        }

        private Boolean CaptureEvent(String PictFileName)
        {
            Boolean IsSucess = false;
            switch (CaptureTarget)
            {
                case CAPTURE_TARGET.FULL_SCREEN:
                    IsSucess = SaveWithPrintScreen("^{PRTSC}", PictFileName);
                    break;
                case CAPTURE_TARGET.CURRENT_SCREEN:
                    IsSucess = SaveWithCaptureCurrentScreen(PictFileName);
                    break;
                case CAPTURE_TARGET.CURRENT_WINDOW:
                    IsSucess = SaveWithPrintScreen("%{PRTSC}", PictFileName);
                    break;
                default:
                    break;
            }

            return IsSucess;
        }

        // PrintScreenでクリップボードへ取り込んでから保存する
        // (Ctrl+PrintScreen="^{PRTSC}"で全画面、Alt+PrintScreen="%{PRTSC}"でアクティブウィンドウ)
        private Boolean SaveWithPrintScreen(String PrintScreenKey, String PictFileName)
        {
            SendKeys.SendWait(PrintScreenKey);

            return SaveClipboard(PictFileName);
        }

        private Boolean SaveClipboard(String PictFileName)
        {
            Boolean IsSucess = true;
            Image img = null;
            try
            {
                IDataObject d = Clipboard.GetDataObject();

                //ビットマップデータ形式に関連付けられているデータを取得
                img = (Image)d.GetData(DataFormats.Bitmap);
                img.Save(PictFileName);
            }
            catch (Exception)
            {
                IsSucess = false;
            }
            finally
            {
                // クリップボードから画像を取得できなかった場合はnullのままなので、そのままDisposeすると例外になる
                if (img != null)
                {
                    img.Dispose();
                }
            }

            return IsSucess;
        }

        private Boolean SaveWithCaptureCurrentScreen(String PictFileName)
        {
            // 「CURRENT_SCREEN」という名前なのに、以前は全モニタをまとめた1枚を作っていた
            // (バグ修正前は範囲計算自体も間違っていたが、直しても「全画面結合」という
            // 設計そのものがFULL_SCREEN(Ctrl+PrintScreenで撮る画面全体=全モニタ結合と
            // ほぼ同じ結果)と機能が重複していた)。名前の通り「呼び出し元のウィンドウが
            // 今表示されているモニタ1枚だけ」を撮るように直した。
            // SaveClipboard(SendKeys経由の他2種)と同じく、失敗時はtrueを固定で返さず
            // falseを返すようにした(ディスク書き込み失敗・GDI例外等を吸収する)。
            Boolean IsSucess = true;
            try
            {
                Screen TargetScreen = (TargetWindow != null) ? Screen.FromControl(TargetWindow) : Screen.PrimaryScreen;

                // Screen.Boundsではなく物理ピクセルでの範囲を使う(表示倍率が100%以外のモニタ対策)
                Rectangle CaptureArea = GetPhysicalBounds(TargetScreen);

                using (Bitmap bmp = new Bitmap(CaptureArea.Width, CaptureArea.Height))
                {
                    //Graphicsの作成
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        //対象モニタの左上座標からコピーする
                        g.CopyFromScreen(CaptureArea.Location, new Point(0, 0), bmp.Size);
                    }

                    // ファイル保存
                    bmp.Save(PictFileName);
                }
            }
            catch (Exception)
            {
                IsSucess = false;
            }

            return IsSucess;
        }

        // モニタの物理ピクセルでの範囲を取得する。
        // Screen.Boundsは表示倍率で割られた論理サイズなので、CopyFromScreen(物理ピクセル単位)と
        // 組み合わせると倍率100%以外のモニタで欠けが出る。EnumDisplaySettingsで実際の
        // 解像度と配置を問い合わせて、そちらを使う。取得に失敗したらScreen.Boundsで代用する。
        private Rectangle GetPhysicalBounds(Screen TargetScreen)
        {
            DEVMODE DevMode = new DEVMODE();
            DevMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettingsA(TargetScreen.DeviceName, ENUM_CURRENT_SETTINGS, ref DevMode))
            {
                return TargetScreen.Bounds;
            }

            if (DevMode.dmPelsWidth <= 0 || DevMode.dmPelsHeight <= 0)
            {
                return TargetScreen.Bounds;
            }

            return new Rectangle(DevMode.dmPositionX, DevMode.dmPositionY,
                                 DevMode.dmPelsWidth, DevMode.dmPelsHeight);
        }
        #endregion

        #region マウス操作
        // マウスのイベント処理
        public void MouseProc(String x, String y, MOUSE_EVENT Event)
        {
            // 座標設定
            if (!SetMousePoint(x, y))
            {
                return;
            }
            SetMouseEvent(Event);
            MouseProc();
        }

        // マウスのイベント処理
        public void MouseProc()
        {
            if (IsStopRequest)
            {
                return;
            }

            Point MousePtOrg = new Point(Cursor.Position.X, Cursor.Position.Y);

            //マウス移動
            if (IsMouseMove)
            {
                Cursor.Position = new Point(MousePt.X, MousePt.Y);
            }

            //struct 配列の宣言
            INPUT[] input = new INPUT[2];
            uint input_num = 0;

            switch(MouseEvent)
            {
                case MOUSE_EVENT.LEFT_CLICK:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                    input[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
                    input_num = 2;
                    break;
                case MOUSE_EVENT.LEFT_DOWN:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                    input_num = 1;
                    break;
                case MOUSE_EVENT.LEFT_UP:
                    input[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;
                    input_num = 1;
                    break;
                default:
                    break;
            }

            //イベントの一括生成
            SendInput(input_num, input, Marshal.SizeOf(input[0]));

            if ( IsMouseMove)
            {
                // マウスの位置をもとに戻す
                if (IsRestoreMousePos)
                {
                    Cursor.Position = new Point(MousePtOrg.X, MousePtOrg.Y);
                }
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
        private Boolean IsDebugMode = false;        // デバッグモードOnOff
        private Boolean UseTimeInFileName = false;  // ファイル名に時間を入れる
        private Boolean IsWriteTime = false;		// デバッグログに時間を入れる
        private String DebugLogFile = "DebugLog.txt";

        private int FileIndex = 1;
        private int FileIndexDigit = 2;

        private StcFileInputOutput fio = new StcFileInputOutput();

        // コンストラクタ
        public StcDebug()
        {
        }

        // コンストラクタ
        public StcDebug(Boolean IsMode)
        {
            IsDebugMode = IsMode;
        }

        public void SetDebugMode(Boolean IsMode)
        {
            IsDebugMode = IsMode;
        }

        public Boolean GetDebugMode()
        {
            return IsDebugMode;
        }

        public void SetUseTimeInFileName(Boolean UseTime)
        {
            UseTimeInFileName = UseTime;
        }

        public Boolean GetUseTimeInFileName()
        {
            return UseTimeInFileName;
        }

        public void SetWriteTime(Boolean WriteTime)
        {
            IsWriteTime = WriteTime;
        }

        public Boolean GetWriteTime()
        {
            return IsWriteTime;
        }

        public String GetDebugLogFilename()
        {
            return DebugLogFile;
        }

        public void SetDebugLogFilename(String LogFile)
        {
            DebugLogFile = LogFile;
        }

        // 共通ファイルにWrite
        public void WriteData(String Data, Boolean IsAppend = true)
        {
            if (IsDebugMode == true)
            {
                String WriteData = "";
                if (IsWriteTime)
                {
                    WriteData = DateTime.Now.ToString() + "  : ";
                }
                WriteData += Data + Environment.NewLine;
                fio.SaveFile(DebugLogFile, WriteData, IsAppend);
            }
        }

        // 新しいファイルにWrite
        public void WriteDataInNewFile(String Data, String FilenameSuffixStr = "", String Extension = "txt")
        {
            if (!IsDebugMode)
            {
                return;
            }

            String FileName = FileIndex.ToString().PadLeft(FileIndexDigit, '0');
            if (UseTimeInFileName)
            {
                FileName += System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            }
            FileName += FilenameSuffixStr + "." + Extension;

            fio.SaveFile(FileName, Data);

            FileIndex++;
        }
    }
}
