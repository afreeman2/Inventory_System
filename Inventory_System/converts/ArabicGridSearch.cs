using DevExpress.Data.Filtering;
using DevExpress.Xpf.Grid;

namespace Inventory_System.converts;

internal static class ArabicGridSearch
{
    #region مجموعات الحروف في واجهة المستفيد
    private static string Equivalents(char letter) => letter switch
    {
        'ا' or 'أ' or 'إ' or 'آ' => "اأإآ",
        'ي' or 'ى' or 'ئ' => "يىئ",
        'و' or 'ؤ' => "وؤ",
        'ه' or 'ة' => "هة",
        _ => ""
    };

    // للقوائم المحملة في الذاكرة نوحّد كل حروف الاسم للبحث مع إبقاء النص المعروض كما هو.
    internal static string Normalize(string? value) => string.Concat((value ?? "").Trim()
        .Where(letter => "ـًٌٍَُِّْ".IndexOf(letter) < 0)
        .Select(letter => letter == 'ٱ' ? 'ا' : Equivalents(letter).FirstOrDefault(letter)));
    #endregion

    #region بناء فلتر بحث الجدول وإبراز النص
    // يولّد بدائل حرف البداية للقوائم التي يستعلم جدولها من قاعدة البيانات.
    internal static void ApplyPrefixVariants(SearchStringToFilterCriteriaEventArgs e, string field, bool isPasting)
    {
        var text = e.SearchString?.Trim();
        if (isPasting || string.IsNullOrEmpty(text))
        {
            e.Filter = null;
            e.Highlighting = null;
            return;
        }

        var equivalents = Equivalents(text[0]);
        e.Filter = equivalents.Length == 0
            ? Filter(FunctionOperatorType.StartsWith, field, text)
            : new GroupOperator(GroupOperatorType.Or, equivalents.Select(letter =>
                Filter(FunctionOperatorType.StartsWith, field, letter + text[1..])));
        e.Highlighting = [new(field, text)];
    }

    // يقارن حقول الأسماء الموحّدة، مع إمكان البحث في أعمدة نصية مثل الباركود بقيمتها الأصلية.
    internal static void ApplyLookup(SearchStringToFilterCriteriaEventArgs e,
        FunctionOperatorType match = FunctionOperatorType.StartsWith,
        params (string Field, bool Normalize)[] additionalFields)
    {
        var original = e.SearchString?.Trim() ?? "";
        var normalized = Normalize(original);
        if (normalized.Length == 0)
        {
            e.Filter = null;
            e.Highlighting = null;
            return;
        }

        var nameFilter = Filter(match, "SearchName", normalized);
        e.Filter = additionalFields.Length == 0
            ? nameFilter
            : new GroupOperator(GroupOperatorType.Or, additionalFields
                .Select(field => Filter(match, field.Field, field.Normalize ? normalized : original))
                .Prepend(nameFilter));
        e.Highlighting = [new("Name", original)];
    }

    private static CriteriaOperator Filter(FunctionOperatorType match, string field, string text) =>
        new FunctionOperator(match, new OperandProperty(field), new OperandValue(text));
    #endregion
}
