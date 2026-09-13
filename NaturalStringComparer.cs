// 文字列中の数字部分を「桁数」ではなく「数値」として比較する自然順(Natural Sort)Comparer。
// 標準のstring比較(ListBox.Sorted等が内部で使うもの)は1文字ずつの辞書順なので、
// "HOGE_2"と"HOGE_10"を比べると'1'<'2'により"HOGE_10"が先に来てしまう。
// これを避けて、人間の感覚に近い「HOGE_0, HOGE_1, HOGE_2, HOGE_10, HOGE_100」の順にしたい時に使う。
//
// ■使い方
// ListBox等のSortedプロパティ(自前のComparerを渡せない)ではなく、
// 表示前にArray.Sort/List.Sort/Enumerable.OrderBy等へこのComparerを渡して事前に並べ替え、
// 並べ替え済みの状態でItemsへ追加する(その場合はSortedはfalseにしておくこと)。
using System;
using System.Collections;
using System.Collections.Generic;

namespace StandardTemplate
{
    public class NaturalStringComparer : IComparer<String>, IComparer
    {
        // Comparer生成のたびにnewしなくて済むよう、使い回せる既定インスタンス
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        public int Compare(String x, String y)
        {
            if (x == null && y == null)
            {
                return 0;
            }
            if (x == null)
            {
                return -1;
            }
            if (y == null)
            {
                return 1;
            }

            int ix = 0;
            int iy = 0;
            while (ix < x.Length && iy < y.Length)
            {
                if (Char.IsDigit(x[ix]) && Char.IsDigit(y[iy]))
                {
                    int startX = ix;
                    while (ix < x.Length && Char.IsDigit(x[ix]))
                    {
                        ix++;
                    }

                    int startY = iy;
                    while (iy < y.Length && Char.IsDigit(y[iy]))
                    {
                        iy++;
                    }

                    int cmp = CompareNumericChunk(x.Substring(startX, ix - startX), y.Substring(startY, iy - startY));
                    if (cmp != 0)
                    {
                        return cmp;
                    }
                }
                else
                {
                    int startX = ix;
                    while (ix < x.Length && !Char.IsDigit(x[ix]))
                    {
                        ix++;
                    }

                    int startY = iy;
                    while (iy < y.Length && !Char.IsDigit(y[iy]))
                    {
                        iy++;
                    }

                    int cmp = String.Compare(x.Substring(startX, ix - startX), y.Substring(startY, iy - startY), StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0)
                    {
                        return cmp;
                    }
                }
            }

            // 片方だけ末尾に文字が残っていれば、短い方を前にする(自然な直感に合わせる)
            return (x.Length - ix).CompareTo(y.Length - iy);
        }

        // 数字だけの文字列同士を、桁数ではなく数値として比較する(先頭の0は無視)。
        // long等へのパースはしない(桁数が非常に多い数字でもオーバーフローしないようにするため)
        private static int CompareNumericChunk(String x, String y)
        {
            String trimmedX = x.TrimStart('0');
            String trimmedY = y.TrimStart('0');

            if (trimmedX.Length != trimmedY.Length)
            {
                return trimmedX.Length.CompareTo(trimmedY.Length);
            }

            return String.CompareOrdinal(trimmedX, trimmedY);
        }

        Int32 IComparer.Compare(Object x, Object y)
        {
            return Compare(x as String, y as String);
        }
    }
}
