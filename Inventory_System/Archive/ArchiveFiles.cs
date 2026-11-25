using System.IO;
using System.Windows.Media.Imaging;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using UglyToad.PdfPig;
using TwainScanning;
using TwainScanning.Bridgex86;
using TwainScanning.NativeStructs;

namespace Inventory_System.Archive;

internal static class ArchiveFiles
{
    #region استيراد الملفات والمسح وفق مسار DCMS
    internal const int MaximumBytes = 100 * 1024 * 1024;
    // صفحة داخلية مؤقتة للمسودة الفارغة فقط؛ تجعل شريط أدوات PdfViewer مرئيًا قبل اختيار أي ملف.
    private static readonly Lazy<byte[]> EmptyPdfTemplate = new(CreateEmptyPdfTemplate);

    internal static byte[] EmptyPdf() => EmptyPdfTemplate.Value;

    // يمر ملف PDF كما هو، وتُحوّل الصورة إلى صفحة للعرض فقط كي تتاح أدوات DevExpress.
    internal static byte[] CreatePreviewPdf(byte[] bytes)
    {
        if (IsPdf(bytes)) return bytes;

        using var source = new MemoryStream(bytes, false);
        var frame = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        // نوحّد تنسيق الصورة للمعاينة مع الاحتفاظ بأبعادها النسبية وبايتات الملف الأصلي.
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(frame);
        using var image = new MemoryStream();
        encoder.Save(image);

        using var report = new XtraReport
        {
            Landscape = frame.PixelWidth > frame.PixelHeight,
            Margins = new DevExpress.Drawing.DXMargins(0, 0, 0, 0)
        };
        var detail = new DetailBand { HeightF = report.PageHeight };
        detail.Controls.Add(new XRPictureBox
        {
            ImageSource = new ImageSource(false, image.ToArray()),
            Sizing = ImageSizeMode.ZoomImage,
            WidthF = report.PageWidth,
            HeightF = detail.HeightF
        });
        report.Bands.Add(detail);
        using var preview = new MemoryStream();
        report.ExportToPdf(preview);
        return preview.ToArray();
    }

    private static byte[] CreateEmptyPdfTemplate()
    {
        using var report = new XtraReport();
        var detail = new DetailBand { HeightF = 2 };
        // عنصر شفاف صغير يضمن أن التقرير ينتج صفحة فعلية؛ التقرير الفارغ تمامًا لا ينشئ صفحة في DevExpress.
        detail.Controls.Add(new XRLabel { Text = " ", WidthF = 1, HeightF = 1, ForeColor = System.Drawing.Color.Transparent });
        report.Bands.Add(detail);
        report.CreateDocument();
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);
        return stream.ToArray();
    }

    // يبقى محتوى الملف PDF أو صورة أصلية قابلة للقراءة خارج البرنامج، دون تغليف البيانات الثنائية.
    internal static byte[] Read(string path)
    {
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidOperationException("حجم الملف يتجاوز 100 ميجابايت.");
        var bytes = File.ReadAllBytes(path);
        Validate(bytes);
        return bytes;
    }

    internal static bool IsPdf(byte[] bytes) => bytes.Length >= 5 && System.Text.Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";

    internal static void Validate(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes) throw new InvalidOperationException("اختر ملفًا غير فارغ لا يتجاوز 100 ميجابايت.");
        if (IsPdf(bytes))
        {
            // نفس قارئ PDF الموجود في DCMS؛ نفحص الملف قبل استبدال النسخة المحفوظة.
            using var document = PdfDocument.Open(bytes);
            if (document.NumberOfPages == 0) throw new InvalidOperationException("ملف PDF لا يحتوي صفحات.");
        }
        else
        {
            using var stream = new MemoryStream(bytes);
            _ = System.Windows.Media.Imaging.BitmapDecoder.Create(stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        }
    }

    internal static string[] Scanners()
    {
        var result = Bridgex86.GetAllDevices();
        if (result.Status == StatusType.Error || result.Value == null)
            throw new InvalidOperationException($"تعذر قراءة الماسحات. {result.ErrorMessages}");
        return result.Value.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
    }

    internal static async Task<byte[]> ScanAsync(string scanner)
    {
        // نفس جسر x86 المستعمل في DCMS يدعم تعريفات TWAIN القديمة داخل التطبيق x64.
        var path = Path.Combine(Path.GetTempPath(), $"inventory-scan-{Guid.NewGuid():N}.pdf");
        try
        {
            var settings = new ScanSettings { Device = scanner, ShowUI = true, CloseUIAfterAcquire = true, TransferMechanism = TwSX.Native };
            var result = await Task.Run(() => Bridgex86.Acquire(path, settings));
            if (result.Status == StatusType.Error || !File.Exists(path))
                throw new InvalidOperationException($"لم يكتمل المسح أو أُلغي. {result.ErrorMessages}");
            return Read(path);
        }
        finally
        {
            // لا نحذف الملف قبل انتهاء القراءة ولا نغيّر الثيم أثناء المسح أو الرسائل.
            try { Bridgex86.ClearCache(); } catch { /* فشل تنظيف الجسر لا يلغي المستند المقروء. */ }
            try { if (File.Exists(path)) File.Delete(path); } catch { /* يظل الملف المؤقت معزولًا عن بيانات العمل. */ }
        }
    }
    #endregion
}
