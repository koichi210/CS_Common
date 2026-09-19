// Newtonsoft.Json(Json.NET)を使ったシンプルなJSON読み書きユーティリティ。
//
// CS_Form配下の各プロジェクトはこれまでXML(StcSaveRestoreのRegistCtrl方式、
// DataGridView等のコントロールに直接値を出し入れする仕組み)で設定値を保存してきたが、
// 今後はプロジェクトごとに用意したPOCOクラス(例: EventRecorderのProfile.cs)を介して
// JSONで保存する方式へ段階的に置き換えていく。その足場としてまず_Commonに置く。
//
// 以前は「外部dll無しで単体完結させたい」という方針([[ogu-event-recorder-project]])から
// .NET Framework標準のJavaScriptSerializer(System.Web.Extensions.dll)を使っていたが、
// 日付・大きい数値の扱いや整形出力(Indented)が弱く、属性によるカスタマイズもしづらいため、
// 2026-09-12にNewtonsoft.Jsonへ切り替えた。.NET Framework向けは追加DLLが
// Newtonsoft.Json.dllの1個だけで済み、System.Text.Json(.NET Framework向けだと
// 依存DLLが何個も付いてくる)より軽量でシンプルなためこちらを選んだ。
// このファイルをリンクする各プロジェクトのcsprojに
// <PackageReference Include="Newtonsoft.Json" Version="13.0.4" /> の追加が必要
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace StandardTemplate
{
    public static class JsonFileStorage
    {
        // dataをJSON文字列にしてfilePathへ書き出す(UTF-8、BOM無し、整形済み)。
        // Formatting.Indentedにしているのは、ユーザーがテキストエディタで開いて
        // 中身を直接確認・編集することもある想定のため(1行に詰め込まない)
        public static void Save<T>(String filePath, T data)
        {
            String json = JsonConvert.SerializeObject(data, Formatting.Indented);
            AtomicFile.WriteAllText(filePath, json, new UTF8Encoding(false));
        }

        // filePathのJSONを読み込んでT型に変換する。ファイルが無い・読めない・中身が壊れている場合はdefault(T)を返す
        public static T Load<T>(String filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return default(T);
                }

                String json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                return default(T);
            }
        }
    }
}
