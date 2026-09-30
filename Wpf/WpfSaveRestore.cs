// StcSaveRestore([[_Common/StandardTemplateClass.cs]])のRegistCtrl方式を、WPFのコントロールで使えるようにしたもの。
// 保存形式はStcSaveRestore.BuildGenericProfile/ApplyGenericProfileと同じGenericProfile(JSON)で、
// キーも同じ"AttrName|AttrValue"にしてある。WinForms版と同じAttrName/AttrValueで登録すれば、
// WinForms版で保存したプロファイルをWPF版でそのまま読める(逆も同じ)。
// 旧XML形式には対応しない(XMLからの移行はWinForms版のJsonSaveRestore.LoadWithMigrationで済んでいる前提)。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace StandardTemplate.Wpf
{
    public class WpfSaveRestore
    {
        private class ValueEntry
        {
            public String Key;
            public String LegacyKey;
            public String DefaultValue;
            public Func<String> Get;
            public Action<String> Set;
        }

        private class ListEntry
        {
            public String Key;
            public Func<List<String>> Get;
            public Action<List<String>> Set;
        }

        private class GridEntry
        {
            public String Key;
            public Func<List<List<String>>> Get;
            public Action<List<List<String>>> Set;
        }

        private readonly List<ValueEntry> Values = new List<ValueEntry>();
        private readonly List<ListEntry> Lists = new List<ListEntry>();
        private readonly List<GridEntry> Grids = new List<GridEntry>();

        private static String MakeKey(String AttrName, String AttrValue)
        {
            return AttrName + "|" + AttrValue;
        }

        // 任意の値を登録する(Get/Setを渡せば、TextBox等以外の値も保存対象にできる)
        public void RegistValue(String AttrName, String AttrValue, Func<String> Get, Action<String> Set, String ElementValue = "", String LegacyAttrValue = null)
        {
            Values.Add(new ValueEntry
            {
                Key = MakeKey(AttrName, AttrValue),
                LegacyKey = (LegacyAttrValue == null) ? null : MakeKey(AttrName, LegacyAttrValue),
                DefaultValue = ElementValue,
                Get = Get,
                Set = Set,
            });
        }

        public void RegistList(String AttrName, String AttrValue, Func<List<String>> Get, Action<List<String>> Set)
        {
            Lists.Add(new ListEntry { Key = MakeKey(AttrName, AttrValue), Get = Get, Set = Set });
        }

        // DataGrid等の表データ(行×列の文字列)を登録する
        public void RegistGrid(String AttrName, String AttrValue, Func<List<List<String>>> Get, Action<List<List<String>>> Set)
        {
            Grids.Add(new GridEntry { Key = MakeKey(AttrName, AttrValue), Get = Get, Set = Set });
        }

        public void RegistCtrl(String AttrName, String AttrValue, TextBox Ctrl, String ElementValue = "", String LegacyAttrValue = null)
        {
            RegistValue(AttrName, AttrValue, () => Ctrl.Text, v => Ctrl.Text = v, ElementValue, LegacyAttrValue);
        }

        // CheckBox/RadioButton。WinForms版と同じく"True"/"False"の文字列で保存する
        public void RegistCtrl(String AttrName, String AttrValue, ToggleButton Ctrl, String ElementValue = "", String LegacyAttrValue = null)
        {
            RegistValue(AttrName, AttrValue, () => (Ctrl.IsChecked == true).ToString(), v => Ctrl.IsChecked = (v == "True"), ElementValue, LegacyAttrValue);
        }

        // ComboBoxの入力欄の文字列(Text)を保存する。項目一覧を保存したい場合はRegistCtrlList
        public void RegistCtrl(String AttrName, String AttrValue, ComboBox Ctrl, String ElementValue = "")
        {
            RegistValue(AttrName, AttrValue, () => Ctrl.Text, v => Ctrl.Text = v, ElementValue);
        }

        // ScrollBar/Slider等。WinForms版のHScrollBarと同じく整数で保存する
        public void RegistCtrl(String AttrName, String AttrValue, RangeBase Ctrl, int ElementValue = 0)
        {
            RegistValue(AttrName, AttrValue, () => ((int)Ctrl.Value).ToString(), v =>
            {
                int parsed;
                if (int.TryParse(v, out parsed))
                {
                    Ctrl.Value = parsed;
                }
            }, ElementValue.ToString());
        }

        // ComboBoxの項目一覧(履歴)を保存する。ItemsSourceではなくItemsに直接文字列を入れる使い方が前提
        public void RegistCtrlList(String AttrName, String AttrValue, ComboBox Ctrl)
        {
            RegistList(AttrName, AttrValue,
                () => Ctrl.Items.Cast<Object>().Select(item => item.ToString()).ToList(),
                items =>
                {
                    Ctrl.Items.Clear();
                    foreach (String item in items)
                    {
                        Ctrl.Items.Add(item);
                    }
                });
        }

        public GenericProfile BuildGenericProfile()
        {
            GenericProfile profile = new GenericProfile();
            foreach (ValueEntry entry in Values) { profile.Values[entry.Key] = entry.Get(); }
            foreach (ListEntry entry in Lists) { profile.Lists[entry.Key] = entry.Get(); }
            foreach (GridEntry entry in Grids) { profile.Grids[entry.Key] = entry.Get(); }
            return profile;
        }

        // 登録済みの値を既定値に戻してから、profileにある値だけ上書きする(WinForms版と同じ方針)。
        // profileがnullなら既定値に戻すだけになる
        public void ApplyGenericProfile(GenericProfile profile)
        {
            // 一覧(Items.Clear)を先に戻す。同じComboBoxをRegistCtrlとRegistCtrlListの両方で登録した場合、
            // 値(Text)を先に入れるとWPFではItems.Clearで消えてしまうため
            foreach (ListEntry entry in Lists)
            {
                List<String> items;
                if (profile == null || !profile.Lists.TryGetValue(entry.Key, out items) || items == null)
                {
                    items = new List<String>();
                }
                entry.Set(items);
            }

            foreach (GridEntry entry in Grids)
            {
                List<List<String>> rows;
                if (profile == null || !profile.Grids.TryGetValue(entry.Key, out rows) || rows == null)
                {
                    rows = new List<List<String>>();
                }
                entry.Set(rows);
            }

            foreach (ValueEntry entry in Values)
            {
                String value;
                if (profile != null && (profile.Values.TryGetValue(entry.Key, out value) ||
                                        (entry.LegacyKey != null && profile.Values.TryGetValue(entry.LegacyKey, out value))))
                {
                    entry.Set(value ?? "");
                }
                else
                {
                    entry.Set(entry.DefaultValue);
                }
            }
        }

        public Boolean Save(String FilePath)
        {
            try
            {
                JsonFileStorage.Save(FilePath, BuildGenericProfile());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ファイルが無い・壊れている場合はfalseを返し、コントロールは今の値のまま変えない
        public Boolean Load(String FilePath)
        {
            GenericProfile profile = JsonFileStorage.Load<GenericProfile>(FilePath);
            if (profile == null)
            {
                return false;
            }
            ApplyGenericProfile(profile);
            return true;
        }

        // 起動時用。ファイルが無い・壊れている場合は既定値(RegistCtrlのElementValue)を入れる
        public void LoadOrDefault(String FilePath)
        {
            ApplyGenericProfile(JsonFileStorage.Load<GenericProfile>(FilePath));
        }
    }
}
