// <root><Setting attribute="キー">値</Setting>...</root> という形のXML設定ファイルを、
// キーと値の対応として読み書きする。
//
// CaptureWindow / PictTriming / PictMerge / PictMerge2 の4プロジェクトが、
// それぞれ同じ形のXMLを手書きで組み立て、読み込みは属性名のif/else連打で
// 取り出していたため集約した。ファイル形式は従来とまったく同じなので、
// これまでの設定ファイルはそのまま読める。
//
// StcSaveRestore(コントロールを直接登録して一括で保存/復元する仕組み)との違いは、
// こちらが「コントロールに依存しないただのキーと値」である点。WPFのように
// StcSaveRestoreが使えない画面でも利用できる。
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Newtonsoft.Json;

namespace StandardTemplate
{
    class StcSimpleSettings
    {
        // 旧形式(XML)から新形式(JSON)への移行用の拡張子
        private const String JsonExtension = ".json";
        private const String XmlExtension = ".xml";

        private const String RootName = "root";
        private const String ElementName = "Setting";
        private const String AttributeName = "attribute";

        private readonly Dictionary<String, String> Values = new Dictionary<String, String>();

        public void Set(String Key, String Value)
        {
            Values[Key] = Value;
        }

        // 設定に無いキーはDefaultValueを返す
        public String Get(String Key, String DefaultValue = "")
        {
            String value;
            return Values.TryGetValue(Key, out value) ? value : DefaultValue;
        }

        public Boolean IsExist(String Key)
        {
            return Values.ContainsKey(Key);
        }

        // 設定ファイルを読み込む(JSON優先)。
        // JSONが無く同名のXMLがある場合は、XMLを読んでJSONで保存し直し、元のXMLは削除する。
        // 呼び出し側はJSONのパスだけ渡せばよく、移行のことを意識しなくてよい
        public static StcSimpleSettings LoadWithMigration(String JsonPath)
        {
            StcSimpleSettings settings = LoadJson(JsonPath);
            if (settings != null)
            {
                return settings;
            }

            String xmlPath = Path.ChangeExtension(JsonPath, XmlExtension);
            settings = Load(xmlPath);
            if (settings == null)
            {
                return null;
            }

            // JSONで保存できたときだけ旧XMLを消す(消してから保存に失敗して設定を失うことがないように)
            if (settings.SaveJson(JsonPath))
            {
                TryDeleteFile(xmlPath);
            }
            return settings;
        }

        // JSONで保存する。呼び出し側が.xmlのパスを渡してきても.jsonへ寄せる
        public Boolean SaveJson(String FilePath)
        {
            String jsonPath = Path.ChangeExtension(FilePath, JsonExtension);
            try
            {
                // FormattingはSystem.Xmlにも同名の型があるため、どちらか明示する
                AtomicFile.WriteAllText(jsonPath, JsonConvert.SerializeObject(Values, Newtonsoft.Json.Formatting.Indented),
                                        new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                return false;
            }
            return true;
        }

        // ファイルが無い・読めない・壊れている場合はnullを返す
        public static StcSimpleSettings LoadJson(String FilePath)
        {
            String jsonPath = Path.ChangeExtension(FilePath, JsonExtension);
            if (!File.Exists(jsonPath))
            {
                return null;
            }

            try
            {
                Dictionary<String, String> values =
                    JsonConvert.DeserializeObject<Dictionary<String, String>>(File.ReadAllText(jsonPath));
                if (values == null)
                {
                    return null;
                }

                StcSimpleSettings settings = new StcSimpleSettings();
                foreach (KeyValuePair<String, String> pair in values)
                {
                    settings.Set(pair.Key, pair.Value);
                }
                return settings;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                return null;
            }
        }

        private static void TryDeleteFile(String FilePath)
        {
            try
            {
                File.Delete(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 消せなくても実害は無い(以降はJSONが優先して読まれる)
            }
        }

        public Boolean Save(String FilePath)
        {
            XmlDocument document = new XmlDocument();
            document.AppendChild(document.CreateXmlDeclaration("1.0", "UTF-8", null));

            XmlElement root = document.CreateElement(RootName);
            document.AppendChild(root);

            foreach (KeyValuePair<String, String> pair in Values)
            {
                XmlElement element = document.CreateElement(ElementName);
                element.SetAttribute(AttributeName, pair.Key);
                element.InnerText = pair.Value ?? "";
                root.AppendChild(element);
            }

            try
            {
                AtomicFile.Write(FilePath, document.Save);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
            return true;
        }

        // ファイルが無い・読めない・壊れている場合はnullを返す(呼び出し側は既定値で動かす)
        public static StcSimpleSettings Load(String FilePath)
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            XmlDocument document = new XmlDocument();
            try
            {
                document.Load(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException)
            {
                return null;
            }

            if (document.DocumentElement == null)
            {
                return null;
            }

            StcSimpleSettings settings = new StcSimpleSettings();
            foreach (XmlNode node in document.DocumentElement)
            {
                XmlElement element = node as XmlElement;
                if (element == null)
                {
                    continue;
                }

                String key = element.GetAttribute(AttributeName);
                if (key != String.Empty)
                {
                    settings.Set(key, element.InnerText);
                }
            }

            return settings;
        }
    }
}
