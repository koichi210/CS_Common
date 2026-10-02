// TextBoxEx.PlaceholderText([[_Common/TextBoxEx.cs]])のWPF版。
// Textが空の間だけ、入力例の文字列を薄い灰色で表示する(入力を始めると消え、空に戻ると再び出る)。
// Text自体には何も入らないので、空判定や保存/復元には影響しない。
//
// ■使い方
// 添付プロパティなので、素のTextBoxにもTextBoxExにも付けられる(このファイル単体で完結し、
// 他の_Commonファイルには依存しない。_Commonを使っていないプロジェクトでもこれだけリンクすればよい)。
//   XAML:  xmlns:stw="clr-namespace:StandardTemplate.Wpf"
//          <TextBox stw:Placeholder.Text="例: C:\Work" />
//   コード: Placeholder.SetText(textBox, "例: C:\\Work");
//
// ■仕組み
// WPFのTextBoxにはプレースホルダー機能が無いため、TextBoxの上に重ねるAdorner(装飾レイヤー)で描画する。
// Backgroundを差し替える方式と違い、TextBox側の背景色・スタイルを壊さない。
// TabControlのタブ切替などでTextBoxがビジュアルツリーから外れる/戻ることがあるため、
// Loaded/Unloadedのたびに付け外しし、非表示の間は描画しない。
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace StandardTemplate.Wpf
{
    public static class Placeholder
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text", typeof(String), typeof(Placeholder), new PropertyMetadata(String.Empty, OnTextChanged));

        public static String GetText(DependencyObject obj)
        {
            return (String)obj.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject obj, String value)
        {
            obj.SetValue(TextProperty, value);
        }

        // 付け外しの管理用(TextBoxごとに今付いているAdorner)
        private static readonly DependencyProperty AdornerProperty = DependencyProperty.RegisterAttached(
            "Adorner", typeof(PlaceholderAdorner), typeof(Placeholder), new PropertyMetadata(null));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TextBox textBox = d as TextBox;
            if (textBox == null)
            {
                return;
            }

            // 二重登録にならないよう、いったん外してから付け直す
            textBox.Loaded -= TextBox_Loaded;
            textBox.Unloaded -= TextBox_Unloaded;
            textBox.TextChanged -= TextBox_TextChanged;
            textBox.IsVisibleChanged -= TextBox_IsVisibleChanged;

            if (String.IsNullOrEmpty(e.NewValue as String))
            {
                Detach(textBox);
                return;
            }

            textBox.Loaded += TextBox_Loaded;
            textBox.Unloaded += TextBox_Unloaded;
            textBox.TextChanged += TextBox_TextChanged;
            textBox.IsVisibleChanged += TextBox_IsVisibleChanged;

            if (textBox.IsLoaded)
            {
                Attach(textBox);
            }
            Refresh(textBox);
        }

        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            Attach((TextBox)sender);
        }

        private static void TextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            Detach((TextBox)sender);
        }

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Refresh((TextBox)sender);
        }

        private static void TextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Refresh((TextBox)sender);
        }

        private static void Attach(TextBox textBox)
        {
            if (textBox.GetValue(AdornerProperty) != null)
            {
                return;
            }

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(textBox);
            if (layer == null)
            {
                return;
            }

            PlaceholderAdorner adorner = new PlaceholderAdorner(textBox);
            layer.Add(adorner);
            textBox.SetValue(AdornerProperty, adorner);
        }

        private static void Detach(TextBox textBox)
        {
            PlaceholderAdorner adorner = textBox.GetValue(AdornerProperty) as PlaceholderAdorner;
            if (adorner == null)
            {
                return;
            }

            AdornerLayer layer = AdornerLayer.GetAdornerLayer(textBox);
            if (layer != null)
            {
                layer.Remove(adorner);
            }
            textBox.ClearValue(AdornerProperty);
        }

        private static void Refresh(TextBox textBox)
        {
            PlaceholderAdorner adorner = textBox.GetValue(AdornerProperty) as PlaceholderAdorner;
            if (adorner != null)
            {
                adorner.InvalidateVisual();
            }
        }

        private sealed class PlaceholderAdorner : Adorner
        {
            // 文字の開始位置をTextBox内のキャレット位置に揃えるための、枠+Padding以外の内側余白
            private const Double InnerMargin = 2.0;

            private readonly TextBox textBox;

            public PlaceholderAdorner(TextBox textBox) : base(textBox)
            {
                this.textBox = textBox;
                IsHitTestVisible = false;
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                String text = GetText(textBox);
                if (!textBox.IsVisible || !String.IsNullOrEmpty(textBox.Text) || String.IsNullOrEmpty(text))
                {
                    return;
                }

                Typeface typeface = new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch);

                // pixelsPerDip付きのコンストラクタ/VisualTreeHelper.GetDpiは.NET 4.6.2以降にしか無く、
                // それより古いターゲットのプロジェクトでもビルドできるよう、旧コンストラクタを使う
#pragma warning disable 618
                FormattedText formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    textBox.FlowDirection,
                    typeface,
                    textBox.FontSize,
                    Brushes.Gray);
#pragma warning restore 618

                Double left = textBox.BorderThickness.Left + textBox.Padding.Left + InnerMargin;
                Double right = textBox.BorderThickness.Right + textBox.Padding.Right + InnerMargin;
                Double top = textBox.BorderThickness.Top + textBox.Padding.Top;
                Double bottom = textBox.BorderThickness.Bottom + textBox.Padding.Bottom;
                Double innerHeight = Math.Max(0, textBox.ActualHeight - top - bottom);

                formatted.MaxTextWidth = Math.Max(1, textBox.ActualWidth - left - right);
                formatted.MaxLineCount = 1;
                formatted.Trimming = TextTrimming.CharacterEllipsis;

                Double y = top;
                if (textBox.VerticalContentAlignment == VerticalAlignment.Center)
                {
                    y = top + (innerHeight - formatted.Height) / 2;
                }
                else if (textBox.VerticalContentAlignment == VerticalAlignment.Bottom)
                {
                    y = top + innerHeight - formatted.Height;
                }

                drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, textBox.ActualWidth, textBox.ActualHeight)));
                drawingContext.DrawText(formatted, new Point(left, y));
                drawingContext.Pop();
            }
        }
    }
}
