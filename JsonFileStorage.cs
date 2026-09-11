// .NET Framework標準の JavaScriptSerializer(System.Web.Extensions.dll)を使った、
// 外部NuGetパッケージ不要のシンプルなJSON読み書きユーティリティ。
//
// CS_Form配下の各プロジェクトはこれまでXML(StcSaveRestoreのRegistCtrl方式、
// DataGridView等のコントロールに直接値を出し入れする仕組み)で設定値を保存してきたが、
// 今後はプロジェクトごとに用意したPOCOクラス(例: EventRecorderのProfile.cs)を介して
// JSONで保存する方式へ段階的に置き換えていく。その足場としてまず_Commonに置く。
//
// 「外部dll無しで単体完結させたい」という方針([[ogu-event-recorder-project]])に合わせ、
// Newtonsoft.Json等のNuGetパッケージは使わず、.NET Frameworkに標準搭載されている
// System.Web.Extensions参照だけで完結させている。
using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace StandardTemplate
{
    public static class JsonFileStorage
    {
        // dataをJSON文字列にしてfilePathへ書き出す(UTF-8、BOM無し)
        public static void Save<T>(String filePath, T data)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;

            String json = serializer.Serialize(data);
            File.WriteAllText(filePath, json, new UTF8Encoding(false));
        }

        // filePathのJSONを読み込んでT型に変換する。ファイルが無ければdefault(T)を返す
        public static T Load<T>(String filePath)
        {
            if (!File.Exists(filePath))
            {
                return default(T);
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;

            String json = File.ReadAllText(filePath, Encoding.UTF8);
            return serializer.Deserialize<T>(json);
        }
    }
}
