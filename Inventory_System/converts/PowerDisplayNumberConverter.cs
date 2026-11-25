using System.Globalization;
using System.Windows.Data;
using System.Windows;
using Binding = System.Windows.Data.Binding;

namespace Inventory_System.converts
{
    public sealed class PowerDisplayNumberConverter : IValueConverter
    {
        #region تنسيق أرقام شاشة الكهرباء كما في المصدر
        // تعرض القيم الصحيحة بلا كسور، وتعرض القيم الكسرية بثلاث منازل مع فواصل الآلاف.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return "0";
            // قد يمرر مصمم XAML نصوصًا تجريبية مثل string1 أو قيمة ربط غير جاهزة.
            // لا نحولها إلى صفر حتى لا تبدو رقمًا صحيحًا؛ يبقى المحول للعرض دون تعديل البيانات.
            if (value == DependencyProperty.UnsetValue || value == Binding.DoNothing) return string.Empty;
            if (!decimal.TryParse(System.Convert.ToString(value, culture), NumberStyles.Number, culture, out var number))
                return string.Empty;
            return number.ToString(number == decimal.Truncate(number) ? "#,0" : "#,0.000", culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
        #endregion
    }
}
