using System;
using System.Collections.Generic;
using System.Text;
using DevExpress.Xpf.PdfViewer;
using System.Diagnostics;
namespace Inventory_System.Archive
{
   

#region تعريب عارض المستندات المنقول من DCMS
public class ArabicPdfViewerLocalizer : PdfViewerLocalizer
{
    public override string GetLocalizedString(PdfViewerStringId id)
    {
        switch (id)
        {
            // =========================
            // فتح وإغلاق
            // =========================

            case PdfViewerStringId.CommandOpenFileCaption:
                return "فتح";

            case PdfViewerStringId.CommandOpenFileDescription:
                return "فتح مستند PDF";

            case PdfViewerStringId.CommandOpenFileFromWebCaption:
                return "فتح من الإنترنت";

            case PdfViewerStringId.CommandOpenFileFromWebDescription:
                return "فتح مستند PDF من الإنترنت";

            case PdfViewerStringId.CommandCloseFileCaption:
                return "إغلاق";

            case PdfViewerStringId.CommandCloseFileDescription:
                return "إغلاق مستند PDF";

            case PdfViewerStringId.OpenDocumentCaption:
                return "فتح";

            case PdfViewerStringId.OpenDocumentFromWebCaption:
                return "فتح من الإنترنت";


            // =========================
            // التنقل
            // =========================

            case PdfViewerStringId.CommandPreviousPageCaption:
                return "السابق";

            case PdfViewerStringId.CommandPreviousPageDescription:
                return "عرض الصفحة السابقة";

            case PdfViewerStringId.CommandNextPageCaption:
                return "التالي";

            case PdfViewerStringId.CommandNextPageDescription:
                return "عرض الصفحة التالية";



            // =========================
            // التكبير والتصغير
            // =========================

            case PdfViewerStringId.CommandZoomInCaption:
                return "تكبير";

            case PdfViewerStringId.CommandZoomInDescription:
                return "تكبير عرض المستند";

            case PdfViewerStringId.CommandZoomInShortcutCaption:
                return " (Ctrl + +)";

            case PdfViewerStringId.CommandZoomOutCaption:
                return "تصغير";

            case PdfViewerStringId.CommandZoomOutDescription:
                return "تصغير عرض المستند";

            case PdfViewerStringId.CommandZoomOutShortcutCaption:
                return " (Ctrl + -)";

            case PdfViewerStringId.CommandViewExactZoomListCaption:
                return "التكبير";

            case PdfViewerStringId.CommandViewExactZoomListDescription:
                return "تغيير مستوى تكبير المستند";


            // =========================
            // نسب التكبير
            // =========================

            case PdfViewerStringId.CommandZoom10Caption:
                return "10%";

            case PdfViewerStringId.CommandZoom25Caption:
                return "25%";

            case PdfViewerStringId.CommandZoom50Caption:
                return "50%";

            case PdfViewerStringId.CommandZoom75Caption:
                return "75%";

            case PdfViewerStringId.CommandZoom100Caption:
                return "100%";

            case PdfViewerStringId.CommandZoom125Caption:
                return "125%";

            case PdfViewerStringId.CommandZoom150Caption:
                return "150%";

            case PdfViewerStringId.CommandZoom200Caption:
                return "200%";

            case PdfViewerStringId.CommandZoom400Caption:
                return "400%";

            case PdfViewerStringId.CommandZoom500Caption:
                return "500%";


            // =========================
            // مجموعات الـ Ribbon
            // =========================

            case PdfViewerStringId.ZoomRibbonGroupCaption:
                return "عرض";

            case PdfViewerStringId.NavigationRibbonGroupCaption:
                return "التنقل";

            case PdfViewerStringId.FileRibbonGroupCaption:
                return "ملف";

            case PdfViewerStringId.FindRibbonGroupCaption:
                return "بحث";

            case PdfViewerStringId.ViewRibbonGroupCaption:
                return "عرض";


            // =========================
            // البحث
            // =========================

            case PdfViewerStringId.MessageSearchCaption:
                return "بحث";


            // =========================
            // الطباعة
            // =========================

            case PdfViewerStringId.CommandPrintFileCaption:
                return "طباعة";

            case PdfViewerStringId.CommandPrintFileDescription:
                return "طباعة المستند";


            // =========================
            // الحفظ
            // =========================

            case PdfViewerStringId.CommandSaveAsCaption:
                return "حفظ باسم";


            // =========================
            // الإغلاق
            // =========================

            case PdfViewerStringId.SaveChangesCaption:
                return "عارض PDF";

            case PdfViewerStringId.SaveChangesMessage:
                return "هل تريد حفظ التغييرات على المستند قبل إغلاقه؟";
        }

        // لمعرفة أي نصوص ما زالت غير مترجمة
        Debug.WriteLine($"PDF Viewer - Untranslated ID: {id}");

        return base.GetLocalizedString(id);
    }
}
#endregion
}
