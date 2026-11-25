using System.Globalization;
using System.Collections;
using System.Windows;
using System.Windows.Data;

namespace Inventory_System.Furniture;

// يحاذي شريط عناوين الأعمدة مع الخلايا، ويعوض الأعمدة النسبية التي لا تعيد عرضًا مباشرًا.
public sealed class FurnitureGroupHeaderLayoutConverter : IMultiValueConverter
{
    #region حساب عرض العناوين النسبية وإزاحة التجميع
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double totalIndent
            || values[1] is not int groupLevel || groupLevel < 0)
            return DependencyProperty.UnsetValue;

        // نترك إزاحة المستوى الأخير ونخصم الإزاحات الأب من أول عمود حتى تصطف الحدود مع الخلايا.
        double currentGroupIndent = totalIndent / (groupLevel + 1);
        if (values.Length == 2) return currentGroupIndent;

        if (values.Length >= 7 && values[3] is int visibleIndex && values[4] is GridLength)
        {
            double columnWidth = ResolveColumnWidth(values);
            if (columnWidth <= 0) return DependencyProperty.UnsetValue;
            return visibleIndex == 0
                ? Math.Max(0, columnWidth - totalIndent + currentGroupIndent)
                : columnWidth;
        }

        return DependencyProperty.UnsetValue;
    }

    // يستخدم القياس الفعلي أولًا، ثم يوزع المساحة المتبقية على الأعمدة النجمية بحسب أوزانها.
    private static double ResolveColumnWidth(object[] values)
    {
        if (values[2] is double actualWidth && actualWidth > 0) return actualWidth;
        if (values[4] is not GridLength columnDefinition) return 0;
        if (values[5] is not IEnumerable columns || values[6] is not double viewportWidth || viewportWidth <= 0)
            return values[2] is double fallback ? fallback : 0;

        double usedWidth = 0;
        double unresolvedStarWeight = 0;
        foreach (object? column in columns)
        {
            if (column is null) continue;
            var type = column.GetType();
            double measuredWidth = type.GetProperty("ActualWidth")?.GetValue(column) is double width ? width : 0;
            if (measuredWidth > 0)
            {
                usedWidth += measuredWidth;
                continue;
            }

            if (type.GetProperty("Width")?.GetValue(column) is not GridLength widthDefinition) continue;
            if (widthDefinition.IsAbsolute) usedWidth += widthDefinition.Value;
            else if (widthDefinition.IsStar) unresolvedStarWeight += widthDefinition.Value;
        }

        if (columnDefinition.IsAbsolute) return columnDefinition.Value;
        if (!columnDefinition.IsStar || unresolvedStarWeight <= 0) return 0;
        double remainingWidth = Math.Max(0, viewportWidth - usedWidth);
        return remainingWidth * columnDefinition.Value / unresolvedStarWeight;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
    #endregion
}
