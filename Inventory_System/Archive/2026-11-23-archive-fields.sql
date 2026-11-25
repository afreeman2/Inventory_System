-- إضافة حقول وصف الأرشيف فقط؛ لا حذف أو إعادة ترميز أو تحديث للملفات والصفوف القديمة.
-- تقبل الأعمدة الجديدة NULL حتى تستمر السجلات القديمة بالعمل دون تعبئة إجبارية.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF DB_NAME() <> N'Cont_maint' THROW 51000, N'يجب تنفيذ التعديل على قاعدة Cont_maint فقط.', 1;
IF OBJECT_ID(N'dbo.T_OwnedPremises_Documents', N'U') IS NULL
 OR OBJECT_ID(N'dbo.T_RentalPremises_Documents', N'U') IS NULL
 OR OBJECT_ID(N'dbo.T_Sub_Doc', N'U') IS NULL
 OR OBJECT_ID(N'dbo.T_Sub_Doc_rental', N'U') IS NULL
 THROW 51001, N'أحد جداول الأرشيف الأصلية غير موجود؛ لم يطبق التعديل.', 1;

IF COL_LENGTH(N'dbo.T_OwnedPremises_Documents', N'AlbumIcon') IS NULL
 ALTER TABLE dbo.T_OwnedPremises_Documents ADD AlbumIcon nvarchar(80) NULL;
IF COL_LENGTH(N'dbo.T_RentalPremises_Documents', N'AlbumIcon') IS NULL
 ALTER TABLE dbo.T_RentalPremises_Documents ADD AlbumIcon nvarchar(80) NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc', N'DocumentTitle') IS NULL
 ALTER TABLE dbo.T_Sub_Doc ADD DocumentTitle nvarchar(250) NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc', N'DocumentDate') IS NULL
 ALTER TABLE dbo.T_Sub_Doc ADD DocumentDate date NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc', N'DocumentSubject') IS NULL
 ALTER TABLE dbo.T_Sub_Doc ADD DocumentSubject nvarchar(1000) NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc', N'DocumentIcon') IS NULL
 ALTER TABLE dbo.T_Sub_Doc ADD DocumentIcon nvarchar(80) NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc_rental', N'DocumentTitle') IS NULL
 ALTER TABLE dbo.T_Sub_Doc_rental ADD DocumentTitle nvarchar(250) NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc_rental', N'DocumentDate') IS NULL
 ALTER TABLE dbo.T_Sub_Doc_rental ADD DocumentDate date NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc_rental', N'DocumentSubject') IS NULL
 ALTER TABLE dbo.T_Sub_Doc_rental ADD DocumentSubject nvarchar(1000) NULL;
IF COL_LENGTH(N'dbo.T_Sub_Doc_rental', N'DocumentIcon') IS NULL
 ALTER TABLE dbo.T_Sub_Doc_rental ADD DocumentIcon nvarchar(80) NULL;
COMMIT TRANSACTION;
