using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// キャラクタライゼーションテスト（安全網その 2）。
    ///
    /// 純粋な計算をするメソッドに、境界値をひととおり流し込んで、
    /// 実際に返ってきた値をまるごとスナップショットに焼き付ける。
    /// 期待値を1件ずつ手で書くより網羅しやすく、書き間違いで嘘の期待値を固定する事故も起きない。
    ///
    /// 対象にしていないもの:
    ///   - プロセス起動 / ネットワーク / 画面キャプチャ（環境に依存して結果が変わる）
    ///   - WinForms コントロール操作（別途書く）
    ///   - AssortList など乱数を使うもの（毎回変わるので別テストで性質だけ確認する）
    /// </summary>
    [TestClass]
    public class BehaviorSnapshotTests
    {
        [TestMethod]
        public void 実挙動が承認済みスナップショットと一致すること()
        {
            var r = new BehaviorRecorder();

            RecordNumberParsing(r);
            RecordPathConversion(r);
            RecordNewLineAndEscape(r);
            RecordStringAndArray(r);
            RecordPathNamePicking(r);
            RecordSecure(r);

            SnapshotFile.Verify(r.Build(), "BehaviorSnapshot",
                "共通クラスの実挙動が変わっている。リファクタで壊した可能性がある。");
        }

        // ------------------------------------------------------------------
        // 数値・真偽値のパース
        // ------------------------------------------------------------------
        private static void RecordNumberParsing(BehaviorRecorder r)
        {
            var util = new StcUtils();

            r.Section("StcUtils.GetInteger(String)");
            foreach (string s in new[] { "", "0", "42", "-7", "042", " 42 ", "42abc", "abc", "2147483648" })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.GetInteger(t));
            }
            r.Case("null", () => util.GetInteger(null));

            r.Section("StcUtils.GetBoolean(String Text, String DetectWord)");
            r.Note("完全一致でしか true にならない。大文字小文字も区別する。");
            var boolCases = new[]
            {
                new[] { "", "True" },
                new[] { "True", "True" },
                new[] { "true", "True" },
                new[] { "False", "True" },
                new[] { "Checked", "Checked" },
                new[] { "UnChecked", "Checked" },
                new[] { "x", "True" },
            };
            foreach (string[] c in boolCases)
            {
                string text = c[0], word = c[1];
                r.Case(BehaviorRecorder.Show(text) + ", " + BehaviorRecorder.Show(word),
                       () => util.GetBoolean(text, word));
            }

            r.Section("StcUtils.GetNumber(String SrcString, int DefaultDigitNum)");
            r.Note("空文字のときだけ DefaultDigitNum がそのまま返る（桁数ではなく値として）。");
            var numCases = new object[][]
            {
                new object[] { "", 1 },
                new object[] { "", 5 },
                new object[] { "42", 1 },
                new object[] { "-7", 1 },
                new object[] { "007", 1 },
                new object[] { "abc", 1 },
                new object[] { " 7", 1 },
            };
            foreach (object[] c in numCases)
            {
                string src = (string)c[0];
                int digit = (int)c[1];
                r.Case(BehaviorRecorder.Show(src) + ", " + digit, () => util.GetNumber(src, digit));
            }

            r.Section("StcUtils.GetNumber(String SrcString, int StartIdx, int Length, String DefaultString)");
            r.Note("StartIdx が範囲外なら終端に丸める。Length が 0 か範囲外なら残り全部にする。");
            r.Note("切り出した中の数字以外を捨て、空になったら DefaultString、そうでなければ DefaultString の桁数まで 0 埋め。");
            var num4Cases = new object[][]
            {
                new object[] { "abc123", 0, 6, "01" },
                new object[] { "abc123", 3, 3, "01" },
                new object[] { "abc123", 0, 0, "01" },
                new object[] { "abc123", 99, 3, "01" },
                new object[] { "abc123", 2, 99, "01" },
                new object[] { "abc", 0, 3, "01" },
                new object[] { "abc", 0, 3, "007" },
                new object[] { "a1b2", 0, 4, "01" },
                new object[] { "a1", 0, 2, "0001" },
                new object[] { "a0001b", 0, 6, "01" },
                new object[] { "", 0, 0, "01" },
                new object[] { "abc123", -1, 3, "01" },
            };
            foreach (object[] c in num4Cases)
            {
                string src = (string)c[0];
                int start = (int)c[1], len = (int)c[2];
                string def = (string)c[3];
                r.Case(string.Format("{0}, {1}, {2}, {3}",
                        BehaviorRecorder.Show(src), start, len, BehaviorRecorder.Show(def)),
                       () => util.GetNumber(src, start, len, def));
            }

            r.Section("StcUtils.GetNumberFromRear(String SrcString, int EndIdx, int Length, String DefaultString)");
            r.Note("StartIdx = 文字数 - EndIdx として計算する。EndIdx が文字数より大きいと負になる。");
            foreach (object[] c in new object[][]
            {
                new object[] { "abc123", 3, 3, "01" },
                new object[] { "abc123", 6, 3, "01" },
                new object[] { "abc123", 0, 3, "01" },
                new object[] { "abc123", 99, 3, "01" },
            })
            {
                string src = (string)c[0];
                int end = (int)c[1], len = (int)c[2];
                string def = (string)c[3];
                r.Case(string.Format("{0}, {1}, {2}, {3}",
                        BehaviorRecorder.Show(src), end, len, BehaviorRecorder.Show(def)),
                       () => util.GetNumberFromRear(src, end, len, def));
            }

            r.Section("StcUtils.GetNumberFromRear(String SrcString, String EndIdxString, String LengthString, String DefaultString)");
            foreach (string[] c in new[]
            {
                new[] { "abc123", "3", "3", "01" },
                new[] { "abc123", "", "", "01" },
                new[] { "abc123", "99", "3", "01" },
                new[] { "abc123", "abc", "3", "01" },
            })
            {
                string src = c[0], end = c[1], len = c[2], def = c[3];
                r.Case(string.Format("{0}, {1}, {2}, {3}",
                        BehaviorRecorder.Show(src), BehaviorRecorder.Show(end),
                        BehaviorRecorder.Show(len), BehaviorRecorder.Show(def)),
                       () => util.GetNumberFromRear(src, end, len, def));
            }
        }

        // ------------------------------------------------------------------
        // パス変換
        // ------------------------------------------------------------------
        private static void RecordPathConversion(BehaviorRecorder r)
        {
            var util = new StcUtils();

            r.Section("StcUtils.GetFilePathType(String)");
            foreach (string s in new[]
            {
                @"C:\work\a.txt", "//depot/main/a.txt", "home/ogu/a.txt", @"work\a.txt",
                "a.txt", "", @"\\server\share", "C:/work", "//"
            })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.GetFilePathType(t));
            }

            r.Section("StcUtils.IsWindowsPath(String)");
            r.Note("区切りが \\ かどうかだけを見る。ドライブレターの有無は見ていない。");
            foreach (string s in new[] { @"C:\work", "C:/work", "work/a", "abc", "" })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.IsWindowsPath(t));
            }

            r.Section("StcUtils.ChangeWindowsPath2CygwinPath(String)");
            foreach (string s in new[]
            {
                @"C:\work\a.txt", @"D:\", @"work\a.txt", "C:/work", "/home/ogu", ""
            })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.ChangeWindowsPath2CygwinPath(t));
            }

            r.Section("StcUtils.ChangeCygwinPath2WindowsPath(String)");
            foreach (string s in new[]
            {
                "/cygdrive/c/work/a.txt", "/cygdrive/c", @"C:\work", "/home/ogu", ""
            })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.ChangeCygwinPath2WindowsPath(t));
            }

            r.Section("StcUtils.ChangeWindowsPath2LinuxPath(String) / ChangeLinuxPath2WindowsPath(String)");
            foreach (string s in new[] { @"C:\work\a.txt", "C:/work/a.txt", "abc", "" })
            {
                string t = s;
                r.Case("W2L " + BehaviorRecorder.Show(t), () => util.ChangeWindowsPath2LinuxPath(t));
                r.Case("L2W " + BehaviorRecorder.Show(t), () => util.ChangeLinuxPath2WindowsPath(t));
            }

            r.Section("StcUtils.ChangeWindowsPath2LinuxPath(String[]) / ChangeLinuxPath2WindowsPath(String[])");
            string[] arr = { @"C:\a", @"b\c", "d/e" };
            r.Case("W2L [C:\\a, b\\c, d/e]", () => util.ChangeWindowsPath2LinuxPath(arr));
            r.Case("L2W [C:\\a, b\\c, d/e]", () => util.ChangeLinuxPath2WindowsPath(arr));
            r.Case("W2L 空配列", () => util.ChangeWindowsPath2LinuxPath(new string[0]));

            r.Section("StcUtils.AppendLinuxPathName(String Path1, String Path2)");
            foreach (string[] c in new[]
            {
                new[] { "a", "b" },
                new[] { "a/", "b" },
                new[] { "a", "/b" },
                new[] { "a/", "/b" },
                new[] { "/home/ogu", "work" },
                new[] { "", "" },
            })
            {
                string p1 = c[0], p2 = c[1];
                r.Case(BehaviorRecorder.Show(p1) + ", " + BehaviorRecorder.Show(p2),
                       () => util.AppendLinuxPathName(p1, p2));
            }
        }

        // ------------------------------------------------------------------
        // 改行コードとエスケープ
        // ------------------------------------------------------------------
        private static void RecordNewLineAndEscape(BehaviorRecorder r)
        {
            var util = new StcUtils();

            r.Section("StcUtils.ChangeNewLineCodeLF2CRLF(String) / ChangeNewLineCodeCRLF2LF(String)");
            foreach (string s in new[] { "a\nb", "a\r\nb", "a\rb", "a\n\nb", "abc", "" })
            {
                string t = s;
                r.Case("LF2CRLF " + BehaviorRecorder.Show(t), () => util.ChangeNewLineCodeLF2CRLF(t));
                r.Case("CRLF2LF " + BehaviorRecorder.Show(t), () => util.ChangeNewLineCodeCRLF2LF(t));
            }

            r.Section("StcUtils.ChangeNewLineCode(ENCORD_TYPE, String)");
            r.Note("いったん LF に統一してから、SHIFT_JIS のときだけ CRLF に戻す。");
            foreach (StcFileInputOutput.ENCORD_TYPE t in
                     (StcFileInputOutput.ENCORD_TYPE[])Enum.GetValues(typeof(StcFileInputOutput.ENCORD_TYPE)))
            {
                StcFileInputOutput.ENCORD_TYPE type = t;
                foreach (string s in new[] { "a\r\nb", "a\nb", "a\rb" })
                {
                    string src = s;
                    r.Case(type + ", " + BehaviorRecorder.Show(src), () => util.ChangeNewLineCode(type, src));
                }
            }

            r.Section("StcUtils.ChangeDoubleQuote2BackSlashDoubleQuote(String)");
            foreach (string s in new[] { "a\"b", "\"\"", "abc", "" })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.ChangeDoubleQuote2BackSlashDoubleQuote(t));
            }
        }

        // ------------------------------------------------------------------
        // 文字列・配列の加工
        // ------------------------------------------------------------------
        private static void RecordStringAndArray(BehaviorRecorder r)
        {
            var util = new StcUtils();

            r.Section("StcUtils.RemoveStringArray(String[] Sources, String Remove)");
            r.Case("[abc, bcd, xyz], \"b\"", () => util.RemoveStringArray(new[] { "abc", "bcd", "xyz" }, "b"));
            r.Case("[abc], \"\"", () => util.RemoveStringArray(new[] { "abc" }, ""));
            r.Case("空配列, \"b\"", () => util.RemoveStringArray(new string[0], "b"));

            r.Section("StcUtils.Array2List(String[]) / List2Array(List<String>)");
            r.Case("Array2List [a, b]", () => util.Array2List(new[] { "a", "b" }));
            r.Case("Array2List 空配列", () => util.Array2List(new string[0]));
            r.Case("List2Array [a, b]", () => util.List2Array(new List<string> { "a", "b" }));

            r.Section("StcUtils.ChangeStrArray2Linear(String[] StrArray, String Suffix)");
            r.Note("null を渡しても落ちずに空文字を返す（数少ない null 対応済みメソッド）。");
            r.Case("[a, b, c], \",\"", () => util.ChangeStrArray2Linear(new[] { "a", "b", "c" }, ","));
            r.Case("[a, b], \"\"", () => util.ChangeStrArray2Linear(new[] { "a", "b" }, ""));
            r.Case("[a], \",\"", () => util.ChangeStrArray2Linear(new[] { "a" }, ","));
            r.Case("空配列, \",\"", () => util.ChangeStrArray2Linear(new string[0], ","));
            r.Case("null, \",\"", () => util.ChangeStrArray2Linear(null, ","));

            r.Section("StcUtils.ChangeStrLinear2Array(String StrLinear, String Delimiter, StringSplitOptions Opt)");
            r.Note("既定は RemoveEmptyEntries なので、区切りが連続すると空要素が消える。");
            r.Case("\"a,b,,c\", \",\" (既定)", () => util.ChangeStrLinear2Array("a,b,,c", ","));
            r.Case("\"a,b,,c\", \",\" (None)", () => util.ChangeStrLinear2Array("a,b,,c", ",", StringSplitOptions.None));
            r.Case("\"\", \",\" (既定)", () => util.ChangeStrLinear2Array("", ","));
            r.Case("\"abc\", \",\" (既定)", () => util.ChangeStrLinear2Array("abc", ","));

            r.Section("StcUtils.TrimDuplication(String[])");
            r.Case("[a, b, a, c, b]", () => util.TrimDuplication(new[] { "a", "b", "a", "c", "b" }));
            r.Case("[a, A]", () => util.TrimDuplication(new[] { "a", "A" }));
            r.Case("[\"\", \"\"]", () => util.TrimDuplication(new[] { "", "" }));
            r.Case("空配列", () => util.TrimDuplication(new string[0]));

            r.Section("StcUtils.TrimDuplication(String Source, String Delimiter)");
            r.Case("\"a,b,a,c\", \",\"", () => util.TrimDuplication("a,b,a,c", ","));
            r.Case("\"a,,b\", \",\"", () => util.TrimDuplication("a,,b", ","));
            r.Case("\"\", \",\"", () => util.TrimDuplication("", ","));

            r.Section("StcUtils.TrimEndGarbage(String)");
            r.Note("末尾の \\ / CR LF をまとめて落とす。先頭や途中には手を付けない。");
            foreach (string s in new[] { @"C:\work\", "C:/work/", "a\r\n", "a\\/\r\n", @"\a", "abc", "" })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.TrimEndGarbage(t));
            }

            r.Section("StcUtils.AdjustDirectoryName(String)");
            r.Note("\":\" を含むときだけ終端の \\ を落とす。含まないときは入力をそのまま返す。");
            foreach (string s in new[] { @"C:\work\", @"C:\work", @"C:\", "C:", @"work\", "work", "" })
            {
                string t = s;
                r.Case(BehaviorRecorder.Show(t), () => util.AdjustDirectoryName(t));
            }
        }

        // ------------------------------------------------------------------
        // パス名の切り出し（StcFileInputOutput）
        // ------------------------------------------------------------------
        private static void RecordPathNamePicking(BehaviorRecorder r)
        {
            var fio = new StcFileInputOutput();

            r.Section("StcFileInputOutput.GetFirstPathName(String) / GetLastPathName(String)");
            r.Note("\\ で分割するだけ。/ 区切りのパスには反応しない。");
            foreach (string s in new[] { @"C:\work\sub\a.txt", @"work\a.txt", "a.txt", "", @"\a", @"a\" })
            {
                string t = s;
                r.Case("First " + BehaviorRecorder.Show(t), () => fio.GetFirstPathName(t));
                r.Case("Last  " + BehaviorRecorder.Show(t), () => fio.GetLastPathName(t));
            }
        }

        // ------------------------------------------------------------------
        // 暗号（鍵が固定なので結果は決まる）
        // ------------------------------------------------------------------
        private static void RecordSecure(BehaviorRecorder r)
        {
            var secure = new StcSecure();

            r.Section("StcSecure.Encode(String) / Decode(String)");
            r.Note("鍵と IV がソースに直書きされているため、同じ入力なら常に同じ結果になる。");
            foreach (string s in new[] { "hello", "パスワード", "", "0123456789" })
            {
                string t = s;
                r.Case("Encode " + BehaviorRecorder.Show(t), () => secure.Encode(t));
            }
            foreach (string s in new[] { "hello", "パスワード", "0123456789" })
            {
                string t = s;
                r.Case("往復 " + BehaviorRecorder.Show(t), () => secure.Decode(secure.Encode(t)));
            }
        }
    }
}
