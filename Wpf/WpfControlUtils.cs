// StcUtils([[_Common/StandardTemplateClass.cs]])のうち、WinFormsのコントロールを引数に取るため
// WPFから呼べないもの(選択項目の取得・クリップボードへのコピー・Ctrl+A・コンボボックスの履歴など)のWPF版。
// 中身はWinForms版と同じ処理にしてある(メソッド名もなるべくStcUtilsと同じにしてある)。
//
// もとはCheetos(WpfUtils)・FFEdit(WpfControlUtils)・FileArranger(WpfControlHelper)が
// それぞれプロジェクト内に持っていた、ほぼ同じ処理をここへ1つにまとめたもの。
//
// ■使い方
// csprojに次の1ファイルをリンクするだけでよい(他の_Commonファイルには依存しない)。
//   <Compile Include="..\..\_Common\Wpf\WpfControlUtils.cs"><Link>_Common\Wpf\WpfControlUtils.cs</Link></Compile>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StandardTemplate.Wpf
{
    public static class WpfControlUtils
    {
        // ------------------------------------------------------------------
        // ListBox / ListView
        // ------------------------------------------------------------------

        // 選択項目のインデックスを昇順で返す(WinFormsのSelectedItems[i].Index相当)。
        // WPFのSelectedItemsは「選択した順」に並ぶが、WinFormsのSelectedItemsはインデックス順だったため、それに合わせる。
        // 選択項目ごとにItems.IndexOfで先頭から探すと「項目数×選択数」になり、全選択では項目数の2乗になる。
        // 選択項目をHashSetに入れておき、Itemsを先頭から1回なめるだけで済ませる(結果は最初から昇順)
        public static List<int> GetSelectedIndices(ListBox listCtrl)
        {
            HashSet<Object> selected = new HashSet<Object>(listCtrl.SelectedItems.Cast<Object>());
            List<int> indices = new List<int>(selected.Count);
            for (int i = 0; i < listCtrl.Items.Count && indices.Count < selected.Count; i++)
            {
                if (selected.Contains(listCtrl.Items[i]))
                {
                    indices.Add(i);
                }
            }
            return indices;
        }

        // 選択項目を「画面の並び順(インデックス順)」で返す
        public static List<Object> GetSelectedItemsInIndexOrder(ListBox listCtrl)
        {
            return GetSelectedIndices(listCtrl).Select(index => listCtrl.Items[index]).ToList();
        }

        // 選択中の項目の表示名を画面の並び順で返す
        public static List<String> GetSelectedNames(ListBox listCtrl)
        {
            return GetSelectedItemsInIndexOrder(listCtrl).Select(item => item.ToString()).ToList();
        }

        // 一覧の項目を入れ替える
        public static void SetListItems(ItemsControl listCtrl, IEnumerable<Object> items)
        {
            listCtrl.Items.Clear();
            foreach (Object item in items)
            {
                listCtrl.Items.Add(item);
            }
        }

        // Ctrl+Aで全選択(WinForms版StcUtils.SelectAll(KeyEventArgs)はSendKeysで{HOME}+{END}を送っていた)
        public static void SelectAll(ListBox listCtrl, KeyEventArgs e)
        {
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                listCtrl.SelectAll();
                e.Handled = true;
            }
        }

        // 選択項目を「rootPath\項目名」の形で改行区切りに連結する(StcUtils.GetSelectName/GetSelectListName相当)。
        // getItemTextを省略すると項目のToString()を使う
        public static String GetSelectListName(ListBox listCtrl, Func<Object, String> getItemText = null, String rootPath = "")
        {
            Func<Object, String> textOf = getItemText ?? (item => item.ToString());
            StringBuilder targetName = new StringBuilder();
            foreach (Object item in GetSelectedItemsInIndexOrder(listCtrl))
            {
                if (rootPath != String.Empty)
                {
                    targetName.Append(rootPath).Append(@"\");
                }
                targetName.Append(textOf(item)).Append(Environment.NewLine);
            }
            return targetName.ToString();
        }

        // リストボックスの選択項目をクリップボードへコピーする(StcUtils.CopyToClipboardと同じ)
        public static Boolean CopyToClipboard(ListBox listCtrl, String rootPath = "")
        {
            return SetClipboardText(GetSelectListName(listCtrl, null, rootPath));
        }

        // Ctrl+Cで選択項目をコピー(StcUtils.CopyToClipboard(KeyEventArgs, ListView, RootPath, index)相当)。
        // Ctrl+C以外のキーや、何も選択されていないときは何もせずfalseを返す
        public static Boolean CopyToClipboard(KeyEventArgs e, ListBox listCtrl, Func<Object, String> getItemText = null, String rootPath = "")
        {
            if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
            {
                return false;
            }

            String targetName = GetSelectListName(listCtrl, getItemText, rootPath);
            if (targetName.Equals(String.Empty))
            {
                return false;
            }
            e.Handled = true;
            return SetClipboardText(targetName);
        }

        // ------------------------------------------------------------------
        // クリップボード
        // ------------------------------------------------------------------

        // クリップボードは他のアプリが掴んでいる間は開けず例外になるため、少し待って数回やり直す
        // (StcUtils.SetClipboardTextと同じ考え方。こちらはWPFのClipboardを使う)。空文字のときはクリアする
        public static Boolean SetClipboardText(String text)
        {
            const int retryCount = 5;
            const int retryWaitMsec = 100;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    if (String.IsNullOrEmpty(text))
                    {
                        Clipboard.Clear();
                    }
                    else
                    {
                        Clipboard.SetText(text);
                    }
                    return true;
                }
                catch (COMException)
                {
                    Thread.Sleep(retryWaitMsec);
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // ComboBox
        // ------------------------------------------------------------------

        // WinForms版ComboBoxのSorted=true相当の目印。WPFのItems.SortDescriptionsは、Items.Addした項目が
        // Refreshするまで並び替わらないため使わず、項目を入れ替えるときにこちらで並べ替える
        public static readonly DependencyProperty SortedProperty =
            DependencyProperty.RegisterAttached("Sorted", typeof(Boolean), typeof(WpfControlUtils), new PropertyMetadata(false));

        public static void SetSorted(ItemsControl ctrl, Boolean value)
        {
            ctrl.SetValue(SortedProperty, value);
        }

        public static Boolean GetSorted(ItemsControl ctrl)
        {
            return (Boolean)ctrl.GetValue(SortedProperty);
        }

        // 項目一覧を入れ替える。入力可能なComboBoxはItems.Clearで入力欄の文字が消えることがあるため、
        // 入力欄の文字(Text)は入れ替え前のまま残す(WinForms版ComboBox(DropDown)と同じ見え方にする)。
        // Sortedの目印が付いていれば昇順(WinForms版と同じくカルチャ依存の文字列比較)に並べる
        public static void SetItemsKeepText(ComboBox comboCtrl, IEnumerable<String> items)
        {
            String text = comboCtrl.Text;
            List<String> list = items.ToList();
            if (GetSorted(comboCtrl))
            {
                list.Sort(StringComparer.CurrentCulture);
            }
            comboCtrl.Items.Clear();
            foreach (String item in list)
            {
                comboCtrl.Items.Add(item);
            }
            if (comboCtrl.IsEditable && comboCtrl.Text != text)
            {
                comboCtrl.Text = text;
            }
        }

        public static List<String> GetItems(ComboBox comboCtrl)
        {
            return comboCtrl.Items.Cast<Object>().Select(item => item.ToString()).ToList();
        }

        // 文字配列をコンボボックスにセット(StcUtils.SetComboBoxFromArray相当)。
        // removeStringは各要素の先頭から取り除く部分(長さ+区切り1文字分だけ読み飛ばす)
        public static void SetComboBoxFromArray(ComboBox comboCtrl, String[] values, String removeString = "", String limitString = "")
        {
            int startIdx = removeString == String.Empty ? 0 : removeString.Length + 1;
            SetComboBoxFromArraySubString(comboCtrl, values, startIdx, "", limitString);
        }

        // 文字配列(SubString)をコンボボックスにセット(StcUtils.SetComboBoxFromArraySubString相当)。
        // valuesがnull(プロファイル未読込・リストアップ前)なら項目を空にするだけ
        public static void SetComboBoxFromArraySubString(ComboBox comboCtrl, String[] values, int startIdx, String endDelimiter = "", String limitString = "", Boolean isReverse = false)
        {
            List<String> items = new List<String>();
            HashSet<String> added = new HashSet<String>();
            foreach (String value in values ?? new String[0])
            {
                // カラ文字
                if (String.IsNullOrEmpty(value))
                {
                    continue;
                }

                // 文字列生成
                int endIdx = isReverse ? value.LastIndexOf(endDelimiter) : value.IndexOf(endDelimiter);

                String valueName;
                if (endIdx >= 0 && endIdx > startIdx)
                {
                    valueName = value.Substring(startIdx, endIdx - startIdx);
                }
                else
                {
                    valueName = value.Substring(startIdx);
                }

                // 文字の絞り込み(limitStringを含む項目だけ残す。大文字小文字を区別せずに部分一致で検索)
                if (limitString != String.Empty && valueName.IndexOf(limitString, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                // 登録済みだったらスキップ(HashSetで重複を判定し、項目数が多くてもO(n)で済ませる)
                if (added.Add(valueName))
                {
                    items.Add(valueName);
                }
            }

            SetItemsKeepText(comboCtrl, items);
        }

        // ComboBoxのTextをプルダウン(履歴)に追加する。重複は除き、順序は保つ。Textが空なら何もしない
        // (StcUtils.AddComboBoxTextToItems相当)
        public static void AddComboBoxTextToItems(ComboBox comboCtrl)
        {
            if (comboCtrl.Text == String.Empty)
            {
                return;
            }

            List<String> items = GetItems(comboCtrl);
            items.Add(comboCtrl.Text);
            SetItemsKeepText(comboCtrl, items.Distinct().ToList());
        }
    }
}
