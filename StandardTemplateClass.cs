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

        // プロセス強制終了
        public void ProcessKill(String ProcessName)
        {
            Process[] processes = Process.GetProcesses();
            foreach (Process proc in processes)
            {
                if (proc.ProcessName == ProcessName)
                {
                    // プロセスを強制終了させる
                    proc.Kill();
                }
            }
        }

        // プロセス起動中か？
        public Boolean IsStartingProcess(String ProcessName, String MachineName = "")
        {
            Boolean IsStarting = false;
            String TargetProcessName = Path.GetFileNameWithoutExtension(ProcessName);

            Process[] hProcesses = Process.GetProcesses();

            // 他PCのプロセスを取得する場合
            if (MachineName != String.Empty)
            {
                hProcesses = Process.GetProcesses(MachineName);
            }

            // 取得できたプロセスからプロセス名を取得する
            foreach (Process hProcess in hProcesses)
            {
                if (hProcess.ProcessName == TargetProcessName)
                {
                    IsStarting = true;
                    break;
                }
            }

            return IsStarting;
        }

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

        public Boolean IsExistFiles(String[] Filename, String BaseDir = "")
        {
            Boolean IsExist = true;

            // BaseDirの終端に"\"を付加
            if (BaseDir != String.Empty)
            {
                BaseDir = BaseDir.TrimEnd('\\') + '\\';
            }

            for (int i = 0; i < Filename.Length; i++)
            {
                if (!File.Exists(BaseDir + Filename[i]))
                {
                    IsExist = false;
                    break;
                }
            }

            return IsExist;
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

        public Boolean IsExistDirectory(String DirectoryPath, Boolean IsNoticeExceptMsg = false, String ExceptMsgStr = "")
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

        public String GetAbsolutePath(String RelativePath)
        {
            return Path.GetFullPath(RelativePath);
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

        // ファイルパス実行 With 読み取り専用属性の解除可能
        public Boolean ExecuteFileSupportReadOnly(String ExecPath, KeyEventArgs e)
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

            //TODO：参照関係を直したい。UtilからFileIOは参照しない。
            StcFileInputOutput FileIO = new StcFileInputOutput();
            FileIO.RemoveReadonlyAttribute(ExecPath);
            ExecutePath(ExecPath);

            return true;
        }

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
                    NewPath.Insert(1, @":");
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
            return OldPathArray.Select(str => str.Replace(@"\", @"/")).ToArray();
        }

        // パス区切りを変換[/ → \]
        public String ChangeLinuxPath2WindowsPath(String OldPath)
        {
            return OldPath.Replace(@"/", @"\");
        }

        // パス区切りを変換[/ → \]
        public String[] ChangeLinuxPath2WindowsPath(String[] OldPathArray)
        {
            return OldPathArray.Select(str => str.Replace(@"/", @"\")).ToArray();
        }

        // 改行コードを変換[LF→CRLF]
        public String ChangeNewLineCodeLF2CRLF(String Source)
        {
            String Dest = Source.Replace("\n", "\r\n");
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
            String AppendPath = Path1;

            if (Path1.Substring(Path1.Length, 0) != "/")
            {
                if (Path2.Substring(0, 0) != "/")
                {
                    AppendPath += "/";
                }
            }
            AppendPath += Path2;

            return AppendPath;
        }

        // 並びをアソート
        public String[] AssortList(String[] Sources)
        {
            return Sources.OrderBy(i => Guid.NewGuid()).ToArray();
        }

        // LSF簡易版。接続可能なホストをランダムに選定
        public String GetLsfLightHostName(String[] HostNameList, String DefaultHostName = "")
        {
            String HostName = DefaultHostName;

            // ランダムに並べ替え
            HostNameList = AssortList(HostNameList);

            // 接続可能なホストを検索
            foreach (String CurrentHostName in HostNameList)
            {
                if (CurrentHostName == String.Empty)
                {
                    continue;
                }

                if (IsAliveHost(CurrentHostName))
                {
                    HostName = CurrentHostName;
                    break;
                }
            }
            return HostName;
        }

        // 特定の文字列を削除
        public String[] RemoveStringArray(String[] Sources, String Remove)
        {
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

        // IPアドレス取得
        public String GetIPAddress()
        {
            String hostname = Dns.GetHostName();
            IPAddress[] adrList = Dns.GetHostAddresses(hostname);

            return adrList[adrList.Length - 1].ToString();
        }

        public Boolean IsAliveHost(String HostName)
        {
            Boolean IsAlive = true;
            String output;
            ExecuteProcess(out output, "ping", "-n 1 " + HostName);

            // 該当の文字列があったら、切断されてる
            if (output.IndexOf("見つかりませんでした") != -1)
            {
                IsAlive = false;
            }
            return IsAlive;
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
            // TODO：条件文は外に出したい
            // 全選択
            if (e.KeyCode == Keys.A && e.Control == true)
            {
                SendKeys.SendWait("{HOME}+{END}");
            }
        }

        public String[] GetSubDirFileList(String TopDirPath, String SubDirPath, String TargetFile)
        {
            String[] FileList = { "" };

            TopDirPath += @"\";     // "\" もRemoveしたいので、TopDirPathにAddする
            String FullPath = TopDirPath + SubDirPath;

            if (Directory.Exists(FullPath))
            {
                FileList = Directory.GetFiles(FullPath, TargetFile, SearchOption.AllDirectories);
                FileList = RemoveStringArray(FileList, TopDirPath);
            }
            return FileList;
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
            System.Collections.ArrayList al = new System.Collections.ArrayList(SourceArray.Length);

            //基になる配列の要素を列挙する
            foreach (String i in SourceArray)
            {
                //コレクション内に存在していなければ、追加する
                if (!al.Contains(i))
                {
                    al.Add(i);
                }
            }

            //配列に変換する
            return (String[])al.ToArray(typeof(String));
        }

        // 全角文字を半角文字に変換
        //public String[] ChangeWide2Narrow(String[] StrArray)
        //{
        //    // Memo: 参照設定に「Microsoft.VisualBasic」が必要
        //    return StrArray.Select(str => Microsoft.VisualBasic.Strings.StrConv(str, Microsoft.VisualBasic.VbStrConv.Narrow)).ToArray();
        //}

        //// 半角文字を全角文字に変換
        //public String[] ChangeNarrow2Wide(String[] StrArray)
        //{
        //    // Memo: 参照設定に「Microsoft.VisualBasic」が必要
        //    return StrArray.Select(str => Microsoft.VisualBasic.Strings.StrConv(str, Microsoft.VisualBasic.VbStrConv.Wide)).ToArray();
        //}

        public String[] GetStringArray(ComboBox CbCtrl)
        {
            String[] CombBoxArray = CbCtrl.Items.Cast<String>().ToArray();
            return CombBoxArray;
        }

        public void SetComboBoxString(ref ComboBox CbCtrl, String[] StringArray)
        {
            // DataSourceを設定すると、以降ItemsAddができなくなる
            CbCtrl.DataSource = StringArray;

            CbCtrl.Items.Clear();
            CbCtrl.Items.AddRange(StringArray);
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
                // (例) C: のようにドライブレターだけが指定された場合は、終端に"\"が必要
                if (DestDirName.Length == 2)
                {
                    DestDirName += @":\";
                }
            }
            else
            {
                DestDirName = SrcDirName;
            }

            return DestDirName;
        }

        public String GetSelectName(ListBox ListBoxCtrl, String RootPath = "")
        {
            String TargetName = "";
            for (int i = 0; i < ListBoxCtrl.SelectedItems.Count; i++)
            {
                String AddName = "";
                if (RootPath != String.Empty)
                {
                    AddName = RootPath + @"\";
                }
                AddName += ListBoxCtrl.SelectedItems[i].ToString() + Environment.NewLine;

                TargetName += AddName;
            }

            return TargetName;
        }

        public String GetSelectListName(ListView ListViewCtrl, String RootPath = "", int index = 0)
        {
            String TargetName = "";
            for (int i = 0; i < ListViewCtrl.SelectedItems.Count; i++)
            {
                String AddName = "";
                if (RootPath != String.Empty)
                {
                    AddName = RootPath + @"\";
                }
                AddName += ListViewCtrl.SelectedItems[i].SubItems[index].Text + Environment.NewLine;

                TargetName += AddName;
            }

            return TargetName;
        }

        // リストボックスの選択項目をコピー
        public void CopyToClipboard(KeyEventArgs e, ListBox ListBoxCtrl, String RootPath = "")
        {
            // TODO：条件文は外に出したい
            // コピー
            if (e.KeyCode == Keys.C && e.Control == true)
            {
                String TargetName = GetSelectName(ListBoxCtrl, RootPath);
                Clipboard.SetText(TargetName);
            }
        }

        // リストコントロールの選択項目をコピー
        public void CopyToClipboard(KeyEventArgs e, ListView ListViewCtrl, String RootPath = "", int index = 0)
        {
            // TODO：条件文は外に出したい
            // コピー
            if (e.KeyCode == Keys.C && e.Control == true)
            {
                String TargetName = GetSelectListName(ListViewCtrl, RootPath, index);
	            if (!TargetName.Equals(String.Empty))
	            {
	                Clipboard.SetText(TargetName);
	            }
            }
        }

        // リストコントロールの中身をString配列で取得
        public String[] GetStrArrayFromListBox(ListBox.SelectedObjectCollection ListBoxSelected)
        {
            // TODO ListBoxの値を配列で取得したい
            String Str = "";
            foreach (object Item in ListBoxSelected)
            {
                Str = Str + Item.ToString() + Environment.NewLine;
            }

            char[] TrimChar = { '\r', '\n' };
            String[] StrArray = Str.TrimEnd(TrimChar).Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            return StrArray;
        }

        // 文字列をコンボボックスにセット
        public void SetComboBoxFromTextList(ref ComboBox ComboCtrl, String TextList, String RemoveString = "")
        {
            String[] Array = TextList.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            SetComboBoxFromArray(ComboCtrl, Array, RemoveString);
        }

        // コンボボックスの選択肢を文字列の配列で取得
        public String[] GetComboBoxList(ComboBox ComboCtrl)
        {
            String ComboBoxStr = "";
            for (int i = 0; i < ComboCtrl.Items.Count; i++)
            {
                ComboBoxStr += ComboCtrl.Items[i].ToString() + Environment.NewLine;
            }
            return ComboBoxStr.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
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

            // ファイルをリストアップ
            String[] files = Directory.GetFiles(DirectoryPath, FileExtension, SearchOption.AllDirectories);

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

        // リストビューの中から目的の文字列を探す
        public String FindStringFromListView(ListView LvCtrl, String SrcName, String TrimName = "")
        {
            String SerchName = SrcName;
            String DestName = "";

            // SrcTrimNameが設定されていたら、特定の文字列で区切る
            if (TrimName != String.Empty)
            {
                int FileNameidx = SrcName.IndexOf(TrimName);
                if (0 <= FileNameidx)
                {
                    SerchName = SrcName.Substring(0, FileNameidx);
                }
            }

            for (int i = 0; i < LvCtrl.Items.Count; i++)
            {
                String LvString = LvCtrl.Items[i].ToString();
                if (LvString.IndexOf(SerchName) != -1)
                {
                    DestName = LvString;
                    break;
                }
            }

            return DestName;
        }

        // ドロップされたファイルをコントロールにセット
        public Boolean DropFileNames(DragEventArgs e, TextBox TextCtrl, Boolean IsClear = false)
        {
            Boolean IsApply = false;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                String[] fileName = (String[])e.Data.GetData(DataFormats.FileDrop, false);

                if (IsClear)
                {
                    TextCtrl.Text = "";
                }

                for (int i = 0; i < fileName.Length; i++)
                {
                    TextCtrl.Text += fileName[i] + Environment.NewLine;
                }
                IsApply = true;
            }

            return IsApply;
        }

        public Boolean IsKeyPressNumber(KeyPressEventArgs e)
        {
            Boolean IsNumber = false;
            if (e.KeyChar < '0' || e.KeyChar > '9')
            {
                IsNumber = true;
            }
            return IsNumber;
        }

        public Boolean GetDataGridCellBool(DataGridView dgv, int RowCount, int ColumnCount)
        {
            Boolean IsTrue = false;
            String CellData = GetDataGridCell(dgv, RowCount, ColumnCount);
            if (CellData.Equals("True"))
            {
                IsTrue = true;
            }
            return IsTrue;
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
    }

    // *******************************************************************************
    // プロファイルの保存＆読み込み
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
        public void RegistCtrl(String AttrName, String AttrValue, TextBox Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegTextCtrl, RegTextCtrl.Length + 1);
            int AddIdx = RegTextCtrl.Length - 1;

            RegTextCtrl[AddIdx] = new TextCtrlDB();
            RegTextCtrl[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegTextCtrl[AddIdx].Ctrl = Ctrl;
        }

        public void RegistCtrl(String AttrName, String AttrValue, RadioButton Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegRadioCtrl, RegRadioCtrl.Length + 1);
            int AddIdx = RegRadioCtrl.Length - 1;

            RegRadioCtrl[AddIdx] = new RadioButtonCtrlDB();
            RegRadioCtrl[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegRadioCtrl[AddIdx].Ctrl = Ctrl;
        }

        public void RegistCtrl(String AttrName, String AttrValue, CheckBox Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegCheckCtrl, RegCheckCtrl.Length + 1);
            int AddIdx = RegCheckCtrl.Length - 1;

            RegCheckCtrl[AddIdx] = new CheckBoxDB();
            RegCheckCtrl[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegCheckCtrl[AddIdx].Ctrl = Ctrl;
        }

        public void RegistCtrl(String AttrName, String AttrValue, ComboBox Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegComboCtrl, RegComboCtrl.Length + 1);
            int AddIdx = RegComboCtrl.Length - 1;

            RegComboCtrl[AddIdx] = new ComboBoxDB();
            RegComboCtrl[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegComboCtrl[AddIdx].Ctrl = Ctrl;
        }

        public void RegistCtrl(String AttrName, String AttrValue, String AttrCountValue, DataGridView Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegDataGridCtrl, RegDataGridCtrl.Length + 1);
            int AddIdx = RegDataGridCtrl.Length - 1;

            RegDataGridCtrl[AddIdx] = new DataGridViewDB();
            RegDataGridCtrl[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegDataGridCtrl[AddIdx].Ctrl = Ctrl;
            RegDataGridCtrl[AddIdx].Ctrl.RowCount = 1;
            RegDataGridCtrl[AddIdx].AttrCountValue = AttrCountValue;
        }

        public void RegistCtrl(String AttrName, String AttrValue, HScrollBar Ctrl, int ElementValue = 0)
        {
            Array.Resize(ref RegHScrollBarCtrl, RegHScrollBarCtrl.Length + 1);
            int AddIdx = RegHScrollBarCtrl.Length - 1;

            RegHScrollBarCtrl[AddIdx] = new HScrollBarDB();
            RegHScrollBarCtrl[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue.ToString());
            RegHScrollBarCtrl[AddIdx].Ctrl = Ctrl;
        }

        public void RegistCtrlList(String AttrName, String AttrValue, ComboBox Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegComboCtrlList, RegComboCtrlList.Length + 1);
            int AddIdx = RegComboCtrlList.Length - 1;

            RegComboCtrlList[AddIdx] = new ComboBoxDB();
            RegComboCtrlList[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegComboCtrlList[AddIdx].Ctrl = Ctrl;
        }

        public void RegistCtrlList(String AttrName, String AttrValue, CheckedListBox Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegCheckedListBox, RegCheckedListBox.Length + 1);
            int AddIdx = RegCheckedListBox.Length - 1;

            RegCheckedListBox[AddIdx] = new CheckedListBoxDB();
            RegCheckedListBox[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegCheckedListBox[AddIdx].Ctrl = Ctrl;
        }

        public void RegistSecureCtrl(String AttrName, String AttrValue, TextBox Ctrl, String ElementValue = "")
        {
            Array.Resize(ref RegSecureCtrl, RegSecureCtrl.Length + 1);
            int AddIdx = RegSecureCtrl.Length - 1;

            RegSecureCtrl[AddIdx] = new SecureCtrlDB();
            RegSecureCtrl[AddIdx].SetDefaultParam(AttrName, AttrValue, ElementValue);
            RegSecureCtrl[AddIdx].Ctrl = Ctrl;

            RegSecureCtrl[AddIdx].DesKey = new List<byte>();
            RegSecureCtrl[AddIdx].DesIV = new List<byte>();
            RegSecureCtrl[AddIdx].cryptData = new List<byte>();
            RegSecureCtrl[AddIdx].SecureAttrName = DefaultSecureAttrName;
            RegSecureCtrl[AddIdx].SecureAttrValueDesKey = DefaultSecureAttrValueDesKey;
            RegSecureCtrl[AddIdx].SecureAttrValueDesIV = DefaultSecureAttrValueDesIV;
            RegSecureCtrl[AddIdx].SecureAttrValueCryptData = DefaultSecureAttrValueCryptData;
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

            // 管理情報は先に読む
            Boolean UseSecure = UseSecureCtrl();
            if (UseSecure)
            {
                LoadSecureCode(FileName);
            }

            // 設定値を読む
            XmlDocument document = new XmlDocument();
            document.Load(FileName);

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

        // コントロール読み込み[個別]
        public String LoadXmlFile(String FileName, String AttrName, String AttrValue, String DefaultElement = "")
        {
            String ElementValue = DefaultElement;
            if (!File.Exists(FileName))
            {
                return ElementValue;
            }

            XmlDocument document = new XmlDocument();
            document.Load(FileName);

            foreach (XmlElement element in document.DocumentElement)
            {
                String attribute = "";
                attribute = element.GetAttribute(AttrName);
                if (attribute != String.Empty)
                {
                    if (attribute.IndexOf(AttrValue) != -1)
                    {
                        ElementValue = element.InnerText;
                        break;
                    }
                }
            }
            return ElementValue;
        }

        // コントロール読み込み[個別&リスト]
        public String[] LoadXmlFileList(String FileName, String AttrName, String AttrValue)
        {
            String[] StringArray = new String[] { };
            if (!File.Exists(FileName))
            {
                return StringArray;
            }

            XmlDocument document = new XmlDocument();
            document.Load(FileName);

            foreach (XmlElement element in document.DocumentElement)
            {
                String text = element.InnerText;
                String attribute = element.GetAttribute(AttrName);
                if (attribute != String.Empty)
                {
                    if (attribute.IndexOf(AttrValue) != -1)
                    {
                        Array.Resize(ref StringArray, StringArray.Length + 1);
                        StringArray[StringArray.Length - 1] = text;
                    }
                }
            }
            return StringArray;
        }

        // 設定ファイル読み込み[TextCtrl]
        private Boolean LoadTextCtrl(XmlElement element, String ElementValue)
        {
            for (int i = 0; i < RegTextCtrl.Length; i++)
            {
                if (RegTextCtrl[i].IsExistFullMatch(element))
                {
                    RegTextCtrl[i].Ctrl.Text = ElementValue;
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[RadioButton]
        private Boolean LoadRadioCtrl(XmlElement element, String ElementValue)
        {
            for (int i = 0; i < RegRadioCtrl.Length; i++)
            {
                if (RegRadioCtrl[i].IsExistFullMatch(element))
                {
                    RegRadioCtrl[i].Ctrl.Checked = util.GetBoolean(ElementValue);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[CheckBox]
        private Boolean LoadCheckCtrl(XmlElement element, String ElementValue)
        {
            for (int i = 0; i < RegCheckCtrl.Length; i++)
            {
                if (RegCheckCtrl[i].IsExistFullMatch(element))
                {
                    RegCheckCtrl[i].Ctrl.Checked = util.GetBoolean(ElementValue);
                    return true;
                }
            }
            return false;
        }

        private Boolean LoadComboCtrl(XmlElement element, String ElementValue)
        {
            for (int i = 0; i < RegComboCtrl.Length; i++)
            {
                if (RegComboCtrl[i].IsExistFullMatch(element))
                {
                    RegComboCtrl[i].Ctrl.Text = ElementValue;
                    return true;
                }
            }
            return false;
        }

        private Boolean LoadComboCtrlList(XmlElement element, String ElementValue)
        {
            for (int i = 0; i < RegComboCtrlList.Length; i++)
            {
                if (RegComboCtrlList[i].IsExistPartMatch(element))
                {
                    RegComboCtrlList[i].Ctrl.Items.Add(ElementValue);
                    return true;
                }
            }
            return false;
        }

        private Boolean LoadCheckedListBoxCtrl(XmlElement element, String ElementValue)
        {
            for (int i = 0; i < RegCheckedListBox.Length; i++)
            {
                if (RegCheckedListBox[i].IsExistPartMatch(element, RegCheckedListBox[i].AttrName, RegCheckedListBox[i].AttrValue + "-"))
                {
                    String attribute = element.GetAttribute(RegCheckedListBox[i].AttrName);
                    int ItemNameIdx = RegCheckedListBox[i].AttrValue.Length + 1;

                    // 名前
                    RegCheckedListBox[i].Ctrl.Items.Add(attribute.Substring(ItemNameIdx));

                    // 状態
                    Boolean IsChecked = util.GetBoolean(ElementValue, "Checked");
                    RegCheckedListBox[i].Ctrl.SetItemChecked(RegCheckedListBox[i].Ctrl.Items.Count - 1, IsChecked);
                    return true;
                }
            }
            return false;
        }

        private Boolean LoadDataGridCtrl(XmlElement element, String ElementValue)
        {
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
            for (int i = 0; i < RegHScrollBarCtrl.Length; i++)
            {
                if (RegHScrollBarCtrl[i].IsExistFullMatch(element))
                {
                    RegHScrollBarCtrl[i].Ctrl.Value = int.Parse(ElementValue);
                    return true;
                }
            }
            return false;
        }

        // 設定ファイル読み込み[CheckedListBox]
        private void LoadCheckListCtrl(CheckedListBox CheckListCtrl, String Attribute, String ElementValue)
        {
            for (int i = 0; i < CheckListCtrl.Items.Count; i++)
            {
                if (CheckListCtrl.Items[i].ToString() == Attribute)
                {
                    Boolean IsChecked = util.GetBoolean(ElementValue, "Checked");
                    CheckListCtrl.SetItemChecked(i, IsChecked);
                    break;
                }
            }
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

        private void LoadSecureCode(String FileName)
        {
            XmlDocument document = new XmlDocument();
            document.Load(FileName);

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
            XmlDocument document = new XmlDocument();
            document.Load(FileName);
            return LoadXmlVersion(document);
        }

        // 設定ファイルの読み込み[バージョン]
        public int LoadXmlVersion(XmlDocument document)
        {
            int VersionNo = 0;
            foreach (XmlElement element in document.DocumentElement)
            {
                string attribute = element.GetAttribute(VersionAttrName);
                string text = element.InnerText;

                if (attribute.Equals(VersionKeyName))
                {
                    VersionNo = int.Parse(text);
                    break;
                }
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
                m_WriteDocument.Save(file_name);
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
        public void SaveXmlManageParam(String AttrName, String AttrValue, byte[] Value)
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
        private void SaveTextCtrl()
        {
            for (int i = 0; i < RegTextCtrl.Length; i++)
            {
                SaveXmlString(RegTextCtrl[i].AttrName, RegTextCtrl[i].AttrValue, RegTextCtrl[i].Ctrl.Text);
            }
        }

        // ファイル保存[RadioButton]
        private void SaveRadioCtrl()
        {
            for (int i = 0; i < RegRadioCtrl.Length; i++)
            {
                SaveXmlString(RegRadioCtrl[i].AttrName, RegRadioCtrl[i].AttrValue, RegRadioCtrl[i].Ctrl.Checked.ToString());
            }
        }

        // ファイル保存[CheckBox]
        private void SaveCheckCtrl()
        {
            for (int i = 0; i < RegCheckCtrl.Length; i++)
            {
                SaveXmlString(RegCheckCtrl[i].AttrName, RegCheckCtrl[i].AttrValue, RegCheckCtrl[i].Ctrl.Checked.ToString());
            }
        }

        // ファイル保存[ComboBox]
        private void SaveComboCtrl()
        {
            for (int i = 0; i < RegComboCtrl.Length; i++)
            {
                SaveXmlString(RegComboCtrl[i].AttrName, RegComboCtrl[i].AttrValue, RegComboCtrl[i].Ctrl.Text);
            }
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
                        String CellValue = "";
                        if (RegDataGridCtrl[i].Ctrl.Rows[RowCount].Cells[ColumnCount].Value != null)
                        {
                            CellValue = RegDataGridCtrl[i].Ctrl.Rows[RowCount].Cells[ColumnCount].Value.ToString();
                        }

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
            for (int i = 0; i < RegHScrollBarCtrl.Length; i++)
            {
                SaveXmlString(RegHScrollBarCtrl[i].AttrName, RegHScrollBarCtrl[i].AttrValue, RegHScrollBarCtrl[i].Ctrl.Value.ToString());
            }
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
    }

    // *******************************************************************************
    // セキュリティ
    public partial class StcSecure
    {
        // TODO：暗号キー＆複合キーを実装したい
        // 鍵
        private readonly byte[] DesKey = { 19, 205, 192, 75, 64, 178, 64, 178, 46, 129, 118, 109, 70, 30, 146, 231, 149, 124, 156, 41, 192, 20, 225, 204 };
        private readonly byte[] DesIv = { 137, 203, 34, 150, 244, 130, 250, 180 };

        // 暗号化
        public String Encode(String Word)
        {
            return Encryption(Word, true);
        }

        // 複合化
        public String Decode(String Word)
        {
            return Encryption(Word, false);
        }

        // 暗号化&複合化のメイン処理
        private String Encryption(String Word, Boolean IsEncode = true)
        {
            // 文字列をbyte配列に変換
            byte[] Source = Encoding.Unicode.GetBytes(Word);

            // Triple DESのサービスプロバイダを生成
            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();

            // 入出力用のストリームを生成
            MemoryStream ms = new MemoryStream();
            ICryptoTransform ict;
            if (IsEncode)
            {
                ict = des.CreateEncryptor(DesKey, DesIv);
            }
            else
            {
                ict = des.CreateDecryptor(DesKey, DesIv);
            }
            CryptoStream cs = new CryptoStream(ms, ict, CryptoStreamMode.Write);

            // ストリームに暗号化されたデータを書き込み
            cs.Write(Source, 0, Source.Length);
            cs.Close();

            // 復号化されたデータをbyte配列で取得
            byte[] cryptData = ms.ToArray();
            ms.Close();

            // byte配列を文字列に変換して表示
            return Encoding.Unicode.GetString(cryptData);
        }

        // 暗号化witch鍵
        public String Encode(String str, out byte[] DesKey, out byte[] DesIV, out byte[] cryptData)
        {
            TripleDESCryptoServiceProvider TDES = new TripleDESCryptoServiceProvider();
            DesKey = TDES.Key;
            DesIV = TDES.IV;

            // source 配列から cryptData 配列へ変換 
            // 文字列を byte 配列に変換します 
            byte[] source = Encoding.Unicode.GetBytes(str);

            // Triple DES のサービス プロバイダを生成します 
            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();

            // 入出力用のストリームを生成します 
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(DesKey, DesIV), CryptoStreamMode.Write);

            // ストリームに暗号化するデータを書き込みます 
            cs.Write(source, 0, source.Length);
            cs.Close();

            // 暗号化されたデータを byte 配列で取得します 
            cryptData = ms.ToArray();
            ms.Close();

            return Encoding.Unicode.GetString(cryptData);
        }

        // 複合化with鍵
        public String Decode(String str, byte[] DesKey, byte[] DesIV, byte[] cryptData)
        {
            // cryptData 配列から destination 配列へ変換 

            // Triple DES のサービス プロバイダを生成します 
            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();

            // 入出力用のストリームを生成します 
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, des.CreateDecryptor(DesKey, DesIV),
                                                                CryptoStreamMode.Write);

            // ストリームに暗号化されたデータを書き込みます 
            cs.Write(cryptData, 0, cryptData.Length);
            cs.Close();

            // 復号化されたデータを byte 配列で取得します 
            byte[] destination = ms.ToArray();
            ms.Close();

            // byte 配列を文字列に変換して表示します 
            return Encoding.Unicode.GetString(destination);
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
            StreamWriter sw = new StreamWriter(
                FileName,
                false,
                GetEncord(EncordType));

            // デバッグモードのときは、バッチの画面を閉じない
            if (DebugMode)
            {
                Data += @"PAUSE" + System.Environment.NewLine;
            }

            // 改行コードを変換
            Data = utils.ChangeNewLineCode(EncordType, Data);
            sw.Write(Data);
            sw.Close();
        }

        // 文字コード変換[UTF8→Sjis]
        public Boolean ChangeStringCodeUTF2SJIS(String InFileName, String OutFileName)
        {
            //Encoding src = Encoding.ASCII;
            Encoding src = Encoding.GetEncoding("utf-8");
            Encoding dest = Encoding.GetEncoding("Shift_JIS");
            
            String FileData = "";
            if (File.Exists(InFileName))
            {
                StreamReader sr = new StreamReader(InFileName, src);
                FileData = sr.ReadToEnd();
                sr.Close();
            }
            
            Byte[] temp = src.GetBytes(FileData);
            Byte[] sjis_temp = Encoding.Convert(src, dest, temp);
            String sjis_str = dest.GetString(sjis_temp);

            SaveFile(OutFileName, FileData);

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

            // TODO：本関数を汎用的かつシンプルにしたい
            Byte[] dat = new Byte[] { };
            Stream sr = null;
            Stream sw = null;
            BinaryReader br = null;
            BinaryWriter bw = null;

            // 入力ファイルをバイナリ形式で入力
            sr = File.Open(InFileName, FileMode.Open, FileAccess.Read);
            br = new BinaryReader(sr);
            Array.Resize<Byte>(ref dat, (int)sr.Length);
            dat = br.ReadBytes((int)sr.Length);
            br.Close();
            sr.Close();

            String uni;
            uni = System.Text.Encoding.GetEncoding("EUC-JP").GetString(dat);
            dat = System.Text.Encoding.GetEncoding("SHIFT-JIS").GetBytes(uni);

            sw = File.Open(OutFileName, FileMode.Create, FileAccess.Write);
            bw = new BinaryWriter(sw);
            bw.Write(dat);

            bw.Close();
            sw.Close();
            br.Close();
            sr.Close();

            return true;
        }

        // ファイルを更新できるかチェック
        public Boolean IsAuthorityOverwrite(String FilePath)
        {
            if (!utils.IsExistPath(FilePath))
            {
                // ファイルが存在しなければロックされていないので、更新できる
                return true;
            }

            try
            {
                FileStream fs = File.Open(FilePath, FileMode.Open, FileAccess.Read);
                fs.Close();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        // フォルダパス取得ダイアログ
        public String GetDirectoryPath(String Title = "フォルダを指定してください。", String DefaultPath = "", Boolean IsNewFolder = false)
        {
            String PathName = "";

            FolderBrowserDialog fd = new FolderBrowserDialog();

            fd.Description = Title;
            fd.RootFolder = Environment.SpecialFolder.Desktop;
            fd.ShowNewFolderButton = IsNewFolder;
            if (DefaultPath != String.Empty)
            {
                fd.SelectedPath = Path.GetDirectoryName(DefaultPath);
            }

            if (fd.ShowDialog() == DialogResult.OK)
            {
                PathName = fd.SelectedPath;
            }
            return PathName;
        }

        // 読み込みファイルを選択
        public String SelectLoadFileName(String FileName = "")
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = FileName;
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
        public String SelectSaveFileName(String FileName)
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
                if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    fi.Attributes = FileAttributes.Normal;
                }
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

            FileInfo fi = new FileInfo(DeleteFile);
            if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                fi.Attributes = FileAttributes.Normal;
            }

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

        // ディレクトリの中にディレクトリがあるか？
        public Boolean IsExistSubDirectory(String Path)
        {
            if (!Directory.Exists(Path))
            {
                // そもそもパスが無効
                return false;
            }

            // フォルダをリストアップ
            String[] files = Directory.GetDirectories(Path, "*", SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                return true;
            }
            return false;
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

        // ファイルコピー[移動できないことを考慮]
        public Boolean FileCopy(String SourcePath, String TargetPath)
        {
            Boolean IsSuccess = true;
            try
            {
                File.Copy(SourcePath, TargetPath);
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

        // ディレクトリが存在するか？
        public Boolean IsExistDirectory(String Path, Boolean IsAutoCreate = false)
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

                    if (result == DialogResult.Yes)
                    {
                        IsCreate = true;
                    }
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
            StreamWriter sw = new StreamWriter(FilePath, IsAppend, System.Text.Encoding.GetEncoding("Shift_JIS"));
            sw.Write(Data);
            sw.Close();
        }

        // TODO：削除する
        // ファイルデータを取得する
        public String GetFileData(String FilePath)
        {
            return LoadFile(FilePath);
        }

        // ファイルデータを取得する
        public String LoadFile(String FilePath)
        {
            String FileData = "";
            if (File.Exists(FilePath))
            {
                StreamReader sr = new StreamReader(FilePath, Encoding.GetEncoding("Shift_JIS"));
                FileData = sr.ReadToEnd();
                sr.Close();
            }
            return FileData;
        }

        public Boolean DetectFileData(String FilePath, String DetectWord)
        {
            Boolean IsFound = false;
            String Data = GetFileData(FilePath);

            if (Data.IndexOf(DetectWord) != -1)
            {
                IsFound = true;
            }

            return IsFound;
        }

        private Encoding GetEncord(ENCORD_TYPE EncordType)
        {
            String EncordStr = "";
            switch (EncordType)
            {
                case ENCORD_TYPE.SHIFT_JIS:
                    EncordStr = "shift_jis";
                    break;

                case ENCORD_TYPE.EUC_JP:
                    EncordStr = "euc-jp";
                    break;

                default:
                    break;
            }

            return Encoding.GetEncoding(EncordStr);
        }

        private String GetHtmlSource(String Url)
        {
            String HtmlSource = "";
            WebClient client = new WebClient();
            try
            {
                client.Encoding = System.Text.Encoding.UTF8;
                HtmlSource = client.DownloadString(Url);
            }
            catch (Exception)
            {
                MessageBox.Show("ソース取得に失敗しました。" + Environment.NewLine + Url);
            }

            StcUtils util = new StcUtils();
            return util.ChangeNewLineCodeLF2CRLF(HtmlSource);
        }
    }

    // *******************************************************************************
    // ImgFileIO
    public partial class StcImage
    {
        public Size GetImageSize(String FilePath)
        {
            Size sz = new Size();

            if (File.Exists(FilePath))
            {
                Bitmap canvas = new Bitmap(FilePath);

                sz.Width = canvas.Width;
                sz.Height = canvas.Height;

                ReleaseImg(ref canvas);
            }

            return sz;
        }

        private void ReleaseImg(ref Bitmap img)
        {
            if (img != null)
            {
                img.Dispose();
                img = null;
            }
        }
    }

    // *******************************************************************************
    // ShellScript
    class StcCreateScript
    {
        private StcUtils util = new StcUtils();

        // スクリプト生成[ヘッダ]
        public String CommonHeader()
        {
            String Script = @"#!/bin/sh" + System.Environment.NewLine +
                             System.Environment.NewLine;
            return Script;
        }

        public String LocalScriptShell(String Shell, String Param = "", Boolean IsBackGround = false)
        {
            String Script = Shell + " " + Param;

            if (IsBackGround)
            {
                Script += " &";
            }
            Script += System.Environment.NewLine;

            return Script;
        }

        public String LocalScriptSedDelete(String Target, String FileName)
        {
            String Script = "sed -i.bak '/" + util.ChangeLinuxPath2WindowsPath(Target) + "/d' " + FileName + System.Environment.NewLine;
            return Script;
        }

        public String LocalScriptSedInsert(String Target, String Add, String FileName)
        {
            String Script = "sed -i.bak '/" + util.ChangeLinuxPath2WindowsPath(Target) + "/i " + util.ChangeLinuxPath2WindowsPath(Add) + "' " + FileName + System.Environment.NewLine;
            return Script;
        }

        public String LocalScriptSedReplace(String Src, String Dest, String FileName)
        {
            String Script = "sed -i.bak 's|" + util.ChangeLinuxPath2WindowsPath(Src) + "|" + util.ChangeLinuxPath2WindowsPath(Dest) + "|g' " + FileName + System.Environment.NewLine;
            return Script;
        }

        public String LocalScriptSedReplaceLine(String Src, String Dest, String FileName)
        {
            String Script = "sed -i.bak 's|" + util.ChangeLinuxPath2WindowsPath(Src) + ".*|" + util.ChangeLinuxPath2WindowsPath(Dest) + "|g' " + FileName + System.Environment.NewLine;
            return Script;
        }

        public String LocalScriptSedReplaceTargetLine(String Target, String Src, String Dest, String FileName)
        {
            String Script = "sed -i.bak '/" + util.ChangeLinuxPath2WindowsPath(Target) + "/s|" + util.ChangeLinuxPath2WindowsPath(Src) + "|" + util.ChangeLinuxPath2WindowsPath(Dest) + "|g' " + FileName + System.Environment.NewLine;
            return Script;
        }

        public String LocalScriptDiff(String SrcFile, String DestFile)
        {
            String Script = "diff " + SrcFile + " " + DestFile + System.Environment.NewLine;
            return Script;
        }

        public String LocalScriptCp(String Src, String Dest)
        {
            String Ext = System.IO.Path.GetExtension(Src);
            if (Ext == String.Empty)
            {
                // コピー元がフォルダの場合、フォルダの中身をコピーする
                Src += "/*";
            }
            String Script = "cp -rp " + Src + " " + Dest + "/." + System.Environment.NewLine;
            return Script;
        }

        // Grep
        public String GetGrepWord(String GrepWord, String FileName, String OutputLine)
        {
            String OutFileName = Path.GetDirectoryName(FileName) + @"\";
            OutFileName += Path.GetFileNameWithoutExtension(FileName);
            OutFileName += "_" + GrepWord;
            OutFileName += Path.GetExtension(FileName);

            String Script = @"grep -A " + OutputLine + @" """ + GrepWord + @": ""  """ + FileName + @"""  > """ + OutFileName + @"""" + System.Environment.NewLine;
            return Script;
        }

        // ファイルがカラでないときにコマンド実行
        public String GetExecCmdWithExistFile(String FileName, String CommandName = "")
        {
            String Script = "";
            String CmdName = "start";

            if (CommandName != String.Empty)
            {
                CmdName = CommandName;
            }

            Script += "for %%i in (" + FileName + ") do set SIZE=%%~zi" + System.Environment.NewLine;
            Script += "if %SIZE% neq 0 ( " + CmdName + " " + FileName + " )" + System.Environment.NewLine;

            return Script;
        }
    }

    // *******************************************************************************
    // Sleepタイマー
    class StcSleep
    {
        private TimeSpan SleepTimeMsec = TimeSpan.FromMilliseconds(0);      // Sleepする時間
        private TimeSpan SleepCycleMsec = TimeSpan.FromMilliseconds(1000);  // Sleepを刻む時間
        private Boolean IsStopRequest = false;  // 停止要求

        public void SetSleepTimeMsec(uint msec)
        {
            SleepTimeMsec = TimeSpan.FromMilliseconds(msec);
        }

        public void SetSleepTimeSec(uint sec)
        {
            SleepTimeMsec = TimeSpan.FromSeconds(sec);
        }

        public void SetSleepCycleMsec(uint msec)
        {
            SleepCycleMsec = TimeSpan.FromMilliseconds(msec);
        }

        public Boolean StartSleep()
        {
            Boolean IsSuccess = true;
            for (TimeSpan Timer = TimeSpan.FromMilliseconds(0); Timer < SleepTimeMsec; Timer += SleepCycleMsec)
            {
                Thread.Sleep(SleepCycleMsec);
                if (IsStopRequest)
                {
                    // 途中キャンセル
                    IsSuccess = false;
                    break;
                }
            }
            return IsSuccess;
        }

        public void StopSleep()
        {
            IsStopRequest = true;
        }
    }

    // TODO：機能ごとにバラしておくと汎用的
    // *******************************************************************************
    // キャプチャ
    public class CaptWindow
    {
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

        // TODO：パラメータの意味が分かり難い
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

        private String ErrorLog;
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
                    IsSucess = SaveWithCaptureFullScreen(PictFileName);
                    break;
                case CAPTURE_TARGET.CURRENT_SCREEN:
                    IsSucess = SaveWithCaptureCurrentScreen(PictFileName);
                    break;
                case CAPTURE_TARGET.CURRENT_WINDOW:
                    IsSucess = SaveWithCaptureCurrentWindow(PictFileName);
                    break;
                default:
                    break;
            }

            return IsSucess;
        }

        private Boolean SaveWithCaptureFullScreen(String PictFileName)
        {
            // 全画面
            SendKeys.SendWait("^{PRTSC}");

            return SaveClipboard(PictFileName);
        }

        private Boolean SaveWithCaptureCurrentWindow(String PictFileName)
        {
            // Current Windowのみ
            SendKeys.SendWait("%{PRTSC}");

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
                img.Dispose();
            }

            return IsSucess;
        }

        private Boolean SaveWithCaptureCurrentScreen(String PictFileName)
        {
            //Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
            //                        Screen.PrimaryScreen.Bounds.Height);

            //TODO：画面配置によっては期待動作しない
            int Width = 0;
            int Height = 0;
            int X = 0;
            int Y = 0;

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                Width += System.Math.Abs(Screen.AllScreens[i].Bounds.Width);
                Height += System.Math.Abs(Screen.AllScreens[i].Bounds.Height);

                if (X > Screen.AllScreens[i].Bounds.X)
                {
                    X = Screen.AllScreens[i].Bounds.X;
                }

                if (Y > Screen.AllScreens[i].Bounds.Y)
                {
                    Y = Screen.AllScreens[i].Bounds.Y;
                }
            }
            Bitmap bmp = new Bitmap(Width, Height);

            //Graphicsの作成
            using (Graphics g = Graphics.FromImage(bmp))
            {
                //画面全体をコピーする
                //g.CopyFromScreen(new Point(0, 0), new Point(0, 0), bmp.Size);
                g.CopyFromScreen(new Point(X, Y), new Point(X, Y), bmp.Size);

                //解放
                g.Dispose();
            }

            // ファイル保存
            bmp.Save(PictFileName);
            bmp.Dispose();

            return true;    // TODO：エラー判定いれる？
        }

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
          System.Runtime.Serialization.StreamingContext context) { }
    }

    // *******************************************************************************
    // Draw
    public partial class StcDraw
    {
        public Bitmap canvas { get; private set; }

        private Graphics g;
        private PictureBox pb;

        private Brush PenColor = Brushes.Black;
        private int PenWidth = 1;

        // T.B.D：未使用
        private double coef = 1.0;  // PictureBox内に収まるようにスケーリング

        StcDraw(PictureBox pict_box)
        {
            pb = pict_box;
            canvas = new Bitmap(pb.Width, pb.Height);

            //ImageオブジェクトのGraphicsオブジェクトを生成
            g = Graphics.FromImage(canvas);
        }

        ~StcDraw()
        {
            g.Dispose();
        }

        public void SetPenColor(Brush color)
        {
            PenColor = color;
        }

        public void SetPenWidth(int width)
        {
            PenWidth = width;
        }

        // 背景塗り潰し
        public void FillBackground(Brush color)
        {
            g.FillRectangle(color, 0, 0, pb.Width, pb.Height);
        }

        // PictureBox内に収まるようにスケーリングを調整
        public void AdjustScale(Size ImgSize)
        {
            double ScaleWidth = 1.0;
            if (ImgSize.Width != 0)
            {
                ScaleWidth = (double)pb.Width / (double)ImgSize.Width;
            }

            double ScaleHeight = 1.0;
            if (ImgSize.Height != 0)
            {
                ScaleHeight = (double)pb.Height / (double)ImgSize.Height;
            }

            coef = Math.Min(ScaleWidth, ScaleHeight);
        }

        // 境界線を描画
        public void DrawRectBorderLine(int x, int y, int width, int height)
        {
            Pen pen = new Pen(PenColor, PenWidth);

            g.DrawRectangle(pen,
                x,
                y,
                width,
                height);

            pen.Dispose();
        }

        // 矩形塗り潰し
        public void DrawFillRect(Brush color, int x, int y, int width, int height)
        {
            g.FillRectangle(color,
                x,
                y,
                width,
                height);
        }

        public void DrawFillRectWidthBorderLine(Brush color, int x, int y, int width, int height)
        {
            // 塗り潰し
            g.FillRectangle(color,
                x,
                y,
                width,
                height);

            // 境界線
            DrawRectBorderLine(
                x,
                y,
                width,
                height);
        }
    }

    // *******************************************************************************
    // Check
    public class StcCheck
    {
        // Cygwinのフォルダパス
        private readonly String DefaultCygwinPath = @"C:\Cygwin";

        // CygwinのBinが使えるかチェックするファイル
        private readonly String CheckCygwinBinName = "sh.exe";

        // リモート処理に必要なファイルの一部
        private readonly String[] NeedRemoteScriptFile = {
            @"\bin\expect.exe",
            @"\lib\tcl8.5\init.tcl"};

        private StcUtils util = new StcUtils();

        // CygwinのBinが使えるかチェック
        public Boolean IsExistCygwinDir(String CygwinDir = "")
        {
            if (CygwinDir == String.Empty)
            {
                CygwinDir = DefaultCygwinPath;
            }
            return Directory.Exists(CygwinDir);
        }

        // CygwinのBinが使えるかチェック
        public Boolean IsPossibleCygwinBin(String BinName = "")
        {
            if (BinName == String.Empty)
            {
                BinName = CheckCygwinBinName;
            }
            return util.IsExistFileNameInEnvironment(BinName);
        }

        // リモート処理が可能かチェック
        public Boolean IsExistRemoteProcFile(String BaseDir = "")
        {
            return util.IsExistFiles(NeedRemoteScriptFile, BaseDir);
        }
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
                StcFileInputOutput fio = new StcFileInputOutput();
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

            StcFileInputOutput fio = new StcFileInputOutput();
            fio.SaveFile(FileName, Data);

            FileIndex++;
        }
    }
}
