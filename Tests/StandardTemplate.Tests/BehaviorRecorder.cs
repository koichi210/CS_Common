using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// 「この入力を渡したら、実際にこう返ってきた」を淡々と記録していく道具。
    ///
    /// 期待値は書かない。書いたところで今の実装が返す値がそのまま正解になるだけなので、
    /// それなら実測値を丸ごと焼き付けて、変化したときに差分で気づけるようにする方がよい。
    /// 例外が飛んだ場合もそれが現在の挙動なので、型名を記録して固定する
    /// （メッセージは環境の言語設定で変わるため記録しない）。
    /// </summary>
    internal sealed class BehaviorRecorder
    {
        private readonly StringBuilder sb = new StringBuilder();
        private readonly List<KeyValuePair<string, string>> pending = new List<KeyValuePair<string, string>>();
        private int caseCount;
        private int sectionCount;

        public BehaviorRecorder()
        {
            sb.AppendLine("# StandardTemplate 実挙動スナップショット");
            sb.AppendLine("# このファイルは自動生成。手で編集しないこと。");
            sb.AppendLine("# ここに書かれているのは「正しい仕様」ではなく「今の実装が実際に返す値」。");
            sb.AppendLine("# 差分が出た = リファクタで挙動が変わった、という意味。");
            sb.AppendLine("# 表示の約束: 文字列は \" \" で囲む。改行は \\n、タブは \\t と表示する。");
            sb.AppendLine("#             区切り文字の \\ は読みやすさのためエスケープせずそのまま出す。");
            sb.AppendLine("#             例外が飛んだ場合は !! に続けて例外の型名を書く。");
        }

        /// <summary>対象メソッドごとの見出し。</summary>
        public void Section(string signature)
        {
            Flush();
            sb.AppendLine();
            sb.AppendLine("## " + signature);
            sectionCount++;
        }

        /// <summary>見出しに補足を添える（挙動の説明や注意書き）。</summary>
        public void Note(string text)
        {
            Flush();
            sb.AppendLine("   # " + text);
        }

        /// <summary>1 件ぶんの入力と、それを渡した結果を記録する。</summary>
        public void Case(string inputLabel, Func<object> action)
        {
            string result;
            try
            {
                result = Show(action());
            }
            catch (Exception ex)
            {
                result = "!! " + ex.GetType().Name;
            }
            pending.Add(new KeyValuePair<string, string>(inputLabel, result));
            caseCount++;
        }

        public string Build()
        {
            Flush();
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "# total: {0} sections / {1} cases", sectionCount, caseCount));
            return sb.ToString();
        }

        /// <summary>溜めておいた行を、入力の幅を揃えてから書き出す（読みやすさのため）。</summary>
        private void Flush()
        {
            if (pending.Count == 0) return;

            int width = pending.Max(p => p.Key.Length);
            if (width > 52) width = 52;   // 極端に長い入力があるときは整列を諦める

            foreach (KeyValuePair<string, string> p in pending)
            {
                sb.Append("  ").Append(p.Key.PadRight(width)).Append("  ->  ").AppendLine(p.Value);
            }
            pending.Clear();
        }

        /// <summary>値を、目で見て分かる形の文字列にする。</summary>
        public static string Show(object value)
        {
            if (value == null) return "null";
            if (value is string) return Quote((string)value);
            if (value is bool) return ((bool)value) ? "true" : "false";
            if (value is char) return "'" + (char)value + "'";

            if (value is IEnumerable)
            {
                var items = new List<string>();
                foreach (object item in (IEnumerable)value)
                {
                    items.Add(Show(item));
                }
                return string.Format(CultureInfo.InvariantCulture,
                    "[{0}] ({1}件)", string.Join(", ", items.ToArray()), items.Count);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Quote(string s)
        {
            var q = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\r': q.Append("\\r"); break;
                    case '\n': q.Append("\\n"); break;
                    case '\t': q.Append("\\t"); break;
                    default: q.Append(c); break;
                }
            }
            return q.Append('"').ToString();
        }
    }
}
