using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace Inventory_System.converts
{
    public sealed class UserPermissionCountConverter : IMultiValueConverter
    {
        public object Convert(
            object[] values,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            int enabledCount = values.Count(value => value is bool isEnabled && isEnabled);
            return $"الصلاحيات المفعّلة: {enabledCount} من {values.Length}";
        }

        public object[] ConvertBack(
            object value,
            Type[] targetTypes,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
