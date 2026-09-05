using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace StandardTemplate.Tests
{
    /// <summary>
    /// StandardTemplate 名前空間の「外から見える API」をテキスト化する。
    ///
    /// リファクタで中身をどう書き換えても、ここが出力する文字列が 1 文字も変わらなければ、
    /// 共通クラスを参照している 17 プロジェクトのビルドは壊れない（＝呼び出し側は無影響）。
    /// リファクタ前に生成した ApiSnapshot.approved.txt との diff がゼロであることを
    /// ApiSnapshotTests が検証する。
    /// </summary>
    internal static class ApiSurface
    {
        private const string TargetNamespace = "StandardTemplate";

        public static string Dump()
        {
            Assembly asm = typeof(StcUtils).Assembly;

            List<Type> types = asm.GetTypes()
                .Where(t => t.Namespace == TargetNamespace)
                .Where(IsVisibleOutside)
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# StandardTemplate public API snapshot");
            sb.AppendLine("# このファイルは自動生成。手で編集しないこと。");
            sb.AppendLine("# 差分が出た = 呼び出し側に影響する変更をした、という意味。");
            sb.AppendLine();

            int memberCount = 0;
            foreach (Type t in types)
            {
                sb.AppendLine(DescribeType(t));
                foreach (string line in DescribeMembers(t))
                {
                    sb.Append("    ").AppendLine(line);
                    memberCount++;
                }
                sb.AppendLine();
            }

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "# total: {0} types / {1} members", types.Count, memberCount));
            return Normalize(sb.ToString());
        }

        /// <summary>改行コードの違いで差分が出ないように正規化する。</summary>
        public static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') + "\n";
        }

        /// <summary>アセンブリの外から到達できる型か（入れ子は親も辿る）。</summary>
        private static bool IsVisibleOutside(Type t)
        {
            if (!t.IsNested)
            {
                return t.IsPublic;
            }
            bool visible = t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamORAssem;
            return visible && IsVisibleOutside(t.DeclaringType);
        }

        private static string DescribeType(Type t)
        {
            var sb = new StringBuilder("TYPE ");

            bool isPublic = t.IsNested ? t.IsNestedPublic : t.IsPublic;
            sb.Append(isPublic ? "public " : t.IsNestedFamily ? "protected " : "protected internal ");

            if (t.IsEnum)
            {
                sb.Append("enum ");
            }
            else if (t.IsInterface)
            {
                sb.Append("interface ");
            }
            else if (t.IsValueType)
            {
                sb.Append("struct ");
            }
            else
            {
                if (t.IsAbstract && t.IsSealed) sb.Append("static ");
                else if (t.IsAbstract) sb.Append("abstract ");
                else if (t.IsSealed) sb.Append("sealed ");
                sb.Append("class ");
            }

            sb.Append(t.FullName);

            // 基底型が変わると継承側に影響が出るので記録する（object と Enum/ValueType は省略）
            Type baseType = t.BaseType;
            if (!t.IsEnum && !t.IsInterface && baseType != null
                && baseType != typeof(object) && baseType != typeof(ValueType))
            {
                sb.Append(" : ").Append(TypeName(baseType));
            }

            return sb.ToString();
        }

        private static IEnumerable<string> DescribeMembers(Type t)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Instance | BindingFlags.Static
                                     | BindingFlags.DeclaredOnly;

            var lines = new List<string>();

            if (t.IsEnum)
            {
                // enum は「名前と値の組」が変わると設定ファイルの互換にも効くので値まで記録する
                foreach (string name in Enum.GetNames(t).OrderBy(n => n, StringComparer.Ordinal))
                {
                    object value = Enum.Parse(t, name);
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "ENUM   {0} = {1}",
                        name, Convert.ToInt64(value, CultureInfo.InvariantCulture)));
                }
                return lines;
            }

            foreach (ConstructorInfo c in t.GetConstructors(Flags).Where(m => IsExposed(m)))
            {
                lines.Add("CTOR   " + Access(c) + " " + t.Name + "(" + Parameters(c) + ")");
            }

            foreach (FieldInfo f in t.GetFields(Flags).Where(IsExposed))
            {
                var sb = new StringBuilder("FIELD  " + Access(f) + " ");
                if (f.IsLiteral) sb.Append("const ");
                else if (f.IsStatic) sb.Append("static ");
                if (f.IsInitOnly) sb.Append("readonly ");
                sb.Append(TypeName(f.FieldType)).Append(' ').Append(f.Name);
                if (f.IsLiteral) sb.Append(" = ").Append(Literal(f.GetRawConstantValue()));
                lines.Add(sb.ToString());
            }

            foreach (PropertyInfo p in t.GetProperties(Flags))
            {
                MethodInfo getter = p.GetGetMethod(true);
                MethodInfo setter = p.GetSetMethod(true);
                bool exposed = (getter != null && IsExposed(getter)) || (setter != null && IsExposed(setter));
                if (!exposed) continue;

                var sb = new StringBuilder("PROP   ");
                MethodInfo any = getter ?? setter;
                sb.Append(Access(any)).Append(' ');
                if (any.IsStatic) sb.Append("static ");
                sb.Append(TypeName(p.PropertyType)).Append(' ').Append(p.Name).Append(" {");
                if (getter != null && IsExposed(getter)) sb.Append(" ").Append(Access(getter)).Append(" get;");
                if (setter != null && IsExposed(setter)) sb.Append(" ").Append(Access(setter)).Append(" set;");
                sb.Append(" }");
                lines.Add(sb.ToString());
            }

            foreach (EventInfo e in t.GetEvents(Flags))
            {
                MethodInfo add = e.GetAddMethod(true);
                if (add == null || !IsExposed(add)) continue;
                lines.Add("EVENT  " + Access(add) + " " + TypeName(e.EventHandlerType) + " " + e.Name);
            }

            foreach (MethodInfo m in t.GetMethods(Flags).Where(IsExposed))
            {
                if (m.IsSpecialName) continue;  // プロパティ/イベントのアクセサ、演算子は別枠で出している

                var sb = new StringBuilder("METHOD " + Access(m) + " ");
                if (m.IsStatic) sb.Append("static ");
                else if (m.IsAbstract) sb.Append("abstract ");
                else if (m.IsVirtual && !m.IsFinal) sb.Append("virtual ");
                sb.Append(TypeName(m.ReturnType)).Append(' ').Append(m.Name);
                if (m.IsGenericMethodDefinition)
                {
                    sb.Append('<')
                      .Append(string.Join(", ", m.GetGenericArguments().Select(a => a.Name)))
                      .Append('>');
                }
                sb.Append('(').Append(Parameters(m)).Append(')');
                lines.Add(sb.ToString());
            }

            // リフレクションの返却順は保証されないので、必ず並べ替えて安定させる
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        /// <summary>public か protected（＝派生クラスから触れる）ものだけを API とみなす。</summary>
        private static bool IsExposed(MethodBase m)
        {
            return m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;
        }

        private static bool IsExposed(FieldInfo f)
        {
            return f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly;
        }

        private static string Access(MethodBase m)
        {
            if (m.IsPublic) return "public";
            if (m.IsFamily) return "protected";
            return "protected internal";
        }

        private static string Access(FieldInfo f)
        {
            if (f.IsPublic) return "public";
            if (f.IsFamily) return "protected";
            return "protected internal";
        }

        private static string Parameters(MethodBase m)
        {
            return string.Join(", ", m.GetParameters().Select(Parameter));
        }

        private static string Parameter(ParameterInfo p)
        {
            var sb = new StringBuilder();
            if (p.IsOut) sb.Append("out ");
            else if (p.ParameterType.IsByRef) sb.Append("ref ");

            Type pt = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
            sb.Append(TypeName(pt)).Append(' ').Append(p.Name);

            // 省略可能引数の既定値が変わると、呼び出し側を再ビルドせずとも挙動が変わりうるので記録する
            if (p.IsOptional)
            {
                sb.Append(" = ").Append(Literal(p.RawDefaultValue));
            }
            return sb.ToString();
        }

        private static string TypeName(Type t)
        {
            if (t.IsArray)
            {
                return TypeName(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
            }
            if (t.IsByRef)
            {
                return TypeName(t.GetElementType());
            }
            if (t.IsGenericType)
            {
                string name = t.Name;
                int tick = name.IndexOf('`');
                if (tick >= 0) name = name.Substring(0, tick);
                return name + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName)) + ">";
            }
            return t.Name;
        }

        private static string Literal(object value)
        {
            if (value == null) return "null";
            if (value is string) return "\"" + (string)value + "\"";
            if (value is bool) return ((bool)value) ? "true" : "false";
            if (value is char) return "'" + (char)value + "'";
            if (value is Enum) return value.ToString();
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
